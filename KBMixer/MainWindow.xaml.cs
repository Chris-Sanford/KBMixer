using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Linearstar.Windows.RawInput;
using NAudio.CoreAudioApi;
using WinRT.Interop;

namespace KBMixer;

public sealed partial class MainWindow : Window
{
    const uint WM_INPUT = 0x00FF;
    const uint WM_SIZE = 0x0005;
    const int SIZE_MINIMIZED = 1;
    const long SliderCooldownTicks = TimeSpan.TicksPerMillisecond * 400;

    public const int MouseWheelUp = 120;
    public const int MouseWheelDown = -120;
    public const string Up = "Up";
    public const string Down = "Down";
    public const string MouseWheelButton = "MouseWheel";

    static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KBMixer");

    public ObservableCollection<MixerChannelViewModel> MasterMixerRows { get; } = new();
    public ObservableCollection<MixerChannelViewModel> AppMixerRows { get; } = new();

    public AudioDevice[] audioDevices = Array.Empty<AudioDevice>();
    public AudioApp[] audioApps = Array.Empty<AudioApp>();
    public Config[] configs = Array.Empty<Config>();
    public Config currentConfig = null!;
    public int[] hotkeysToListenFor = Array.Empty<int>();
    public int[] hotkeysHeld = Array.Empty<int>();
    public bool listeningForHotkeyAdd;

    bool suspendSessionPickerEvents;
    bool suspendMixerVolumeEvents;
    bool suspendDeviceComboEvents;

    Dictionary<Guid, List<AudioSessionControl>> cachedSessionsByConfig = new();

    DispatcherTimer? mixerRefreshTimer;
    int _refreshTickCount;
    int _lastKnownSessionCount;
    AppWindow? _appWindow;
    H.NotifyIcon.TaskbarIcon? trayIcon;
    bool _closeConfirmed;

    delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll")]
    static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll")]
    static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll")]
    static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    SubclassProc? _wndProcDelegate;
    IntPtr _hwnd;

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();

        SetupWindow();

        MasterMixerItems.ItemsSource = MasterMixerRows;
        AppMixerItems.ItemsSource = AppMixerRows;

        if (!UiGoldenCapture.Enabled)
        {
            SetupTrayIcon();
            SetupRawInput();
        }

        if (UiGoldenCapture.Enabled && UiGoldenCapture.UseMockMixer)
        {
            audioDevices = Array.Empty<AudioDevice>();
            audioApps = Array.Empty<AudioApp>();
            configs = [CreateGoldenMockConfig()];
            currentConfig = configs[0];
            PopulateConfigs(0);
            LoadConfigToForm();
        }
        else
        {
            audioDevices = Audio.GetAudioDevices();
            var audioAppsList = new List<AudioApp>();
            foreach (var device in audioDevices)
                audioAppsList.AddRange(Audio.GetAudioDeviceApps(device.MMDevice));
            audioApps = audioAppsList.ToArray();

            configs = Configurations.LoadConfigsFromDisk();
            ReconcileConfigDevices();
            if (configs.Length == 0)
                ButtonNewConfig_OnClick(this, new RoutedEventArgs());
            else
            {
                currentConfig = configs[0];
                PopulateConfigs(0);
                LoadConfigToForm();
            }
        }

        RebuildSessionCache();
        UpdateHotkeysToListenFor();
        SnapshotSessionCount();

        RootGrid.Loaded += (_, _) =>
        {
            RebuildMixerStrip();

            if (!UiGoldenCapture.Enabled)
            {
                mixerRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                mixerRefreshTimer.Tick += (_, _) => OnRefreshTimerTick();
                mixerRefreshTimer.Start();

                ShowDeviceRebindNoticeIfNeeded();
                ShowConfigLoadErrorIfNeeded();
                _ = UpdateChecker.CheckAndPromptAsync(Content.XamlRoot);
            }
        };

        if (startMinimized && !UiGoldenCapture.Enabled)
        {
            RootGrid.Loaded += (_, _) =>
            {
                _appWindow?.Hide();
                if (trayIcon != null)
                    trayIcon.Visibility = Visibility.Visible;
            };
        }

        if (UiGoldenCapture.Enabled)
        {
            RootGrid.Loaded += async (_, _) =>
            {
                await Task.Delay(500);
                await CaptureGoldenUiAndExitAsync();
            };
        }

