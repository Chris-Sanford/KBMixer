using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace KBMixer;

/// <summary>
/// Owns every Core Audio (WASAPI) call KBMixer makes, on one dedicated MTA thread.
/// <para>
/// Calls into the Windows audio service are cross-process and can stall for a long time when a game is hammering it.
/// Keeping them here means the UI thread (which also runs the raw-input window procedure) never blocks on audio.
/// Writes are coalesced: a slider drag only applies its latest value, and queued wheel notches are applied in one
/// read-modify-write per target.
/// </para>
/// <para>
/// Devices are kept for the life of the endpoint. NAudio registers a COM callback for every
/// <see cref="AudioSessionManager"/> and <see cref="AudioEndpointVolume"/> it creates and only unregisters on Dispose,
/// so creating fresh <see cref="MMDevice"/> objects on every scan leaks one live registration per device per scan.
/// </para>
/// </summary>
internal sealed class AudioWorker : IDisposable
{
    public sealed class Snapshot
    {
        public required AudioDevice[] Devices { get; init; }
        public string? DefaultDeviceId { get; init; }
    }

    /// <summary>A mixer row to poll. <paramref name="Device"/> set means the device master volume.</summary>
    public readonly record struct PollTarget(object Key, IReadOnlyList<AudioSessionControl> Sessions, AudioDevice? Device);

    /// <summary>Null fields could not be read this time (session went away mid-poll).</summary>
    public readonly record struct PollResult(object Key, float? Volume, bool? Muted);

    const int PollIntervalMs = 120;
    const int ScanIntervalMs = 3000;
    const int MinScanGapMs = 750;
    const int DeviceNameRefreshMs = 60_000;

    sealed class PendingAdjust
    {
        public IReadOnlyList<AudioSessionControl> Sessions = Array.Empty<AudioSessionControl>();
        public int? ProcessIndex;
        public MMDevice? Device;
        public readonly List<bool> Steps = new();
    }

    readonly record struct PendingSet(IReadOnlyList<AudioSessionControl> Sessions, MMDevice? Device, float Scalar);

    readonly Thread _thread;
    readonly AutoResetEvent _wake = new(false);
    readonly ManualResetEventSlim _firstScanDone = new(false);
    readonly object _gate = new();
    volatile bool _stopping;

    // Guarded by _gate.
    readonly Dictionary<Guid, PendingAdjust> _adjusts = new();
    readonly Dictionary<object, PendingSet> _sets = new(ReferenceEqualityComparer.Instance);
    readonly Queue<Action> _actions = new();
    readonly List<TaskCompletionSource<Snapshot?>> _scanWaiters = new();
    bool _scanRequested = true;
    PollTarget[] _pollTargets = Array.Empty<PollTarget>();
    bool _pollEnabled;
    bool _pollNow;

    // Worker thread only.
    MMDeviceEnumerator? _enumerator;
    readonly Dictionary<string, MMDevice> _devices = new(StringComparer.OrdinalIgnoreCase);
    // Reading a device name costs tens of milliseconds (see Audio.ReadDeviceNames); re-read only to catch renames.
    readonly Dictionary<string, (string FriendlyName, string? Description, long ReadAt)> _deviceNames = new(StringComparer.OrdinalIgnoreCase);
    string? _lastFingerprint;
    long _lastScanTicks = long.MinValue / 2;

    /// <summary>Raised on the worker thread whenever the set of devices/sessions changes.</summary>
    public event Action<Snapshot>? SnapshotChanged;

    /// <summary>Raised on the worker thread with fresh volume/mute values for the poll targets.</summary>
    public event Action<PollResult[]>? VolumesRead;

    public Snapshot? Current { get; private set; }

