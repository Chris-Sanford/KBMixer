using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace KBMixer
{
    public class AudioDevice
    {
        public MMDevice MMDevice { get; set; } = null!;
        public AudioApp[] AudioApps { get; set; } = Array.Empty<AudioApp>();
    }

    public class AudioApp // Object class to represnt an audio application controllable by KBMixer
    {
        // This should contain all of the fields/properties necessary
        // to enable interaction and manipulation of an audio application and its sessions
        // INCLUDING methods that can be called upon itself
        // such as modifying the volume of the app or its sessions
        // this should moreso represent what exists now and in memory
        // as opposed to what is stored in the config file that's meant to be loaded to in turn build this object\

        public const Single volumeIncrement = 0.05f;
        public const string up = "Up";
        public const string down = "Down";

        // AudioApps should be per-device:per-app rather than per-app regardless of device
        public string DeviceId { get; set; } = "";
        public string AppFriendlyName { get; set; } = "";
        public string AppFileName { get; set; } = "";
        public List<AudioSessionControl> Sessions { get; set; } = new();

        public void AdjustVolume(bool isUp, int? processIndex = null) =>
            Audio.AdjustSessionsVolume(Sessions, isUp, processIndex);
    }

    // Class that contains methods to interact with the audio sessions
    // these are methods that are used to build AudioApp objects
    public static class Audio 
    {
        public const string systemSoundsId = "%b#";

        private static readonly Regex InstanceSuffix = new(@"\s*\(\s*Instance\s+\d+\s*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

        private sealed class SessionReferenceEqualityComparer : IEqualityComparer<AudioSessionControl>
        {
            public static readonly SessionReferenceEqualityComparer Instance = new();
            public bool Equals(AudioSessionControl? x, AudioSessionControl? y) => ReferenceEquals(x, y);
            public int GetHashCode(AudioSessionControl obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>Adjust the render endpoint’s master volume (Windows volume mixer level for that device).</summary>
        public static bool TryAdjustEndpointMasterVolume(MMDevice device, bool isUp, float increment = AudioApp.volumeIncrement)
        {
            try
            {
                var endpoint = device.AudioEndpointVolume;
                float current = endpoint.MasterVolumeLevelScalar;
                float next = isUp
                    ? Math.Min(1f, current + increment)
                    : Math.Max(0f, current - increment);
                endpoint.MasterVolumeLevelScalar = next;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryAdjustEndpointMasterVolume: {ex.Message}");
                return false;
            }
        }

        public static bool TrySetEndpointMasterVolumeScalar(MMDevice device, float scalar)
        {
            try
            {
                device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(scalar, 0f, 1f);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrySetEndpointMasterVolumeScalar: {ex.Message}");
                return false;
            }
        }

        public static void SetSessionsVolumeScalar(IReadOnlyList<AudioSessionControl> sessions, float scalar)
        {
            if (sessions == null || sessions.Count == 0)
                return;
            scalar = Math.Clamp(scalar, 0f, 1f);
            foreach (var session in sessions)
            {
                try
                {
                    session.SimpleAudioVolume.Volume = scalar;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SetSessionsVolumeScalar: {ex.Message}");
                }
            }
        }

        /// <summary>Adjust one session by index, or all sessions when <paramref name="processIndex"/> is null or out of range.</summary>
        public static void AdjustSessionsVolume(IReadOnlyList<AudioSessionControl> sessions, bool isUp, int? processIndex = null)
        {
            if (sessions == null || sessions.Count == 0)
                return;

            if (processIndex.HasValue && processIndex.Value >= 0 && processIndex.Value < sessions.Count)
            {
                var session = sessions[processIndex.Value];
                if (isUp)
                    session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + AudioApp.volumeIncrement);
                else
                    session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - AudioApp.volumeIncrement);
                return;
            }

            foreach (var session in sessions)
            {
                if (isUp)
                    session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + AudioApp.volumeIncrement);
                else
                    session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - AudioApp.volumeIncrement);
            }
        }

        /// <summary>Grouping key for matching config <c>AppFileName</c> to enumerated apps (strips Windows " (Instance N)" labels).</summary>
        public static string GroupingKeyForAppFileName(string? appFileName) => NormalizeAppFileKey(appFileName);

        /// <summary>
        /// Reads sessions straight from the device session manager (same source as the Windows volume mixer).
        /// Used for process-index bounds and single-session volume so we never miss sessions due to <see cref="AudioApp"/> merge quirks.
        /// </summary>
        public static List<AudioSessionControl> CollectSessionsForExecutableGroup(MMDevice device, string configAppFileName)
        {
            string targetKey = GroupingKeyForAppFileName(configAppFileName);
            var list = new List<AudioSessionControl>();
            var seen = new HashSet<AudioSessionControl>(SessionReferenceEqualityComparer.Instance);

            SessionCollection sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                AudioSessionControl session;
                try
                {
                    session = sessions[i];
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CollectSessionsForExecutableGroup: skip index {i}: {ex.Message}");
                    continue;
                }

                if (!TryGetAppIdentity(session, out string appFileName, out _))
                    continue;
                if (GroupingKeyForAppFileName(appFileName) != targetKey)
                    continue;
                if (seen.Add(session))
                    list.Add(session);
            }

            list.Sort(static (a, b) =>
            {
                int c = a.GetProcessID.CompareTo(b.GetProcessID);
                return c != 0 ? c : string.CompareOrdinal(a.GetSessionInstanceIdentifier ?? "", b.GetSessionInstanceIdentifier ?? "");
            });
            return list;
        }

        /// <summary>
        /// Whether a WASAPI session belongs to the app described by the config. Uses executable grouping first, then
        /// display/friendly substring matching so configs that only store "Discord" (or a mixer title like "Discord (Instance 2)")
        /// still collect every related session even when different sessions resolve to different internal exe strings.
        /// </summary>
        public static bool SessionMatchesConfig(string sessionExeIdentity, string? sessionDisplayName, string? sessionFriendlyNameFromIdentity, string configAppFileName, string? configAppFriendlyName)
        {
            string targetKey = GroupingKeyForAppFileName(configAppFileName);
            string exeKey = GroupingKeyForAppFileName(sessionExeIdentity);
            if (exeKey == targetKey)
                return true;

            string disp = (sessionDisplayName ?? "").Trim();
            if (string.IsNullOrEmpty(disp))
                disp = (sessionFriendlyNameFromIdentity ?? "").Trim();

            string needle = (configAppFriendlyName ?? "").Trim();
            if (needle.Length >= 2 && disp.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;

            string cfgBase = Path.GetFileNameWithoutExtension(configAppFileName.Trim());
            if (cfgBase.Length >= 3 && disp.Contains(cfgBase, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public static bool SessionBelongsToConfig(AudioSessionControl session, Config config)
        {
            if (!TryGetAppIdentity(session, out string appFileName, out string appFriendlyName))
                return false;
            string disp = (session.DisplayName ?? "").Trim();
            return SessionMatchesConfig(appFileName, disp, appFriendlyName, config.AppFileName, config.AppFriendlyName);
        }

        /// <summary>True when any session in <paramref name="app"/> is the hotkey target described by <paramref name="config"/>.</summary>
        public static bool AudioAppMatchesConfigOnDevice(AudioApp app, Config config)
        {
            if (config.ControlDeviceMasterVolume)
                return false;
            if (!string.Equals(app.DeviceId, config.DeviceId, StringComparison.OrdinalIgnoreCase))
                return false;
            return app.Sessions.Any(s => SessionBelongsToConfig(s, config));
        }

        /// <summary>Sessions on <paramref name="device"/> that match <paramref name="config"/> (device id + app identity).</summary>
        public static List<AudioSessionControl> CollectSessionsForConfig(MMDevice device, Config config)
        {
            var list = new List<AudioSessionControl>();
            var seen = new HashSet<AudioSessionControl>(SessionReferenceEqualityComparer.Instance);
            SessionCollection sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                AudioSessionControl session;
                try
                {
                    session = sessions[i];
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"CollectSessionsForConfig: skip index {i}: {ex.Message}");
                    continue;
                }

                if (!TryGetAppIdentity(session, out string appFileName, out string appFriendlyName))
                    continue;

                string disp = (session.DisplayName ?? "").Trim();
                if (!SessionMatchesConfig(appFileName, disp, appFriendlyName, config.AppFileName, config.AppFriendlyName))
                    continue;

                if (seen.Add(session))
                    list.Add(session);
            }

            list.Sort(static (a, b) =>
            {
                int c = a.GetProcessID.CompareTo(b.GetProcessID);
                return c != 0 ? c : string.CompareOrdinal(a.GetSessionInstanceIdentifier ?? "", b.GetSessionInstanceIdentifier ?? "");
            });
            return list;
        }

        /// <summary>Writes every render session on every active device (for debugging / validation).</summary>
        public static string WriteSessionDiagnosticFile()
        {
            var sb = new StringBuilder();
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            int d = 0;
            foreach (MMDevice dev in devices)
            {
                sb.AppendLine($"=== [{d++}] {dev.FriendlyName}");
                sb.AppendLine($"    ID: {dev.ID}");
                SessionCollection coll = dev.AudioSessionManager.Sessions;
                for (int i = 0; i < coll.Count; i++)
                {
                    try
                    {
                        AudioSessionControl s = coll[i];
                        TryGetAppIdentity(s, out string exeId, out string fr);
                        string key = GroupingKeyForAppFileName(exeId);
                        sb.AppendLine($"  [{i}] PID={s.GetProcessID} Display=\"{s.DisplayName}\"");
                        sb.AppendLine($"        exeId={exeId}  groupingKey={key}  identityFriendly={fr}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  [{i}] <error: {ex.Message}>");
                    }
                }
                sb.AppendLine();
            }

            string path = Path.Combine(Path.GetTempPath(), "KBMixer-audio-sessions.txt");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        public static AudioDevice[] GetAudioDevices()
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            List<AudioDevice> audioDevices = new List<AudioDevice>();
            foreach (MMDevice device in devices)
            {
                audioDevices.Add(new AudioDevice
                {
                    MMDevice = device,
                    AudioApps = GetAudioDeviceApps(device)
                });
            }
            return audioDevices.ToArray();
        }

        public static AudioApp[] GetAudioDeviceApps(MMDevice device)
        {
            List<AudioApp> audioApps = new List<AudioApp>();

            SessionCollection sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                AudioSessionControl session;
                try
                {
                    session = sessions[i];
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Skipping audio session {i}: {ex.Message}");
                    continue;
                }

                if (!TryGetAppIdentity(session, out string appFileName, out string appFriendlyName))
                    continue;

                // Same executable often appears under slightly different keys (e.g. "Discord" vs "Discord.exe",
                // casing). Merge into one AudioApp per device so session indices match the volume mixer and
                // we do not run AdjustVolume once per duplicate row.
                string mergeKey = NormalizeAppFileKey(appFileName);
                var existingApp = audioApps.FirstOrDefault(a =>
                    a.DeviceId == device.ID && NormalizeAppFileKey(a.AppFileName) == mergeKey);

                if (existingApp != null)
                {
                    existingApp.Sessions.Add(session);
                    if (!existingApp.AppFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        appFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        existingApp.AppFileName = appFileName;
                    }
                }
                else
                {
                    audioApps.Add(new AudioApp
                    {
                        DeviceId = device.ID,
                        AppFriendlyName = appFriendlyName,
                        AppFileName = appFileName,
                        Sessions = new List<AudioSessionControl> { session }
                    });
                }
            }

            // Debug write the fields of all audio apps
            foreach (var app in audioApps)
            {
                Debug.WriteLine($"DeviceId: {app.DeviceId}, AppFriendlyName: {app.AppFriendlyName}, AppFileName: {app.AppFileName}, SessionsCount: {app.Sessions.Count}");
            }

            return audioApps.ToArray();
        }

        /// <summary>
        /// Stable key for grouping sessions on a device. Strips trailing <c> (Instance N)</c> from display-derived
        /// names so Windows' duplicate labels (e.g. Discord) merge with <c>Discord.exe</c> from the process path.
        /// </summary>
        private static string NormalizeAppFileKey(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            string t = name.Trim();
            if (string.Equals(t, systemSoundsId, StringComparison.Ordinal))
                return t.ToLowerInvariant();
            t = InstanceSuffix.Replace(t, "").Trim();
            if (!t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                t += ".exe";
            return t.ToLowerInvariant();
        }

        private static bool TryGetAppIdentity(AudioSessionControl session, out string appFileName, out string appFriendlyName)
        {
            appFileName = "";
            appFriendlyName = "";

            try
            {
                if (session.IsSystemSoundsSession)
                {
                    appFileName = systemSoundsId;
                    appFriendlyName = "System Sounds";
                    return true;
                }

                uint pid = session.GetProcessID;
                if (pid != 0)
                {
                    try
                    {
                        using Process proc = Process.GetProcessById((int)pid);
                        string? exePath = null;
                        try
                        {
                            exePath = proc.MainModule?.FileName;
                        }
                        catch
                        {
                            // Some sessions are elevated or protected; ProcessName still works for volume routing.
                        }

                        appFileName = !string.IsNullOrEmpty(exePath)
                            ? Path.GetFileName(exePath)
                            : EnsureExeSuffix(proc.ProcessName);

                        if (string.IsNullOrEmpty(appFileName))
                            return false;

                        string display = session.DisplayName ?? "";
                        appFriendlyName = !string.IsNullOrWhiteSpace(display)
                            ? display.Trim()
                            : Path.GetFileNameWithoutExtension(appFileName);
                        return true;
                    }
                    catch
                    {
                        // Process may have exited between enumeration and lookup.
                    }
                }

                string displayName = (session.DisplayName ?? "").Trim();
                if (TryParseExeFromSessionInstanceId(session.GetSessionInstanceIdentifier, out string parsedExe))
                {
                    appFileName = parsedExe;
                    appFriendlyName = !string.IsNullOrEmpty(displayName)
                        ? displayName
                        : Path.GetFileNameWithoutExtension(parsedExe);
                    return true;
                }

                if (!string.IsNullOrEmpty(displayName))
                {
                    appFileName = displayName;
                    appFriendlyName = displayName;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryGetAppIdentity failed: {ex.Message}");
            }

            return false;
        }

        private static string EnsureExeSuffix(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return "";
            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName
                : processName + ".exe";
        }

        /// <summary>
        /// Pulls the .exe segment from a session instance id without relying on a "%b" marker
        /// (format varies by OS build and app packaging).
        /// </summary>
        private static bool TryParseExeFromSessionInstanceId(string? instanceId, out string exeName)
        {
            exeName = "";
            if (string.IsNullOrEmpty(instanceId))
                return false;

            int exeIdx = instanceId.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx < 0)
                return false;

            int segmentEnd = exeIdx + 4;
            int start = instanceId.LastIndexOf('\\', exeIdx);
            if (start < 0)
                start = instanceId.LastIndexOf('/', exeIdx);
            if (start < 0)
                start = instanceId.LastIndexOf('|', exeIdx);
            start++;

            if (start >= segmentEnd || start < 0)
                return false;

            exeName = instanceId.Substring(start, segmentEnd - start);
            return exeName.Length > 0;
        }

        public static AudioSessionControl GetAudioSessionControls(MMDevice device)
        {
            // MMDevice.AudioSessionManager.AudioSessionControl seems to be a class that exists for the purpose of allowing KBMixer
            // to control the main audio session of the device. It's not something that gets us information
            // on individual app session
            var sessionManager = device.AudioSessionManager;
            return sessionManager.AudioSessionControl;
        }
    }
}