        Closed += (_, _) =>
        {
            mixerRefreshTimer?.Stop();
            if (_wndProcDelegate != null)
                RemoveWindowSubclass(_hwnd, _wndProcDelegate, 0);
            trayIcon?.Dispose();
        };
    }

    void SetupWindow()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        int w = UiGoldenCapture.Enabled ? 800 : 720;
        int h = UiGoldenCapture.Enabled ? 900 : 820;
        _appWindow.Resize(new Windows.Graphics.SizeInt32(w, h));

        AppIconHelper.ApplyWindowIcon(_appWindow);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        if (!UiGoldenCapture.Enabled)
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

            _appWindow.Closing += OnWindowClosing;
        }
    }

    async void OnWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || UiGoldenCapture.Enabled)
            return;

        if (IsCloseWarningDismissed())
            return;

        args.Cancel = true;
        await ShowCloseWarningAsync();
    }

    async Task ShowCloseWarningAsync()
    {
        var checkBox = new CheckBox { Content = "Don't remind me again" };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = "KBMixer needs to be running in the background for your hotkeys and volume control to work. " +
                   "Would you like to minimize to the system tray instead?",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(checkBox);

        var dialog = new ContentDialog
        {
            Title = "Close KBMixer?",
            Content = panel,
            PrimaryButtonText = "Minimize to tray",
            SecondaryButtonText = "Close anyway",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (checkBox.IsChecked == true)
            SetCloseWarningDismissed();

        if (result == ContentDialogResult.Primary)
        {
            _appWindow?.Hide();
            if (trayIcon != null)
                trayIcon.Visibility = Visibility.Visible;
        }
        else if (result == ContentDialogResult.Secondary)
        {
            _closeConfirmed = true;
            Close();
        }
    }

    static bool IsCloseWarningDismissed()
    {
        try { return File.Exists(Path.Combine(AppDataDir, ".close-without-warning")); }
        catch { return false; }
    }

    static void SetCloseWarningDismissed()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(Path.Combine(AppDataDir, ".close-without-warning"), "");
        }
        catch { }
    }

    void SetupTrayIcon()
    {
        trayIcon = new H.NotifyIcon.TaskbarIcon();
        trayIcon.ToolTipText = "KBMixer";
        var icoPath = Path.Combine(AppContext.BaseDirectory, "KBMixer.ico");
        if (File.Exists(icoPath))
            trayIcon.Icon = new System.Drawing.Icon(icoPath);
        // Right-click menu: native Win32 popup so it works for unpackaged WinUI while the window is hidden.
        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Open KBMixer" };
        openItem.Click += (_, _) => ShowFromTray();
        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => ExitFromTray();
        menu.Items.Add(openItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(exitItem);
        trayIcon.ContextMenuMode = H.NotifyIcon.ContextMenuMode.PopupMenu;
        trayIcon.ContextFlyout = menu;

        trayIcon.ForceCreate();
        trayIcon.Visibility = Visibility.Collapsed;
        trayIcon.NoLeftClickDelay = true;
        trayIcon.LeftClickCommand = new RelayCommand(ShowFromTray);
    }

    void ShowFromTray()
    {
        _appWindow?.Show();
        if (trayIcon != null)
            trayIcon.Visibility = Visibility.Collapsed;

        // The app list may be stale after sitting in the tray; refresh as soon as the window is shown.
        try { RefreshAudioDevicesAndApps(); }
        catch { }
    }

    void ExitFromTray()
    {
        _closeConfirmed = true;
        DispatcherQueue.TryEnqueue(() => Close());
    }

    void SetupRawInput()
    {
        _wndProcDelegate = WndProcHook;
        SetWindowSubclass(_hwnd, _wndProcDelegate, 0, 0);
        RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, _hwnd);
        RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, _hwnd);
    }

    IntPtr WndProcHook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (msg == WM_INPUT)
        {
            var data = RawInputData.FromHandle(lParam);
            if (data is RawInputKeyboardData keyboardData)
            {
                int virtualKey = keyboardData.Keyboard.VirutalKey;
                bool keyUp = keyboardData.Keyboard.Flags.ToString() == Up;

                if (listeningForHotkeyAdd)
                    AddHotkey(virtualKey);
                else
                    UpdateHotkeysHeld(virtualKey, keyUp);
            }
            else if (data is RawInputMouseData mouseData)
            {
                bool isMouseWheel = mouseData.Mouse.Buttons.ToString() == MouseWheelButton;
                bool wasUpOrDown = mouseData.Mouse.ButtonData == MouseWheelUp || mouseData.Mouse.ButtonData == MouseWheelDown;

                if (isMouseWheel && wasUpOrDown)
                {
                    var matchingConfigs = configs
                        .Where(config => config.Hotkeys.Length > 0 && config.Hotkeys.SequenceEqual(hotkeysHeld))
                        .ToArray();

                    bool anyAdjusted = false;
                    foreach (var config in matchingConfigs)
                    {
                        bool isUp = mouseData.Mouse.ButtonData == MouseWheelUp;
                        if (config.ControlDeviceMasterVolume)
                        {
                            var device = audioDevices
                                .FirstOrDefault(d => string.Equals(d.MMDevice.ID, config.DeviceId, StringComparison.OrdinalIgnoreCase))
                                ?.MMDevice;
                            if (device != null)
                            {
                                Audio.TryAdjustEndpointMasterVolume(device, isUp);
                                anyAdjusted = true;
                            }
                            continue;
                        }

                        var sessions = GetCachedSessions(config);
                        if (sessions.Count == 0)
                            continue;

                        if (config.ControlSingleSession)
                            Audio.AdjustSessionsVolume(sessions, isUp, config.ProcessIndex);
                        else
                            Audio.AdjustSessionsVolume(sessions, isUp, null);
                        anyAdjusted = true;
                    }

                    if (anyAdjusted)
                        DispatcherQueue.TryEnqueue(() => RefreshMixerVolumesFromAudio());
                }
            }
        }
        else if (msg == WM_SIZE && (int)wParam == SIZE_MINIMIZED && !UiGoldenCapture.Enabled)
        {
            _appWindow?.Hide();
            if (trayIcon != null)
                trayIcon.Visibility = Visibility.Visible;
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    static Config CreateGoldenMockConfig() =>
        new()
        {
            ConfigId = Guid.Parse("00000000-0000-4000-8000-000000000001"),
            DeviceId = "golden-mock-device",
            AppFileName = "chrome.exe",
            AppFriendlyName = "Google Chrome",
            Hotkeys = new[] { 16 },
            ControlSingleSession = false,
            ProcessIndex = 0,
            ControlDeviceMasterVolume = false
        };

    async Task CaptureGoldenUiAndExitAsync()
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(UiGoldenCapture.OutputPath)
                ? Path.Combine(Environment.CurrentDirectory, "kbmixer-golden.png")
                : UiGoldenCapture.OutputPath!;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(RootGrid);
            var pixelBuffer = await rtb.GetPixelsAsync();
            var reader = Windows.Storage.Streams.DataReader.FromBuffer(pixelBuffer);
            var pixels = new byte[pixelBuffer.Length];
            reader.ReadBytes(pixels);

            using var fileStream = File.Create(path);
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                fileStream.AsRandomAccessStream());
            encoder.SetPixelData(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                (uint)rtb.PixelWidth,
                (uint)rtb.PixelHeight,
                96, 96,
                pixels);
            await encoder.FlushAsync();
        }
        finally
        {
            _closeConfirmed = true;
            Close();
        }
    }

    // ──────────────────── Refresh timer ────────────────────

    // Timer runs at 120ms. Every ~3s we compare a fingerprint of live sessions/devices; every ~10s we do a full
    // re-enumeration regardless, so apps that start/stop audio show up without the user opening "Choose…".
    const int FingerprintCheckEveryTicks = 25;
    const int FullRefreshEveryTicks = 84;
    const long UserInteractionQuietTicks = TimeSpan.TicksPerSecond * 2;

    void OnRefreshTimerTick()
    {
        RefreshMixerVolumesFromAudio();

        _refreshTickCount++;

        if (_refreshTickCount % FullRefreshEveryTicks == 0)
        {
            if (!UserRecentlyInteractedWithMixer())
                RefreshAudioDevicesAndApps();
            return;
        }

        if (_refreshTickCount % FingerprintCheckEveryTicks == 0)
            CheckForSessionListChanges();
    }

    bool UserRecentlyInteractedWithMixer()
    {
        long now = DateTime.UtcNow.Ticks;
        foreach (var ch in MasterMixerRows)
            if (now - ch.LastUserInteractionTicks < UserInteractionQuietTicks) return true;
        foreach (var ch in AppMixerRows)
            if (now - ch.LastUserInteractionTicks < UserInteractionQuietTicks) return true;
        return false;
    }

    void SnapshotSessionCount()
    {
        int count = 0;
        foreach (var d in audioDevices)
            foreach (var a in d.AudioApps)
                count += a.Sessions.Count;
        _lastKnownSessionCount = count;

        try { _lastSessionFingerprint = Audio.GetSessionFingerprint(audioDevices.Select(d => d.MMDevice)); }
        catch { _lastSessionFingerprint = ""; }
    }

    string _lastSessionFingerprint = "";

    void CheckForSessionListChanges()
    {
        try
        {
            // Enumerate fresh so newly plugged-in devices are part of the comparison too.
            var enumerator = new MMDeviceEnumerator();
            var live = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            string fingerprint = Audio.GetSessionFingerprint(live.Cast<MMDevice>());

            if (!string.Equals(fingerprint, _lastSessionFingerprint, StringComparison.Ordinal))
            {
                _lastSessionFingerprint = fingerprint;
                if (!UserRecentlyInteractedWithMixer())
                    RefreshAudioDevicesAndApps();
            }
        }
        catch { }
    }

    // ──────────────────── Mixer strip ────────────────────

    void RebuildMixerStrip()
    {
        MasterMixerRows.Clear();
        AppMixerRows.Clear();

        if (UiGoldenCapture.Enabled && UiGoldenCapture.UseMockMixer)
        {
            var mockMaster = new MixerChannelViewModel(true, "Volume", null, null, null);
            mockMaster.SyncVolumeFromAudio(0.72f);
            mockMaster.IsHotkeyTarget = false;
            MasterMixerRows.Add(mockMaster);

            var chrome = new MixerChannelViewModel(false, "Google Chrome", null, null, null);
            chrome.SyncVolumeFromAudio(1f);
            chrome.IsHotkeyTarget = true;
            AppMixerRows.Add(chrome);

            var sys = new MixerChannelViewModel(false, "System Sounds", null, null, null);
            sys.SyncVolumeFromAudio(0.12f);
            sys.IsHotkeyTarget = false;
            AppMixerRows.Add(sys);
            return;
        }

        if (audioDevices.Length == 0 || ComboBoxDevice.SelectedIndex < 0)
            return;

        var device = audioDevices[ComboBoxDevice.SelectedIndex];

        var master = new MixerChannelViewModel(true, "Volume", null, null, device.MMDevice);
        try { master.SyncVolumeFromAudio(device.MMDevice.AudioEndpointVolume.MasterVolumeLevelScalar); }
        catch { }
        try { master.SyncMuteFromAudio(device.MMDevice.AudioEndpointVolume.Mute); }
        catch { }
        master.IsHotkeyTarget = currentConfig.ControlDeviceMasterVolume;
        MasterMixerRows.Add(master);

        var sessionRows = new List<(string name, uint pid, AudioApp app, AudioSessionControl session, bool isSystemSounds)>();

        foreach (var app in device.AudioApps)
        {
            foreach (var session in app.Sessions)
            {
                bool isSys = false;
                try { isSys = session.IsSystemSoundsSession; } catch { }

                uint pid = 0;
                try { pid = session.GetProcessID; } catch { }

                sessionRows.Add((app.AppFriendlyName, pid, app, session, isSys));
            }
        }

        sessionRows.Sort((a, b) =>
        {
            if (a.isSystemSounds != b.isSystemSounds)
                return a.isSystemSounds ? -1 : 1;
            int cmp = string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            return cmp != 0 ? cmp : a.pid.CompareTo(b.pid);
        });

        var groups = sessionRows.GroupBy(r => (r.name, r.pid)).ToList();

        var duplicateNames = groups
            .GroupBy(g => g.Key.name, StringComparer.OrdinalIgnoreCase)
            .Where(ng => ng.Count() > 1)
            .Select(ng => ng.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<MixerChannelViewModel>();
        foreach (var group in groups)
        {
            var first = group.First();
            var sessions = group.Select(g => g.session).ToList();
            var icon = ProcessIconHelper.TryGetIconForSession(first.session);

            string? detail = duplicateNames.Contains(first.name) && first.pid > 0
                ? $"PID {first.pid}"
                : null;

            var ch = new MixerChannelViewModel(false, first.name, icon, first.app, null, detailText: detail, sessionsForRow: sessions);
            ch.IsHotkeyTarget = Audio.AudioAppMatchesConfigOnDevice(first.app, currentConfig);

            try { ch.SyncVolumeFromAudio(sessions[0].SimpleAudioVolume.Volume); }
            catch { }
            SyncMixerMuteFromAudio(ch);

            rows.Add(ch);
        }

        // Keep the configured target visible even when it has no active session. This gives the profile a
        // persistent visual anchor while the rest of the list continues to reflect currently playing apps.
        if (!currentConfig.ControlDeviceMasterVolume &&
            currentConfig.HasTarget &&
            !rows.Any(r => r.IsHotkeyTarget))
        {
            string targetName = string.IsNullOrWhiteSpace(currentConfig.AppFriendlyName)
                ? currentConfig.AppFileName
                : currentConfig.AppFriendlyName;
            var targetApp = new AudioApp
            {
                DeviceId = device.MMDevice.ID,
                AppFileName = currentConfig.AppFileName,
                AppFriendlyName = targetName
            };
            var target = new MixerChannelViewModel(
                false,
                targetName,
                ProcessIconHelper.TryGetIconByFriendlyName(audioApps, currentConfig.AppFriendlyName),
                targetApp,
                null,
                detailText: "Not currently active on this output.",
                sessionsForRow: new(),
                targetUnavailable: true);
            target.IsHotkeyTarget = true;
            AppMixerRows.Add(target);
        }

        // The profile's current target always floats to the top so the "what am I controlling" answer is
        // the first thing in the Apps list; everything else keeps the System Sounds-first alphabetical order.
        foreach (var ch in rows.Where(r => r.IsHotkeyTarget))
            AppMixerRows.Add(ch);
        foreach (var ch in rows.Where(r => !r.IsHotkeyTarget))
            AppMixerRows.Add(ch);
    }

    void RefreshMixerVolumesFromAudio()
    {
        if (suspendMixerVolumeEvents)
            return;
        suspendMixerVolumeEvents = true;
        try
        {
            long now = DateTime.UtcNow.Ticks;

            foreach (var ch in MasterMixerRows)
            {
                if (ch.IsMaster && ch.Device != null && (now - ch.LastUserInteractionTicks) > SliderCooldownTicks)
                {
                    try { ch.SyncVolumeFromAudio(ch.Device.AudioEndpointVolume.MasterVolumeLevelScalar); }
                    catch { }
                }

                SyncMixerMuteFromAudio(ch);
            }

            foreach (var ch in AppMixerRows)
            {
                if ((now - ch.LastUserInteractionTicks) > SliderCooldownTicks)
                {
                    if (ch.SessionsForRow.Count > 0)
                    {
                        try { ch.SyncVolumeFromAudio(ch.SessionsForRow[0].SimpleAudioVolume.Volume); }
                        catch { }
                    }
                    else if (ch.App != null && ch.App.Sessions.Count > 0)
                    {
                        try { ch.SyncVolumeFromAudio(ch.App.Sessions[0].SimpleAudioVolume.Volume); }
                        catch { }
                    }
                }

                SyncMixerMuteFromAudio(ch);
            }
        }
        finally { suspendMixerVolumeEvents = false; }
    }

    void MixerSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suspendMixerVolumeEvents)
            return;
        if (sender is not Slider slider || slider.DataContext is not MixerChannelViewModel ch)
            return;

        ch.LastUserInteractionTicks = DateTime.UtcNow.Ticks;
        float v = (float)Math.Clamp(e.NewValue / 100.0, 0, 1);
        ApplyMixerChannelVolume(ch, v);
    }

    void MixerMuteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not MixerChannelViewModel ch)
            return;

        if (ch.IsMaster)
        {
            if (ch.Device == null)
                return;

            try
            {
                var endpoint = ch.Device.AudioEndpointVolume;
                endpoint.Mute = !endpoint.Mute;
                ch.SyncMuteFromAudio(endpoint.Mute);
            }
            catch { }
            return;
        }

        var sessions = GetMixerRowSessions(ch);
        if (sessions.Count == 0)
            return;

        bool anyUnmuted = false;
        foreach (var session in sessions)
        {
            try
            {
                if (!session.SimpleAudioVolume.Mute)
                    anyUnmuted = true;
            }
            catch
            {
                // Avoid an inconsistent target if any represented session cannot be inspected.
                return;
            }
        }

        bool mute = anyUnmuted;
        foreach (var session in sessions)
        {
            try { session.SimpleAudioVolume.Mute = mute; }
            catch { }
        }

        SyncMixerMuteFromAudio(ch);
    }

    static IReadOnlyList<AudioSessionControl> GetMixerRowSessions(MixerChannelViewModel ch)
    {
        if (ch.SessionsForRow.Count > 0)
            return ch.SessionsForRow;
        if (ch.App != null)
            return ch.App.Sessions;
        return Array.Empty<AudioSessionControl>();
    }

    static void SyncMixerMuteFromAudio(MixerChannelViewModel ch)
    {
        if (ch.IsMaster)
        {
            if (ch.Device == null)
                return;

            try { ch.SyncMuteFromAudio(ch.Device.AudioEndpointVolume.Mute); }
            catch { }
            return;
        }

        var sessions = GetMixerRowSessions(ch);
        if (sessions.Count == 0)
            return;

        bool allMuted = true;
        foreach (var session in sessions)
        {
            try
            {
                if (!session.SimpleAudioVolume.Mute)
                    allMuted = false;
            }
            catch
            {
                // Keep the last known state when a session disappears during a refresh.
                return;
            }
        }

        ch.SyncMuteFromAudio(allMuted);
    }

    static void ApplyMixerChannelVolume(MixerChannelViewModel ch, float scalar)
    {
        if (ch.IsMaster && ch.Device != null)
        {
            Audio.TrySetEndpointMasterVolumeScalar(ch.Device, scalar);
        }
        else if (ch.SessionsForRow.Count > 0)
        {
            foreach (var s in ch.SessionsForRow)
            {
                try { s.SimpleAudioVolume.Volume = scalar; }
                catch { }
            }
        }
        else if (ch.App != null)
        {
            Audio.SetSessionsVolumeScalar(ch.App.Sessions, scalar);
        }
    }

    void MixerTargetRadio_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not MixerChannelViewModel ch || ch.App == null)
            return;

        currentConfig.ControlDeviceMasterVolume = false;
        currentConfig.AppFileName = ch.App.AppFileName;
        currentConfig.AppFriendlyName = ch.App.AppFriendlyName;
        currentConfig.ProcessIndex = 0;
        currentConfig.SaveConfig();
        RebuildSessionCache();
        UpdateTargetAppDisplay();
        PopulateProcessControls();
        UpdateConfigComboItemAtSelectedIndex();
        RebuildMixerStrip();
        RecomputeMixerHotkeyHighlights();
    }

    void RecomputeMixerHotkeyHighlights()
    {
        foreach (var ch in MasterMixerRows)
            ch.IsHotkeyTarget = currentConfig.ControlDeviceMasterVolume;

        foreach (var ch in AppMixerRows)
            ch.IsHotkeyTarget = ch.IsTargetUnavailable ||
                                (ch.App != null && Audio.AudioAppMatchesConfigOnDevice(ch.App, currentConfig));
    }

    // ──────────────────── Config / form ────────────────────

    public void LoadConfigToForm()
    {
        PopulateAudioDevices();
        UpdateTargetAppDisplay();
        PopulateHotkeys();
        PopulateProcessControls();
        ApplyVolumeTargetUi();
        RebuildMixerStrip();
        RecomputeMixerHotkeyHighlights();
    }

    string? GetDeviceFriendlyNameForConfig(Config config)
    {
        var device = audioDevices.FirstOrDefault(d => d.MMDevice.ID == config.DeviceId);
        return device?.MMDevice.FriendlyName;
    }

    string GetConfigListDisplayName(Config config)
    {
        if (!string.IsNullOrWhiteSpace(config.CustomDisplayName))
            return config.CustomDisplayName.Trim();
        return config.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(config));
    }

    void RebuildSessionCache()
    {
        var next = new Dictionary<Guid, List<AudioSessionControl>>();
        foreach (var config in configs)
        {
            if (config.ControlDeviceMasterVolume || !config.HasTarget)
                next[config.ConfigId] = new List<AudioSessionControl>();
            else
            {
                var entries = CollectAllMatchingSessionsAcrossDevices(config);
                next[config.ConfigId] = entries.Select(e => e.Item1).ToList();
            }
        }
        cachedSessionsByConfig = next;
    }

    List<AudioSessionControl> GetCachedSessions(Config config) =>
        cachedSessionsByConfig.TryGetValue(config.ConfigId, out var list) ? list : new List<AudioSessionControl>();

    List<(AudioSessionControl session, string deviceName)> CollectAllMatchingSessionsAcrossDevices(Config config)
    {
        if (config.ControlDeviceMasterVolume || !config.HasTarget)
            return new List<(AudioSessionControl session, string deviceName)>();

        var result = new List<(AudioSessionControl, string)>();
        foreach (var ad in audioDevices)
        {
            var sessions = Audio.CollectSessionsForConfig(ad.MMDevice, config);
            foreach (var s in sessions)
                result.Add((s, ad.MMDevice.FriendlyName));
        }

        result.Sort((a, b) =>
        {
            int c = a.Item1.GetProcessID.CompareTo(b.Item1.GetProcessID);
            return c != 0
                ? c
                : string.CompareOrdinal(a.Item1.GetSessionInstanceIdentifier ?? "", b.Item1.GetSessionInstanceIdentifier ?? "");
        });
        return result;
    }

    static string FormatSessionPickLine(int index, AudioSessionControl s, string deviceName)
    {
        uint pid = s.GetProcessID;
        return $"#{index} — PID {pid} — {deviceName}";
    }

    void RefreshSessionPickerFromAudio()
    {
        if (currentConfig.ControlDeviceMasterVolume || !currentConfig.HasTarget)
        {
            suspendSessionPickerEvents = true;
            try
            {
                ComboBoxAudioSession.Items.Clear();
                ComboBoxAudioSession.IsEnabled = false;
            }
            finally { suspendSessionPickerEvents = false; }
            return;
        }

        var entries = CollectAllMatchingSessionsAcrossDevices(currentConfig);
        suspendSessionPickerEvents = true;
        try
        {
            ComboBoxAudioSession.Items.Clear();
            if (entries.Count == 0)
            {
                ComboBoxAudioSession.IsEnabled = false;
                if (currentConfig.ControlSingleSession)
                    currentConfig.ProcessIndex = 0;
                return;
            }

            int maxIndex = entries.Count - 1;
            int clamped = Math.Clamp(currentConfig.ProcessIndex, 0, maxIndex);
            if (clamped != currentConfig.ProcessIndex)
            {
                currentConfig.ProcessIndex = clamped;
                currentConfig.SaveConfig();
            }

            for (int i = 0; i < entries.Count; i++)
                ComboBoxAudioSession.Items.Add(FormatSessionPickLine(i, entries[i].session, entries[i].deviceName));

            ComboBoxAudioSession.SelectedIndex = clamped;
            ComboBoxAudioSession.IsEnabled = currentConfig.ControlSingleSession;
        }
        finally { suspendSessionPickerEvents = false; }
    }

    /// <summary>
    /// Windows can hand out a new endpoint ID for the same physical device (driver update, USB re-enumeration).
    /// Re-bind any profile whose stored ID is gone but whose stored device friendly name still exists.
    /// Returns true if any config changed.
    /// </summary>
    bool ReconcileConfigDevices()
    {
        if (audioDevices.Length == 0 || configs.Length == 0)
            return false;

        var live = audioDevices
            .Select(d => Audio.TryGetDeviceIdentity(d.MMDevice))
            .Where(d => d != null)
            .Select(d => d!)
            .ToList();
        if (live.Count == 0)
            return false;

        DeviceIdentity? fallback = null;
        var rebound = new List<(string profile, string device)>();

        bool anyChanged = false;
        foreach (var config in configs)
        {
            try
            {
                var result = config.TryReconcileDevice(live);
                switch (result)
                {
                    case DeviceReconcileResult.Backfilled:
                    case DeviceReconcileResult.Rematched:
                        config.SaveConfig();
                        anyChanged = true;
                        break;

                    case DeviceReconcileResult.Orphaned:
                        // Nothing to match on (profile saved by an older build, device gone). Rather than leave a
                        // dead profile behind a modal, bind it to the default output and tell the user via InfoBar.
                        fallback ??= PickFallbackDevice(live);
                        config.SetDevice(fallback.Id, fallback.FriendlyName, fallback.Description);
                        config.SaveConfig();
                        rebound.Add((GetConfigListDisplayName(config), fallback.FriendlyName));
                        anyChanged = true;
                        break;
                }
            }
            catch { }
        }

        if (rebound.Count > 0)
            _pendingRebindNotice = BuildRebindNotice(rebound);

        return anyChanged;
    }

    /// <summary>Default multimedia render endpoint if it's in the live list, otherwise the first live device.</summary>
    static DeviceIdentity PickFallbackDevice(List<DeviceIdentity> live)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var match = live.FirstOrDefault(d => string.Equals(d.Id, def.ID, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        catch { }
        return live[0];
    }

    static string BuildRebindNotice(List<(string profile, string device)> rebound)
    {
        var byDevice = rebound.GroupBy(r => r.device).ToList();
        var sb = new StringBuilder();
        foreach (var g in byDevice)
        {
            var names = g.Select(r => r.profile).Distinct().ToList();
            string list = names.Count switch
            {
                1 => names[0],
                2 => $"{names[0]} and {names[1]}",
                _ => string.Join(", ", names.Take(names.Count - 1)) + $", and {names[^1]}"
            };
            string verb = names.Count == 1 ? "was" : "were";
            sb.Append($"{list} {verb} re-bound to {g.Key} because the original output device no longer exists. ");
        }
        sb.Append("If that's wrong, select the profile and pick a different Output device.");
        return sb.ToString();
    }

    string? _pendingRebindNotice;

    /// <summary>Shows any queued auto-rebind message in the non-blocking InfoBar at the top of the window.</summary>
    void ShowDeviceRebindNoticeIfNeeded()
    {
        if (UiGoldenCapture.Enabled || string.IsNullOrEmpty(_pendingRebindNotice))
            return;

        InfoBarDeviceRebind.Title = "Profiles re-bound to a different output device";
        InfoBarDeviceRebind.Message = _pendingRebindNotice;
        InfoBarDeviceRebind.IsOpen = true;
        _pendingRebindNotice = null;
    }

    async void ShowConfigLoadErrorIfNeeded()
    {
        if (Configurations.LastLoadError is { } err)
            await ShowMessageAsync(err, "Configuration Load Error");
    }

    async Task ShowMessageAsync(string message, string title)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 400
            },
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    // ──────────────────── Profile name ────────────────────

    void UpdateConfigComboItemAtSelectedIndex()
    {
        int i = ComboBoxConfig.SelectedIndex;
        if (i >= 0 && i < configs.Length)
        {
            ComboBoxConfig.Items[i] = GetConfigListDisplayName(configs[i]);
            ComboBoxConfig.SelectedIndex = i;
        }
    }

    void RefreshAllConfigComboItemTexts()
    {
        int sel = ComboBoxConfig.SelectedIndex;
        for (int i = 0; i < configs.Length && i < ComboBoxConfig.Items.Count; i++)
            ComboBoxConfig.Items[i] = GetConfigListDisplayName(configs[i]);
        if (sel >= 0 && sel < ComboBoxConfig.Items.Count)
            ComboBoxConfig.SelectedIndex = sel;
    }

    async void ButtonRenameConfig_OnClick(object sender, RoutedEventArgs e)
    {
        if (UiGoldenCapture.Enabled || ComboBoxConfig.SelectedIndex < 0)
            return;

        var textBox = new TextBox
        {
            PlaceholderText = currentConfig.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(currentConfig)),
            Text = currentConfig.CustomDisplayName ?? ""
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Give this profile a custom name. Leave it blank to use the automatic name.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(textBox);

        var dialog = new ContentDialog
        {
            Title = "Rename profile",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        string trimmed = textBox.Text.Trim();
        string auto = currentConfig.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(currentConfig));
        if (string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, auto, StringComparison.OrdinalIgnoreCase))
            currentConfig.CustomDisplayName = null;
        else
            currentConfig.CustomDisplayName = trimmed;

        currentConfig.SaveConfig();
        UpdateConfigComboItemAtSelectedIndex();
    }

    // ──────────────────── Hotkeys ────────────────────

    public void UpdateHotkeysToListenFor() =>
        hotkeysToListenFor = configs.SelectMany(config => config.Hotkeys).Distinct().ToArray();

    public void UpdateHotkeysHeld(int virtualKey, bool keyUp)
    {
        if (hotkeysToListenFor.Contains(virtualKey) && !keyUp && !hotkeysHeld.Contains(virtualKey))
        {
            hotkeysHeld = hotkeysHeld.Append(virtualKey).ToArray();
            RefreshAudioDevicesAndApps();
        }
        else if (keyUp)
        {
            hotkeysHeld = hotkeysHeld.Where(key => key != virtualKey).ToArray();
        }
    }

    void PopulateConfigs(int selectedIndex)
    {
        ComboBoxConfig.Items.Clear();
        foreach (var config in configs)
            ComboBoxConfig.Items.Add(GetConfigListDisplayName(config));

        int idx = Math.Clamp(selectedIndex, 0, Math.Max(0, configs.Length - 1));
        currentConfig = configs[idx];
        ComboBoxConfig.SelectedIndex = idx;
    }

    void PopulateAudioDevices()
    {
        suspendDeviceComboEvents = true;
        try
        {
            if (UiGoldenCapture.Enabled && UiGoldenCapture.UseMockMixer)
            {
                ComboBoxDevice.Items.Clear();
                ComboBoxDevice.Items.Add("Speakers (Realtek High Definition Audio)");
                ComboBoxDevice.SelectedIndex = 0;
                return;
            }

            ComboBoxDevice.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < audioDevices.Length; i++)
            {
                var device = audioDevices[i];
                ComboBoxDevice.Items.Add(device.MMDevice.FriendlyName);
                if (device.MMDevice.ID == currentConfig.DeviceId)
                    selectedIndex = i;
            }

            if (ComboBoxDevice.Items.Count > 0)
                ComboBoxDevice.SelectedIndex = selectedIndex;
        }
        finally { suspendDeviceComboEvents = false; }
    }

    void UpdateTargetAppDisplay()
    {
        if (currentConfig.ControlDeviceMasterVolume)
        {
            TextAppTarget.Text = "Device master volume";
            ImageAppTarget.Source = null;
            IconDeviceMasterTarget.Visibility = Visibility.Visible;
        }
        else if (!currentConfig.HasTarget)
        {
            TextAppTarget.Text = "Choose an app or device master volume";
            ImageAppTarget.Source = null;
            IconDeviceMasterTarget.Visibility = Visibility.Collapsed;
        }
        else
        {
            TextAppTarget.Text = string.IsNullOrWhiteSpace(currentConfig.AppFriendlyName)
                ? currentConfig.AppFileName
                : currentConfig.AppFriendlyName;
            ImageAppTarget.Source = ProcessIconHelper.TryGetIconByFriendlyName(audioApps, currentConfig.AppFriendlyName);
            IconDeviceMasterTarget.Visibility = Visibility.Collapsed;
        }
    }

    void PopulateHotkeys()
    {
        TextBoxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(KeyDisplayNames.GetDisplayName));
        ButtonHotkeyAdd.IsEnabled = currentConfig.HasTarget;
        ButtonHotkeyReset.IsEnabled = currentConfig.HasTarget || currentConfig.Hotkeys.Length > 0;
    }

    void PopulateProcessControls()
    {
        CheckBoxSingleSession.IsChecked = currentConfig.ControlSingleSession;
        RefreshSessionPickerFromAudio();
    }

    void ApplyVolumeTargetUi()
    {
        bool hasTarget = currentConfig.HasTarget;
        bool appMode = hasTarget && !currentConfig.ControlDeviceMasterVolume;
        CheckBoxSingleSession.IsEnabled = appMode;
        LabelSession.Opacity = appMode ? 1 : 0.45;
        ButtonHotkeyAdd.IsEnabled = hasTarget;
        ButtonHotkeyReset.IsEnabled = hasTarget || currentConfig.Hotkeys.Length > 0;

        if (!appMode)
        {
            suspendSessionPickerEvents = true;
            try
            {
                ComboBoxAudioSession.Items.Clear();
                ComboBoxAudioSession.IsEnabled = false;
            }
            finally { suspendSessionPickerEvents = false; }
        }
        else
        {
            RefreshSessionPickerFromAudio();
            ComboBoxAudioSession.IsEnabled = currentConfig.ControlSingleSession;
        }
    }

    void ApplyDeviceMasterChange()
    {
        currentConfig.SaveConfig();
        RebuildSessionCache();
        UpdateTargetAppDisplay();
        ApplyVolumeTargetUi();
        UpdateConfigComboItemAtSelectedIndex();
        RebuildMixerStrip();
        RecomputeMixerHotkeyHighlights();
    }

    void ComboBoxConfig_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboBoxConfig.SelectedIndex < 0 || ComboBoxConfig.SelectedIndex >= configs.Length)
            return;
        var next = configs[ComboBoxConfig.SelectedIndex];
        if (ReferenceEquals(next, currentConfig))
            return;
        currentConfig = next;
        LoadConfigToForm();
    }

    void ComboBoxDevice_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suspendDeviceComboEvents)
            return;
        if (ComboBoxDevice.SelectedIndex < 0 || ComboBoxDevice.SelectedIndex >= audioDevices.Length)
            return;
        var picked = audioDevices[ComboBoxDevice.SelectedIndex].MMDevice;
        currentConfig.SetDevice(picked.ID, picked.FriendlyName, Audio.TryGetDeviceDescription(picked));
        currentConfig.SaveConfig();
        RefreshSessionPickerFromAudio();
        UpdateConfigComboItemAtSelectedIndex();
        RebuildMixerStrip();
        RecomputeMixerHotkeyHighlights();
    }

    void ComboBoxAudioSession_OnDropDownOpened(object sender, object e) => RefreshSessionPickerFromAudio();

    void ButtonHotkeyAdd_OnClick(object sender, RoutedEventArgs e)
    {
        ButtonHotkeyAdd.Content = "Listening…";
        ButtonHotkeyAdd.IsEnabled = false;
        listeningForHotkeyAdd = true;
    }

    void ButtonHotkeyReset_OnClick(object sender, RoutedEventArgs e)
    {
        currentConfig.Hotkeys = Array.Empty<int>();
        currentConfig.SaveConfig();
        UpdateHotkeysToListenFor();
        PopulateHotkeys();
        UpdateConfigComboItemAtSelectedIndex();
    }

    void AddHotkey(int virtualKey)
    {
        listeningForHotkeyAdd = false;
        DispatcherQueue.TryEnqueue(() =>
        {
            ButtonHotkeyAdd.IsEnabled = true;
            ButtonHotkeyAdd.Content = "Add";
        });

        if (!currentConfig.Hotkeys.Contains(virtualKey))
        {
            currentConfig.Hotkeys = currentConfig.Hotkeys.Append(virtualKey).ToArray();
            DispatcherQueue.TryEnqueue(() =>
            {
                TextBoxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(KeyDisplayNames.GetDisplayName));
            });
            currentConfig.SaveConfig();
            UpdateHotkeysToListenFor();
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateConfigComboItemAtSelectedIndex();
            });
        }
        else
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                await ShowMessageAsync("This hotkey is already added.", "Duplicate hotkey");
            });
        }
    }

    void CheckBoxSingleSession_OnChecked(object sender, RoutedEventArgs e) => OnSingleSessionChanged();
    void CheckBoxSingleSession_OnUnchecked(object sender, RoutedEventArgs e) => OnSingleSessionChanged();

    void OnSingleSessionChanged()
    {
        currentConfig.ControlSingleSession = CheckBoxSingleSession.IsChecked == true;
        RefreshSessionPickerFromAudio();
        currentConfig.SaveConfig();
    }

    void ComboBoxAudioSession_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suspendSessionPickerEvents || ComboBoxAudioSession.SelectedIndex < 0)
            return;

        var entries = CollectAllMatchingSessionsAcrossDevices(currentConfig);
        if (entries.Count == 0)
            return;

        int idx = Math.Clamp(ComboBoxAudioSession.SelectedIndex, 0, entries.Count - 1);
        currentConfig.ProcessIndex = idx;
        currentConfig.SaveConfig();
    }

    void ButtonNewConfig_OnClick(object sender, RoutedEventArgs e)
    {
        var device = GetPreferredAudioDevice();
        if (device == null)
            return;

        var newConfig = new Config
        {
            ConfigId = Guid.NewGuid(),
            DeviceId = device.MMDevice.ID,
            DeviceFriendlyName = device.MMDevice.FriendlyName,
            DeviceDescription = Audio.TryGetDeviceDescription(device.MMDevice),
            AppFileName = "",
            AppFriendlyName = "",
            Hotkeys = Array.Empty<int>(),
            ControlSingleSession = false,
            ProcessIndex = 0,
            ControlDeviceMasterVolume = false
        };

        configs = configs.Append(newConfig).ToArray();
        newConfig.SaveConfig();
        PopulateConfigs(configs.Length - 1);
        LoadConfigToForm();
    }

    AudioDevice? GetPreferredAudioDevice()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var matching = audioDevices.FirstOrDefault(d =>
                string.Equals(d.MMDevice.ID, defaultDevice.ID, StringComparison.OrdinalIgnoreCase));
            if (matching != null)
                return matching;
        }
        catch { }

        return audioDevices.FirstOrDefault();
    }

    async void ButtonDeleteConfig_OnClick(object sender, RoutedEventArgs e)
    {
        if (configs.Length == 1)
        {
            await ShowMessageAsync("Cannot delete the only remaining configuration.", "KBMixer");
            return;
        }

        int selectedIndex = ComboBoxConfig.SelectedIndex;
        var configToDelete = configs[selectedIndex];
        configs = configs.Where((_, index) => index != selectedIndex).ToArray();
        configToDelete.DeleteConfig();
        int nextIndex = Math.Min(selectedIndex, configs.Length - 1);
        PopulateConfigs(nextIndex);
        LoadConfigToForm();
    }

    void RefreshAudioDevicesAndApps()
    {
        audioDevices = Audio.GetAudioDevices();
        var audioAppsList = new List<AudioApp>();
        foreach (var device in audioDevices)
            audioAppsList.AddRange(Audio.GetAudioDeviceApps(device.MMDevice));
        audioApps = audioAppsList.ToArray();

        // A device that was missing at startup may have come back under a new ID.
        ReconcileConfigDevices();
        if (_pendingRebindNotice != null)
            DispatcherQueue.TryEnqueue(ShowDeviceRebindNoticeIfNeeded);

        RebuildSessionCache();
        SnapshotSessionCount();
        DispatcherQueue.TryEnqueue(() =>
        {
            PopulateAudioDevices();
            PopulateProcessControls();
            ApplyVolumeTargetUi();
            RefreshAllConfigComboItemTexts();
            RebuildMixerStrip();
            RecomputeMixerHotkeyHighlights();
        });
    }

    async void ButtonAppChoose_OnClick(object sender, RoutedEventArgs e) => await OpenAppSelectionDialogAsync();

    async Task OpenAppSelectionDialogAsync()
    {
        RefreshAudioDevicesAndApps();
        await Task.Delay(100);

        var result = await AppSelectionDialog.ShowAsync(
            Content.XamlRoot, audioApps, currentConfig.AppFriendlyName, currentConfig.ControlDeviceMasterVolume);
        if (result is null)
            return;

        if (result == AppSelectionDialog.NoTargetResult)
        {
            currentConfig.ControlDeviceMasterVolume = false;
            currentConfig.AppFileName = "";
            currentConfig.AppFriendlyName = "";
            currentConfig.Hotkeys = Array.Empty<int>();
            currentConfig.ControlSingleSession = false;
            currentConfig.ProcessIndex = 0;
            ApplyDeviceMasterChange();
            UpdateHotkeysToListenFor();
            PopulateHotkeys();
            return;
        }

        if (result == AppSelectionDialog.DeviceMasterResult)
        {
            currentConfig.ControlDeviceMasterVolume = true;
            ApplyDeviceMasterChange();
            return;
        }

        currentConfig.ControlDeviceMasterVolume = false;

        var matchingApp = audioApps
            .Where(app => string.Equals(app.DeviceId, currentConfig.DeviceId, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(app => !string.IsNullOrWhiteSpace(app.AppFriendlyName) && app.AppFriendlyName.Equals(result, StringComparison.OrdinalIgnoreCase));
        matchingApp ??= audioApps.FirstOrDefault(app =>
            !string.IsNullOrWhiteSpace(app.AppFriendlyName) && app.AppFriendlyName.Equals(result, StringComparison.OrdinalIgnoreCase));

        string selectedAppFileName;
        string actualFriendlyName;

        if (matchingApp != null)
        {
            selectedAppFileName = matchingApp.AppFileName;
            actualFriendlyName = matchingApp.AppFriendlyName;
        }
        else
        {
            matchingApp = audioApps
                .Where(app => string.Equals(app.DeviceId, currentConfig.DeviceId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(app => !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName.Equals(result, StringComparison.OrdinalIgnoreCase));
            matchingApp ??= audioApps.FirstOrDefault(app =>
                !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName.Equals(result, StringComparison.OrdinalIgnoreCase));

            if (matchingApp != null)
            {
                selectedAppFileName = matchingApp.AppFileName;
                actualFriendlyName = matchingApp.AppFriendlyName;
            }
            else
            {
                selectedAppFileName = result;
                actualFriendlyName = result;
            }
        }

        currentConfig.AppFileName = selectedAppFileName;
        currentConfig.AppFriendlyName = actualFriendlyName;
        currentConfig.ProcessIndex = 0;
        ApplyDeviceMasterChange();
        PopulateProcessControls();
    }

    // ──────────────────── Help & Settings ────────────────────

    async void ButtonHelp_OnClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 10 };
        var steps = new[]
        {
            "Pick a profile, device, and hotkeys (click Add, then press keys).",
            "Choose desired app to control or device master volume.",
            "Hold your hotkeys and scroll the mouse wheel to change volume anywhere.",
            "Keep KBMixer running; minimize to the notification area."
        };
        for (int i = 0; i < steps.Length; i++)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"{i + 1}. {steps[i]}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }

        var dialog = new ContentDialog
        {
            Title = "How KBMixer works",
            Content = content,
            CloseButtonText = "Got it",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    async void ButtonSettings_OnClick(object sender, RoutedEventArgs e)
    {
        var checkBox = new CheckBox
        {
            Content = "Open KBMixer when Windows starts (minimized to the notification area)"
        };

        try { checkBox.IsChecked = StartupRegistration.IsRegisteredForCurrentExe(); }
        catch { }

        var versionText = new TextBlock
        {
            Text = $"KBMixer {typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "?"}",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        var checkUpdatesButton = new Button { Content = "Check for updates" };

        var updateRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        updateRow.Children.Add(checkUpdatesButton);
        updateRow.Children.Add(versionText);

        var settingsPanel = new StackPanel { Spacing = 16 };
        settingsPanel.Children.Add(checkBox);
        settingsPanel.Children.Add(updateRow);

        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = settingsPanel,
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };

        checkUpdatesButton.Click += async (_, _) =>
        {
            // Only one ContentDialog can be open at a time in WinUI; close Settings before prompting.
            dialog.Hide();
            await UpdateChecker.CheckAndPromptAsync(Content.XamlRoot, silentIfUpToDate: false);
        };

        checkBox.Checked += async (_, _) =>
        {
            try { StartupRegistration.SetRegistered(true); }
            catch (Exception ex) { await ShowSettingsErrorAsync(checkBox, ex); }
        };
        checkBox.Unchecked += async (_, _) =>
        {
            try { StartupRegistration.SetRegistered(false); }
            catch (Exception ex) { await ShowSettingsErrorAsync(checkBox, ex); }
        };

        await dialog.ShowAsync();
    }

    async Task ShowSettingsErrorAsync(CheckBox cb, Exception ex)
    {
        cb.IsChecked = !cb.IsChecked;
        await ShowMessageAsync($"Could not update the Windows startup setting.\n\n{ex.Message}", "KBMixer");
    }
}