    public AudioWorker()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "KBMixer audio" };
        _thread.SetApartmentState(ApartmentState.MTA);
        // A full-screen game can saturate every core at normal priority; the worker does very little work,
        // so a small bump keeps hotkey volume changes responsive without competing meaningfully with the game.
        _thread.Priority = ThreadPriority.AboveNormal;
    }

    public void Start() => _thread.Start();

    /// <summary>Blocks until the first device scan has finished (or the timeout passes).</summary>
    public Snapshot? WaitForFirstSnapshot(TimeSpan timeout)
    {
        _firstScanDone.Wait(timeout);
        return Current;
    }

    // ──────────────────── Requests (any thread) ────────────────────

    /// <summary>Queue one wheel notch for a hotkey profile. <paramref name="device"/> set means device master volume.</summary>
    public void QueueAdjust(Guid key, IReadOnlyList<AudioSessionControl> sessions, int? processIndex, MMDevice? device, bool isUp)
    {
        lock (_gate)
        {
            if (!_adjusts.TryGetValue(key, out var pending))
                _adjusts[key] = pending = new PendingAdjust();
            pending.Sessions = sessions;
            pending.ProcessIndex = processIndex;
            pending.Device = device;
            pending.Steps.Add(isUp);
        }
        _wake.Set();
    }

    /// <summary>Set an absolute volume; a newer request for the same <paramref name="key"/> replaces an unapplied one.</summary>
    public void QueueSetVolume(object key, IReadOnlyList<AudioSessionControl> sessions, MMDevice? device, float scalar)
    {
        lock (_gate)
            _sets[key] = new PendingSet(sessions, device, Math.Clamp(scalar, 0f, 1f));
        _wake.Set();
    }

    /// <summary>Toggle mute. For sessions: mutes all if any is unmuted, otherwise unmutes all.</summary>
    public void QueueToggleMute(IReadOnlyList<AudioSessionControl> sessions, MMDevice? device)
    {
        lock (_gate)
            _actions.Enqueue(() => ToggleMute(sessions, device));
        _wake.Set();
    }

    /// <summary>Ask for a rescan soon. Cheap to call often: scans are rate-limited and only publish on change.</summary>
    public void RequestScan()
    {
        lock (_gate)
            _scanRequested = true;
        _wake.Set();
    }

    /// <summary>Rescan now and complete with the resulting snapshot (whether or not anything changed).</summary>
    public Task<Snapshot?> ScanNowAsync()
    {
        var tcs = new TaskCompletionSource<Snapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _scanWaiters.Add(tcs);
            _scanRequested = true;
        }
        _wake.Set();
        return tcs.Task;
    }

    public void SetPollTargets(PollTarget[] targets)
    {
        lock (_gate)
        {
            _pollTargets = targets;
            _pollNow = true;
        }
        _wake.Set();
    }

    /// <summary>Volume polling only matters while the mixer is on screen; turn it off while hidden in the tray.</summary>
    public void SetPollingEnabled(bool enabled)
    {
        lock (_gate)
        {
            _pollEnabled = enabled;
            _pollNow = enabled;
        }
        _wake.Set();
    }

    public void Dispose()
    {
        _stopping = true;
        _wake.Set();
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    // ──────────────────── Worker thread ────────────────────

    void Run()
    {
        try { _enumerator = new MMDeviceEnumerator(); }
        catch (Exception ex) { Debug.WriteLine($"AudioWorker: enumerator failed: {ex.Message}"); }

        long nextPoll = 0;
        while (!_stopping)
        {
            try
            {
                bool wrote = DrainWrites();

                long now = Environment.TickCount64;
                bool scanRequested;
                bool waitersPending;
                bool pollEnabled;
                bool pollNow;
                lock (_gate)
                {
                    scanRequested = _scanRequested;
                    waitersPending = _scanWaiters.Count > 0;
                    pollEnabled = _pollEnabled;
                    pollNow = _pollNow;
                }

                long sinceScan = now - _lastScanTicks;
                if (sinceScan >= ScanIntervalMs || waitersPending || (scanRequested && sinceScan >= MinScanGapMs))
                {
                    lock (_gate)
                        _scanRequested = false;
                    Scan();
                    _lastScanTicks = Environment.TickCount64;
                    now = _lastScanTicks;
                }

                if (pollEnabled && (wrote || pollNow || now >= nextPoll))
                {
                    lock (_gate)
                        _pollNow = false;
                    Poll();
                    nextPoll = Environment.TickCount64 + PollIntervalMs;
                    now = Environment.TickCount64;
                }

                long wait = _lastScanTicks + ScanIntervalMs - now;
                lock (_gate)
                {
                    if (_scanRequested)
                        wait = Math.Min(wait, _lastScanTicks + MinScanGapMs - now);
                }
                if (pollEnabled)
                    wait = Math.Min(wait, nextPoll - now);
                _wake.WaitOne((int)Math.Clamp(wait, 1, ScanIntervalMs));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioWorker loop: {ex}");
                _wake.WaitOne(250);
            }
        }

        lock (_gate)
        {
            foreach (var waiter in _scanWaiters)
                waiter.TrySetResult(Current);
            _scanWaiters.Clear();
        }
        foreach (var device in _devices.Values)
        {
            try { device.Dispose(); } catch { }
        }
        _devices.Clear();
        try { _enumerator?.Dispose(); } catch { }
    }

    bool DrainWrites()
    {
        List<PendingAdjust>? adjusts = null;
        List<PendingSet>? sets = null;
        List<Action>? actions = null;
        lock (_gate)
        {
            if (_adjusts.Count > 0) { adjusts = _adjusts.Values.ToList(); _adjusts.Clear(); }
            if (_sets.Count > 0) { sets = _sets.Values.ToList(); _sets.Clear(); }
            if (_actions.Count > 0) { actions = _actions.ToList(); _actions.Clear(); }
        }

        bool sessionFailed = false;
        if (adjusts != null)
            foreach (var a in adjusts)
                sessionFailed |= !ApplyAdjust(a);
        if (sets != null)
            foreach (var s in sets)
                sessionFailed |= !ApplySet(s);
        if (actions != null)
        {
            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception ex) { Debug.WriteLine($"AudioWorker action: {ex.Message}"); }
            }
        }

        // A session that throws has usually expired (app closed); rescan so the hotkey targets its replacement.
        if (sessionFailed)
        {
            lock (_gate)
                _scanRequested = true;
        }
        return adjusts != null || sets != null || actions != null;
    }

    static bool ApplyAdjust(PendingAdjust a)
    {
        if (a.Device != null)
        {
            try
            {
                var endpoint = a.Device.AudioEndpointVolume;
                float current = endpoint.MasterVolumeLevelScalar;
                float next = Audio.ApplyVolumeSteps(current, a.Steps);
                if (next != current)
                    endpoint.MasterVolumeLevelScalar = next;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AudioWorker adjust master: {ex.Message}");
                return false;
            }
        }

        var sessions = a.Sessions;
        bool ok = true;
        if (a.ProcessIndex is int i && i >= 0 && i < sessions.Count)
            return AdjustSession(sessions[i], a.Steps);
        foreach (var session in sessions)
            ok &= AdjustSession(session, a.Steps);
        return ok;
    }

    static bool AdjustSession(AudioSessionControl session, IReadOnlyList<bool> steps)
    {
        try
        {
            var volume = session.SimpleAudioVolume;
            float current = volume.Volume;
            float next = Audio.ApplyVolumeSteps(current, steps);
            if (next != current)
                volume.Volume = next;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioWorker adjust session: {ex.Message}");
            return false;
        }
    }

    static bool ApplySet(PendingSet s)
    {
        if (s.Device != null)
            return Audio.TrySetEndpointMasterVolumeScalar(s.Device, s.Scalar);

        bool ok = true;
        foreach (var session in s.Sessions)
        {
            try { session.SimpleAudioVolume.Volume = s.Scalar; }
            catch { ok = false; }
        }
        return ok;
    }

    static void ToggleMute(IReadOnlyList<AudioSessionControl> sessions, MMDevice? device)
    {
        if (device != null)
        {
            var endpoint = device.AudioEndpointVolume;
            endpoint.Mute = !endpoint.Mute;
            return;
        }

        bool anyUnmuted = false;
        foreach (var session in sessions)
        {
            // Let an unreadable session abort the toggle rather than leave the group inconsistent.
            if (!session.SimpleAudioVolume.Mute)
                anyUnmuted = true;
        }

        foreach (var session in sessions)
        {
            try { session.SimpleAudioVolume.Mute = anyUnmuted; }
            catch { }
        }
    }

    void Poll()
    {
        PollTarget[] targets;
        lock (_gate)
            targets = _pollTargets;
        if (targets.Length == 0)
            return;

        var results = new PollResult[targets.Length];
        for (int t = 0; t < targets.Length; t++)
        {
            var target = targets[t];
            float? volume = null;
            bool? muted = null;

            if (target.Device != null)
            {
                try
                {
                    var endpoint = target.Device.MMDevice.AudioEndpointVolume;
                    volume = endpoint.MasterVolumeLevelScalar;
                    muted = endpoint.Mute;
                    target.Device.MasterVolume = volume.Value;
                    target.Device.MasterMuted = muted.Value;
                }
                catch { }
            }
            else if (target.Sessions.Count > 0)
            {
                try
                {
                    volume = target.Sessions[0].SimpleAudioVolume.Volume;
                    Audio.Info(target.Sessions[0]).Volume = volume.Value;
                }
                catch { }

                bool allMuted = true;
                bool readAll = true;
                foreach (var session in target.Sessions)
                {
                    try
                    {
                        bool m = session.SimpleAudioVolume.Mute;
                        Audio.Info(session).Muted = m;
                        if (!m)
                            allMuted = false;
                    }
                    catch
                    {
                        // Keep the last known state when a session disappears during a refresh.
                        readAll = false;
                        break;
                    }
                }
                if (readAll)
                    muted = allMuted;
            }

            results[t] = new PollResult(target.Key, volume, muted);
        }

        VolumesRead?.Invoke(results);
    }

    void Scan()
    {
        Snapshot? published = null;
        try
        {
            var live = new List<MMDevice>();
            if (_enumerator != null)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (MMDevice fresh in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    string id;
                    try { id = fresh.ID; }
                    catch { continue; }
                    if (!seen.Add(id))
                        continue;

                    if (_devices.TryGetValue(id, out var known))
                    {
                        // Reuse the long-lived device; just re-read its session list.
                        try { known.AudioSessionManager.RefreshSessions(); }
                        catch (Exception ex) { Debug.WriteLine($"AudioWorker RefreshSessions({id}): {ex.Message}"); }
                        live.Add(known);
                    }
                    else
                    {
                        _devices[id] = fresh;
                        try { fresh.AudioSessionManager.OnSessionCreated += (_, _) => RequestScan(); }
                        catch (Exception ex) { Debug.WriteLine($"AudioWorker session notifications({id}): {ex.Message}"); }
                        live.Add(fresh);
                    }
                }

                foreach (var gone in _devices.Keys.Where(k => !seen.Contains(k)).ToList())
                {
                    // Unregisters NAudio's COM callbacks for the removed endpoint.
                    try { _devices[gone].Dispose(); } catch { }
                    _devices.Remove(gone);
                    _deviceNames.Remove(gone);
                }
            }

            var devices = new List<AudioDevice>(live.Count);
            long now = Environment.TickCount64;
            bool renameCheckDone = false; // at most one stale re-read per scan so a wheel notch never waits long
            foreach (var device in live)
            {
                (string, string?)? names = null;
                try
                {
                    string id = device.ID;
                    bool known = _deviceNames.TryGetValue(id, out var cached);
                    if (!known || (!renameCheckDone && now - cached.ReadAt >= DeviceNameRefreshMs))
                    {
                        renameCheckDone |= known;
                        var read = Audio.ReadDeviceNames(device);
                        cached = (read.FriendlyName, read.Description, now);
                        _deviceNames[id] = cached;
                    }
                    names = (cached.FriendlyName, cached.Description);
                }
                catch { }

                var built = Audio.BuildAudioDevice(device, names);
                if (built != null)
                    devices.Add(built);
            }

            Audio.RetainProcessImageCache(devices);

            string fingerprint = Audio.GetSnapshotFingerprint(devices);
            if (Current == null || !string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
            {
                _lastFingerprint = fingerprint;
                published = new Snapshot { Devices = devices.ToArray(), DefaultDeviceId = TryGetDefaultDeviceId() };
                Current = published;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioWorker scan: {ex}");
        }
        finally
        {
            _firstScanDone.Set();
        }

        List<TaskCompletionSource<Snapshot?>> waiters;
        lock (_gate)
        {
            waiters = _scanWaiters.ToList();
            _scanWaiters.Clear();
        }

        if (published != null)
            SnapshotChanged?.Invoke(published);
        foreach (var waiter in waiters)
            waiter.TrySetResult(Current);
    }

    string? TryGetDefaultDeviceId()
    {
        try { return _enumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID; }
        catch { return null; }
    }
}
