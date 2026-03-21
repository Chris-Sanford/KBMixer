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

                ShowMissingAudioDevicesWarningIfNeeded();
                ShowConfigLoadErrorIfNeeded();
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
                        .Where(config => config.Hotkeys.SequenceEqual(hotkeysHeld))
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

    void OnRefreshTimerTick()
    {
        RefreshMixerVolumesFromAudio();

        if (++_refreshTickCount % 25 == 0)
            CheckForSessionListChanges();
    }

    void SnapshotSessionCount()
    {
        int count = 0;
        foreach (var d in audioDevices)
            foreach (var a in d.AudioApps)
                count += a.Sessions.Count;
        _lastKnownSessionCount = count;
    }

    void CheckForSessionListChanges()
    {
        try
        {
            int count = 0;
            foreach (var d in audioDevices)
            {
                try
                {
                    var sessions = d.MMDevice.AudioSessionManager.Sessions;
                    count += sessions.Count;
                }
                catch { }
            }

            if (count != _lastKnownSessionCount)
            {
                _lastKnownSessionCount = count;
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

            AppMixerRows.Add(ch);
        }
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
            }

            foreach (var ch in AppMixerRows)
            {
                if ((now - ch.LastUserInteractionTicks) <= SliderCooldownTicks)
                    continue;

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

    void MixerUseForProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not MixerChannelViewModel ch || ch.App == null)
            return;

        currentConfig.AppFileName = ch.App.AppFileName;
        currentConfig.AppFriendlyName = ch.App.AppFriendlyName;
        currentConfig.ProcessIndex = 0;
        currentConfig.SaveConfig();
        UpdateTargetAppDisplay();
        PopulateProcessControls();
        UpdateConfigComboItemAtSelectedIndex();
        UpdateConfigDisplayNamePlaceholder();
        RecomputeMixerHotkeyHighlights();
    }

    void RecomputeMixerHotkeyHighlights()
    {
        foreach (var ch in MasterMixerRows)
            ch.IsHotkeyTarget = currentConfig.ControlDeviceMasterVolume;

        foreach (var ch in AppMixerRows)
            ch.IsHotkeyTarget = ch.App != null && Audio.AudioAppMatchesConfigOnDevice(ch.App, currentConfig);
    }

    // ──────────────────── Config / form ────────────────────

    public void LoadConfigToForm()
    {
        PopulateAudioDevices();
        UpdateTargetAppDisplay();
        PopulateHotkeys();
        PopulateProcessControls();
        PopulateConfigDisplayNameControl();
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
            if (config.ControlDeviceMasterVolume)
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
        if (currentConfig.ControlDeviceMasterVolume)
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

    async void ShowMissingAudioDevicesWarningIfNeeded()
    {
        if (UiGoldenCapture.Enabled || configs.Length == 0)
            return;

        var validIds = new HashSet<string>(
            audioDevices.Select(d => d.MMDevice.ID),
            StringComparer.OrdinalIgnoreCase);

        var missing = configs.Where(c => !validIds.Contains(c.DeviceId)).ToList();
        if (missing.Count == 0)
            return;

        var message = new StringBuilder();
        message.AppendLine("Some configurations reference an audio output device that is not available (disconnected, disabled, or removed).");
        message.AppendLine("Choose a device from the list for each affected configuration, or reconnect the device.");
        message.AppendLine();

        foreach (var group in missing.GroupBy(c => c.DeviceId))
        {
            message.AppendLine("Device ID:");
            message.AppendLine(group.Key);
            foreach (var c in group)
                message.AppendLine($"  • {GetConfigListDisplayName(c)}");
            message.AppendLine();
        }

        await ShowMessageAsync(message.ToString().TrimEnd(), "Audio device not found");
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

    // ──────────────────── Profile display name ────────────────────

    void PopulateConfigDisplayNameControl()
    {
        TextBoxConfigDisplayName.Text = currentConfig.CustomDisplayName ?? "";
        UpdateConfigDisplayNamePlaceholder();
    }

    void UpdateConfigDisplayNamePlaceholder()
    {
        ToolTipService.SetToolTip(TextBoxConfigDisplayName,
            string.IsNullOrWhiteSpace(currentConfig.CustomDisplayName)
                ? $"Automatic name: {currentConfig.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(currentConfig))}"
                : "Leave blank to use the automatic name (app + hotkeys + device).");
    }

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

    void TextBoxConfigDisplayName_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (UiGoldenCapture.Enabled || ComboBoxConfig.SelectedIndex < 0)
            return;

        string trimmed = TextBoxConfigDisplayName.Text.Trim();
        string auto = currentConfig.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(currentConfig));
        if (string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, auto, StringComparison.OrdinalIgnoreCase))
            currentConfig.CustomDisplayName = null;
        else
            currentConfig.CustomDisplayName = trimmed;

        TextBoxConfigDisplayName.Text = currentConfig.CustomDisplayName ?? "";
        currentConfig.SaveConfig();
        UpdateConfigComboItemAtSelectedIndex();
        UpdateConfigDisplayNamePlaceholder();
    }

    void ButtonResetDisplayName_OnClick(object sender, RoutedEventArgs e)
    {
        currentConfig.CustomDisplayName = null;
        currentConfig.SaveConfig();
        PopulateConfigDisplayNameControl();
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
        }
        else
        {
            TextAppTarget.Text = currentConfig.AppFriendlyName;
            ImageAppTarget.Source = ProcessIconHelper.TryGetIconByFriendlyName(audioApps, currentConfig.AppFriendlyName);
        }
    }

    void PopulateHotkeys() =>
        TextBoxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(KeyDisplayNames.GetDisplayName));

    void PopulateProcessControls()
    {
        CheckBoxSingleSession.IsChecked = currentConfig.ControlSingleSession;
        RefreshSessionPickerFromAudio();
    }

    void ApplyVolumeTargetUi()
    {
        bool appMode = !currentConfig.ControlDeviceMasterVolume;
        CheckBoxSingleSession.IsEnabled = appMode;
        LabelSession.Opacity = appMode ? 1 : 0.45;

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
        UpdateConfigDisplayNamePlaceholder();
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
        currentConfig.DeviceId = audioDevices[ComboBoxDevice.SelectedIndex].MMDevice.ID;
        currentConfig.SaveConfig();
        RefreshSessionPickerFromAudio();
        UpdateConfigComboItemAtSelectedIndex();
        UpdateConfigDisplayNamePlaceholder();
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
        hotkeysToListenFor = Array.Empty<int>();
        TextBoxHotkeys.Text = "";
        currentConfig.Hotkeys = hotkeysToListenFor;
        currentConfig.SaveConfig();
        UpdateConfigComboItemAtSelectedIndex();
        UpdateConfigDisplayNamePlaceholder();
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
                UpdateConfigDisplayNamePlaceholder();
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
        if (audioDevices.Length == 0)
            return;

        var newConfig = new Config
        {
            ConfigId = Guid.NewGuid(),
            DeviceId = audioDevices[0].MMDevice.ID,
            AppFileName = "%b#",
            AppFriendlyName = "System Sounds",
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

        RebuildSessionCache();
        SnapshotSessionCount();
        DispatcherQueue.TryEnqueue(() =>
        {
            PopulateAudioDevices();
            PopulateProcessControls();
            ApplyVolumeTargetUi();
            RefreshAllConfigComboItemTexts();
            UpdateConfigDisplayNamePlaceholder();
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

        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = checkBox,
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
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
