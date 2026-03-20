using Linearstar.Windows.RawInput;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Text;

namespace KBMixer
{
    public partial class Form1 : Form
    {
        private const int WM_INPUT = 0x00FF;
        public const int mouseWheelUp = 120;
        public const int mouseWheelDown = -120;
        public const string up = "Up";
        public const string down = "Down";
        public const string mouseWheelButton = "MouseWheel";

        public AudioDevice[] audioDevices; // Array of Audio Devices to Control
        public AudioApp[] audioApps; // Array of Audio Apps to Control
        public Config[] configs; // Array of Configurations to Load
        public Config currentConfig = null!; // Set during startup (LoadConfigs or New Config)
        public int[] hotkeysToListenFor = Array.Empty<int>(); // Array of Hotkeys to Listen For
        public int[] hotkeysHeld = Array.Empty<int>(); // Array of Hotkeys that are currently being held down
        public bool listeningForHotkeyAdd = false;

        /// <summary>Avoids <see cref="comboBoxAudioSession"/> <c>SelectedIndexChanged</c> while we sync from audio state.</summary>
        private bool suspendSessionPickerEvents;

        /// <summary>Avoids <see cref="checkBoxOpenAtStartup"/> <c>CheckedChanged</c> while we sync from the registry.</summary>
        private bool suspendOpenAtStartupEvents;

        /// <summary>Avoids <see cref="checkBoxDeviceMasterVolume"/> <c>CheckedChanged</c> while loading config into the form.</summary>
        private bool suspendDeviceMasterEvents;

        /// <summary>
        /// Cached session lists per config id, built at startup / refresh / hotkey-down.
        /// Used by <see cref="WndProc"/> so scroll events never re-enumerate WASAPI.
        /// </summary>
        private Dictionary<Guid, List<AudioSessionControl>> cachedSessionsByConfig = new();

        private const int LayoutFieldLeft = 56;
        private const int LayoutMarginRight = 12;
        private const int LayoutFieldToButtonGap = 8;
        private const int LayoutTwinButtonWidth = 64;
        private const int LayoutTwinButtonGap = 6;
        private const int LayoutActionStripWidth = LayoutTwinButtonWidth + LayoutTwinButtonGap + LayoutTwinButtonWidth;

        private const int LayoutUsageMarginX = 8;
        private const int LayoutAfterSessionGap = 14;
        private const int LayoutUsageLabelTop = 22;
        private const int LayoutUsageGroupBottomPad = 14;
        private const int LayoutAfterUsageGroupGap = 14;
        private const int LayoutUsageLabelPadH = 12;
        private const int LayoutBottomMargin = 12;

        public Form1(bool startMinimized = false)
        {
            InitializeComponent();

            this.Resize += new EventHandler(MainForm_Resize);
            trayIcon.Click += new EventHandler(TrayIcon_Click);

            RegisterRawInputDevices();

            // Load all Audio Devices into Memory
            audioDevices = Audio.GetAudioDevices();

            // Load all Audio Apps into Memory
            var audioAppsList = new List<AudioApp>();
            foreach (var device in audioDevices)
            {
                audioAppsList.AddRange(Audio.GetAudioDeviceApps(device.MMDevice));
            }
            audioApps = audioAppsList.ToArray();

            // Get Configs from Disk, if exists
            configs = Configurations.LoadConfigsFromDisk();

            // If there are no configs from disk, create a default config (populates UI)
            if (configs.Length == 0)
            {
                buttonNewConfig_Click(this, EventArgs.Empty);
            }
            else
            {
                currentConfig = configs[0];
                PopulateConfigs(0);
                LoadConfigToForm();
            }

            RebuildSessionCache();

            // Update the array of hotkeys to listen for from all available configs
            UpdateHotkeysToListenFor();

            ShowMissingAudioDevicesWarningIfNeeded();

            ApplyResponsiveLayout();

            suspendOpenAtStartupEvents = true;
            try
            {
                checkBoxOpenAtStartup.Checked = StartupRegistration.IsRegisteredForCurrentExe();
            }
            finally
            {
                suspendOpenAtStartupEvents = false;
            }

            if (startMinimized)
                WindowState = FormWindowState.Minimized;
        }

        private void ApplyResponsiveLayout()
        {
            if (!IsHandleCreated || WindowState == FormWindowState.Minimized)
                return;

            int cw = ClientSize.Width;
            int btnRight = cw - LayoutMarginRight;

            buttonDeleteConfig.Left = btnRight - LayoutTwinButtonWidth;
            buttonNewConfig.Left = btnRight - LayoutActionStripWidth;

            buttonHotkeyReset.Left = btnRight - LayoutTwinButtonWidth;
            buttonHotkeyAdd.Left = btnRight - LayoutActionStripWidth;

            int singleLeft = btnRight - LayoutActionStripWidth;
            buttonResetDisplayName.Left = singleLeft;
            buttonRefreshAudio.Left = singleLeft;
            buttonAppSet.Left = singleLeft;

            int fieldWidth = singleLeft - LayoutFieldToButtonGap - LayoutFieldLeft;
            if (fieldWidth < 180)
                fieldWidth = 180;

            comboBoxConfig.Width = fieldWidth;
            textBoxConfigDisplayName.Width = fieldWidth;
            deviceComboBox.Width = fieldWidth;
            textBoxAppSelected.Width = fieldWidth;
            textboxHotkeys.Width = fieldWidth;

            int sessionPickLeft = checkBoxControlSingleAppProcess.Right + LayoutFieldToButtonGap;
            comboBoxAudioSession.Left = sessionPickLeft;
            comboBoxAudioSession.Top = checkBoxControlSingleAppProcess.Top
                + (checkBoxControlSingleAppProcess.Height - comboBoxAudioSession.Height) / 2;
            comboBoxAudioSession.Width = Math.Max(120, singleLeft - LayoutFieldToButtonGap - sessionPickLeft);
            labelSession.Top = checkBoxControlSingleAppProcess.Top
                + (checkBoxControlSingleAppProcess.Height - labelSession.Height) / 2;

            int sessionBottom = Math.Max(checkBoxControlSingleAppProcess.Bottom, comboBoxAudioSession.Bottom);

            groupBoxHowTo.Left = LayoutUsageMarginX;
            groupBoxHowTo.Top = sessionBottom + LayoutAfterSessionGap;
            groupBoxHowTo.Width = Math.Max(200, cw - 2 * LayoutUsageMarginX);

            int innerW = Math.Max(80, groupBoxHowTo.ClientSize.Width - 2 * LayoutUsageLabelPadH);
            int textH = MeasureWrappedInstructionHeight(labelInstructions.Text, labelInstructions.Font, innerW);
            labelInstructions.Left = LayoutUsageLabelPadH;
            labelInstructions.Top = LayoutUsageLabelTop;
            labelInstructions.Width = innerW;
            labelInstructions.Height = Math.Max(textH + 4, 36);

            groupBoxHowTo.Height = LayoutUsageLabelTop + labelInstructions.Height + LayoutUsageGroupBottomPad;

            checkBoxOpenAtStartup.Left = LayoutFieldLeft;
            checkBoxOpenAtStartup.Top = groupBoxHowTo.Bottom + LayoutAfterUsageGroupGap;

            int minClientH = checkBoxOpenAtStartup.Bottom + LayoutBottomMargin;
            int chrome = Height - ClientSize.Height;
            if (chrome < 1)
                chrome = 39;
            int minOuterH = minClientH + chrome;
            if (minOuterH > MinimumSize.Height)
                MinimumSize = new Size(Math.Max(MinimumSize.Width, 520), minOuterH);

            SyncConfigComboDropDownWidth();
        }

        private static int MeasureWrappedInstructionHeight(string? text, Font font, int maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            const TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix;
            return TextRenderer.MeasureText(text, font, new Size(maxWidth, int.MaxValue), flags).Height;
        }

        void MainForm_Resize(object? sender, EventArgs e)
        {
            ApplyResponsiveLayout();

            if (this.WindowState == FormWindowState.Minimized)
            {
                trayIcon.Visible = true;
                this.Hide();
            }
        }

        void TrayIcon_Click(object? sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            trayIcon.Visible = false;
        }

        public void LoadConfigToForm()
        {
            // Load the current config into the form
            PopulateAudioDevices();
            PopulateAudioAppSelection();
            PopulateHotkeys();
            PopulateProcessControls();
            PopulateConfigDisplayNameControl();
            ApplyVolumeTargetUi();
        }

        private string? GetDeviceFriendlyNameForConfig(Config config)
        {
            var device = audioDevices?.FirstOrDefault(d => d.MMDevice.ID == config.DeviceId);
            return device?.MMDevice.FriendlyName;
        }

        private string GetConfigListDisplayName(Config config)
        {
            if (!string.IsNullOrWhiteSpace(config.CustomDisplayName))
                return config.CustomDisplayName.Trim();
            return config.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(config));
        }

        /// <summary>Rebuilds <see cref="cachedSessionsByConfig"/> for every config.</summary>
        private void RebuildSessionCache()
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

        /// <summary>Returns the cached session list for a config, or an empty list if not cached.</summary>
        private List<AudioSessionControl> GetCachedSessions(Config config)
        {
            return cachedSessionsByConfig.TryGetValue(config.ConfigId, out var list) ? list : new();
        }

        /// <summary>
        /// All sessions for this app across ALL output devices, sorted by PID then session id.
        /// Windows pairs endpoints (e.g. "Hyper-X Game" + "Headset Earphone Chat") so an app like
        /// Discord shows two mixer entries that appear to be on the same device but are actually on
        /// separate WASAPI endpoints. This collects them all so "control all" adjusts every instance
        /// and the session picker can list every selectable target.
        /// </summary>
        private List<(AudioSessionControl session, string deviceName)> CollectAllMatchingSessionsAcrossDevices(Config config)
        {
            var result = new List<(AudioSessionControl, string)>();
            if (audioDevices == null)
                return result;

            foreach (var ad in audioDevices)
            {
                var sessions = Audio.CollectSessionsForConfig(ad.MMDevice, config);
                foreach (var s in sessions)
                    result.Add((s, ad.MMDevice.FriendlyName));
            }

            result.Sort((a, b) =>
            {
                int c = a.Item1.GetProcessID.CompareTo(b.Item1.GetProcessID);
                return c != 0 ? c : string.CompareOrdinal(
                    a.Item1.GetSessionInstanceIdentifier ?? "",
                    b.Item1.GetSessionInstanceIdentifier ?? "");
            });
            return result;
        }

        private static string FormatSessionPickLine(int index, AudioSessionControl s, string deviceName)
        {
            uint pid = s.GetProcessID;
            string devShort = deviceName.Length > 20 ? deviceName[..20] + "…" : deviceName;
            return $"#{index} — PID {pid} — {devShort}";
        }

        private void RefreshSessionPickerFromAudio()
        {
            if (currentConfig.ControlDeviceMasterVolume)
            {
                suspendSessionPickerEvents = true;
                try
                {
                    comboBoxAudioSession.Items.Clear();
                    comboBoxAudioSession.Enabled = false;
                }
                finally
                {
                    suspendSessionPickerEvents = false;
                }
                return;
            }

            var entries = CollectAllMatchingSessionsAcrossDevices(currentConfig);
            suspendSessionPickerEvents = true;
            try
            {
                comboBoxAudioSession.Items.Clear();
                if (entries.Count == 0)
                {
                    comboBoxAudioSession.Enabled = false;
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
                    comboBoxAudioSession.Items.Add(FormatSessionPickLine(i, entries[i].session, entries[i].deviceName));

                comboBoxAudioSession.SelectedIndex = clamped;
                comboBoxAudioSession.Enabled = currentConfig.ControlSingleSession;
            }
            finally
            {
                suspendSessionPickerEvents = false;
            }
        }

        /// <summary>
        /// One dialog listing every configuration whose saved device ID is not among current endpoints
        /// (avoids multiple message boxes from per-device loops; see issue #24).
        /// </summary>
        private void ShowMissingAudioDevicesWarningIfNeeded()
        {
            if (configs is not { Length: > 0 })
                return;

            var validIds = new HashSet<string>(
                audioDevices?.Select(d => d.MMDevice.ID) ?? Enumerable.Empty<string>(),
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

            MessageBox.Show(message.ToString().TrimEnd(), "Audio device not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void PopulateConfigDisplayNameControl()
        {
            textBoxConfigDisplayName.Text = currentConfig.CustomDisplayName ?? "";
            UpdateConfigDisplayNamePlaceholder();
        }

        private void UpdateConfigDisplayNamePlaceholder()
        {
            textBoxConfigDisplayName.PlaceholderText = currentConfig.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(currentConfig));
        }

        private void UpdateConfigComboItemAtSelectedIndex()
        {
            int i = comboBoxConfig.SelectedIndex;
            if (i >= 0 && i < configs.Length)
                comboBoxConfig.Items[i] = GetConfigListDisplayName(configs[i]);
            SyncConfigComboDropDownWidth();
        }

        private void RefreshAllConfigComboItemTexts()
        {
            for (int i = 0; i < configs.Length && i < comboBoxConfig.Items.Count; i++)
                comboBoxConfig.Items[i] = GetConfigListDisplayName(configs[i]);
            SyncConfigComboDropDownWidth();
        }

        private void SyncConfigComboDropDownWidth()
        {
            if (comboBoxConfig.Items.Count == 0)
                return;
            int maxWidth = comboBoxConfig.Width;
            foreach (var item in comboBoxConfig.Items)
            {
                if (item is string text)
                {
                    int w = TextRenderer.MeasureText(text, comboBoxConfig.Font).Width
                        + SystemInformation.VerticalScrollBarWidth + 8;
                    if (w > maxWidth)
                        maxWidth = w;
                }
            }
            comboBoxConfig.DropDownWidth = Math.Min(maxWidth, 900);
        }

        private void textBoxConfigDisplayName_Leave(object? sender, EventArgs e)
        {
            if (comboBoxConfig.SelectedIndex < 0)
                return;

            string trimmed = textBoxConfigDisplayName.Text.Trim();
            string auto = currentConfig.GetAutoDisplayName(GetDeviceFriendlyNameForConfig(currentConfig));
            if (string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, auto, StringComparison.OrdinalIgnoreCase))
                currentConfig.CustomDisplayName = null;
            else
                currentConfig.CustomDisplayName = trimmed;

            textBoxConfigDisplayName.Text = currentConfig.CustomDisplayName ?? "";
            currentConfig.SaveConfig();
            UpdateConfigComboItemAtSelectedIndex();
            UpdateConfigDisplayNamePlaceholder();
        }

        private void buttonResetDisplayName_Click(object? sender, EventArgs e)
        {
            currentConfig.CustomDisplayName = null;
            currentConfig.SaveConfig();
            PopulateConfigDisplayNameControl();
            UpdateConfigComboItemAtSelectedIndex();
        }

        public void RegisterRawInputDevices()
        {
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, Handle);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, Handle);
        }

        // Load Hotkeys from Configurations
        public void UpdateHotkeysToListenFor()
        {
            hotkeysToListenFor = configs.SelectMany(config => config.Hotkeys).Distinct().ToArray();
        }

        // Create a method that will add or remove a hotkey from the array of hotkeys
        // based on the key pressed or released
        public void UpdateHotkeysHeld(int virtualKey, bool keyUp)
        {
            // If the key was pressed down (Flags = None) and the key is in the array of hotkeys
            if (hotkeysToListenFor.Contains(virtualKey) && keyUp == false && !hotkeysHeld.Contains(virtualKey))
            {
                hotkeysHeld = hotkeysHeld.Append(virtualKey).ToArray();
                RefreshAudioDevicesAndApps();
            }
            else if (keyUp) // If the key was released (Flags = Up)
            {
                hotkeysHeld = hotkeysHeld.Where(key => key != virtualKey).ToArray();
            }
        }

        // override the original WndProc method to process raw input messages
        // in a way that serves our purposes, then call the base method at the end
        protected override void WndProc(ref Message m)
        {
            // If the message is a raw input message, process it
            if (m.Msg == WM_INPUT)
            {
                // get the raw input data based on the handle from the message sent to WndProc
                var data = RawInputData.FromHandle(m.LParam);

                if (data is RawInputKeyboardData keyboardData)
                {
                    int virtualKey = keyboardData.Keyboard.VirutalKey; // Notice this typo in RawInput... Virutal not Virtual
                    bool keyUp = keyboardData.Keyboard.Flags.ToString() == up; // gotta be a better way to do this

                    // if we're looking to set the hot key, not adjust volume
                    if (listeningForHotkeyAdd)
                    {
                        AddHotkey(virtualKey);
                    }
                    else
                    {
                        // Update the array of hotkeys that are currently being held down
                        UpdateHotkeysHeld(virtualKey, keyUp);
                    }
                }
                else if (data is RawInputMouseData mouseData)
                {
                    bool isMouseWheel = mouseData.Mouse.Buttons.ToString() == mouseWheelButton; // gotta be a better way to do this
                    bool wasUpOrDown = mouseData.Mouse.ButtonData == mouseWheelUp || mouseData.Mouse.ButtonData == mouseWheelDown; // ensure to not capture wheel button presses

                    // If the mouse input was a scroll up or down
                    if (isMouseWheel && wasUpOrDown)
                    {
                        // Identify the configurations that match the hotkeys held
                        var matchingConfigs = configs
                            .Where(config => config.Hotkeys.SequenceEqual(hotkeysHeld))
                            .ToArray();

                        foreach (var config in matchingConfigs)
                        {
                            bool isUp = mouseData.Mouse.ButtonData == mouseWheelUp;
                            if (config.ControlDeviceMasterVolume)
                            {
                                var device = audioDevices?
                                    .FirstOrDefault(d => string.Equals(d.MMDevice.ID, config.DeviceId, StringComparison.OrdinalIgnoreCase))
                                    ?.MMDevice;
                                if (device != null)
                                    Audio.TryAdjustEndpointMasterVolume(device, isUp);
                                continue;
                            }

                            var sessions = GetCachedSessions(config);
                            if (sessions.Count == 0)
                                continue;

                            if (config.ControlSingleSession)
                                Audio.AdjustSessionsVolume(sessions, isUp, config.ProcessIndex);
                            else
                                Audio.AdjustSessionsVolume(sessions, isUp, null);
                        }
                    }
                }
            }
            base.WndProc(ref m); // Continue processing the message as WndProc normally would
        }

        // Populate config combo with friendly names (auto or custom); keep configs[] in sync.
        private void PopulateConfigs(int selectedIndex)
        {
            comboBoxConfig.BeginUpdate();
            try
            {
                comboBoxConfig.Items.Clear();
                foreach (var config in configs)
                    comboBoxConfig.Items.Add(GetConfigListDisplayName(config));

                int idx = Math.Clamp(selectedIndex, 0, Math.Max(0, configs.Length - 1));
                currentConfig = configs[idx];
                comboBoxConfig.SelectedIndex = idx;
            }
            finally
            {
                comboBoxConfig.EndUpdate();
            }
            SyncConfigComboDropDownWidth();
        }

        private void PopulateAudioDevices()
        {
            // Clear the combo box
            deviceComboBox.Items.Clear();
            int selectedIndex = 0;

            // Loop through all audio devices
            for (int i = 0; i < audioDevices.Length; i++)
            {
                var device = audioDevices[i];
                // Add each device to the combo box as a selectable option
                deviceComboBox.Items.Add(device.MMDevice.FriendlyName);

                // If the current config's Device selection is currently available
                if (device.MMDevice.ID == currentConfig.DeviceId)
                    selectedIndex = i;
            }

            // Set the selected index to the matched device or default to 0
            deviceComboBox.SelectedIndex = selectedIndex;
        }

        private void PopulateAudioAppSelection()
        {
            textBoxAppSelected.Text = currentConfig.AppFriendlyName;
        }

        private void PopulateHotkeys()
        {
            textboxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(KeyDisplayNames.GetDisplayName));
        }

        private void PopulateProcessControls()
        {
            checkBoxControlSingleAppProcess.Checked = currentConfig.ControlSingleSession;
            RefreshSessionPickerFromAudio();
        }

        private void ApplyVolumeTargetUi()
        {
            suspendDeviceMasterEvents = true;
            try
            {
                checkBoxDeviceMasterVolume.Checked = currentConfig.ControlDeviceMasterVolume;
                bool appMode = !currentConfig.ControlDeviceMasterVolume;
                appLabel.Enabled = appMode;
                textBoxAppSelected.Enabled = appMode;
                buttonAppSet.Enabled = appMode;
                checkBoxControlSingleAppProcess.Enabled = appMode;
                labelSession.Enabled = appMode;

                if (!appMode)
                {
                    suspendSessionPickerEvents = true;
                    try
                    {
                        comboBoxAudioSession.Items.Clear();
                        comboBoxAudioSession.Enabled = false;
                    }
                    finally
                    {
                        suspendSessionPickerEvents = false;
                    }
                }
                else
                {
                    RefreshSessionPickerFromAudio();
                    comboBoxAudioSession.Enabled = currentConfig.ControlSingleSession;
                }
            }
            finally
            {
                suspendDeviceMasterEvents = false;
            }
        }

        private void checkBoxDeviceMasterVolume_CheckedChanged(object? sender, EventArgs e)
        {
            if (suspendDeviceMasterEvents)
                return;

            currentConfig.ControlDeviceMasterVolume = checkBoxDeviceMasterVolume.Checked;
            currentConfig.SaveConfig();
            RebuildSessionCache();
            ApplyVolumeTargetUi();
            UpdateConfigComboItemAtSelectedIndex();
            UpdateConfigDisplayNamePlaceholder();
        }

        private void comboBoxConfig_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxConfig.SelectedIndex < 0 || comboBoxConfig.SelectedIndex >= configs.Length)
                return;

            var next = configs[comboBoxConfig.SelectedIndex];
            if (ReferenceEquals(next, currentConfig))
                return;

            currentConfig = next;
            LoadConfigToForm();
        }

        private void deviceComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Update current config with the selected device
            currentConfig.DeviceId = audioDevices[deviceComboBox.SelectedIndex].MMDevice.ID;
            currentConfig.SaveConfig();
            RefreshSessionPickerFromAudio();
            UpdateConfigComboItemAtSelectedIndex();
            UpdateConfigDisplayNamePlaceholder();
        }

        private void buttonHotkeyAdd_Click(object sender, EventArgs e)
        {
            // Update the button text to indicate to user to press a hotkey to set
            buttonHotkeyAdd.Text = "Listening...";

            // Disable the button so the function can't be called again until the hotkey is set  
            buttonHotkeyAdd.Enabled = false;

            // Set a flag/variable to indicate that we are waiting for a key press
            // so that WndProc knows to process the key press
            listeningForHotkeyAdd = true;
        }

        private void buttonHotkeyReset_Click(object? sender, EventArgs e)
        {
            // Clear the hotkey array
            hotkeysToListenFor = Array.Empty<int>();
            // Clear the textbox
            textboxHotkeys.Text = "";
            // Update Config Object
            currentConfig.Hotkeys = hotkeysToListenFor;
            // Save config to disk
            currentConfig.SaveConfig();
            UpdateConfigComboItemAtSelectedIndex();
            UpdateConfigDisplayNamePlaceholder();
        }

        private void AddHotkey(int virtualKey)
        {
            listeningForHotkeyAdd = false; // prevent double-triggering conditional code block by immediately stop listening
            buttonHotkeyAdd.Enabled = true; // re-enable the button to allow a new hotkey to be added
            buttonHotkeyAdd.Text = "Add"; // Reset the button text, maybe make this a constant

            // Check if the hotkey already exists in the current config's hotkeys array
            if (!currentConfig.Hotkeys.Contains(virtualKey))
            {
                // Add the hotkey to the current config's hotkeys array
                currentConfig.Hotkeys = currentConfig.Hotkeys.Append(virtualKey).ToArray();

                // Update the textbox text with the array of hotkeys as a string, in "HK1 + HK2 + HK3" format
                textboxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(KeyDisplayNames.GetDisplayName));

                // Save Config to Disk
                currentConfig.SaveConfig();

                // Append the hotkey to the array of hotkeys to listen for
                UpdateHotkeysToListenFor();
                UpdateConfigComboItemAtSelectedIndex();
                UpdateConfigDisplayNamePlaceholder();
            }
            else
            {
                MessageBox.Show("This hotkey is already added.", "Duplicate Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            
        }

        private void checkBoxControlSingleAppProcess_CheckedChanged(object? sender, EventArgs e)
        {
            currentConfig.ControlSingleSession = checkBoxControlSingleAppProcess.Checked;
            RefreshSessionPickerFromAudio();
            currentConfig.SaveConfig();
        }

        private void comboBoxAudioSession_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (suspendSessionPickerEvents)
                return;
            if (comboBoxAudioSession.SelectedIndex < 0)
                return;

            var entries = CollectAllMatchingSessionsAcrossDevices(currentConfig);
            if (entries.Count == 0)
                return;

            int idx = Math.Clamp(comboBoxAudioSession.SelectedIndex, 0, entries.Count - 1);
            currentConfig.ProcessIndex = idx;
            currentConfig.SaveConfig();
        }

        private void buttonSaveConfig_Click(object? sender, EventArgs e)
        {
            currentConfig.SaveConfig();
        }

        private void buttonNewConfig_Click(object sender, EventArgs e)
        {
            // Create a new config with default values
            Config newConfig = new Config
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

            // Add the new config to the configs array
            configs = configs.Append(newConfig).ToArray();

            // Save the new config to disk
            newConfig.SaveConfig();

            // Repopulate the configs in the GUI and select the new config
            PopulateConfigs(configs.Length - 1);
            LoadConfigToForm();
        }

        private void buttonDeleteConfig_Click(object? sender, EventArgs e)
        {
            // If there is only one config left, show a warning message and return
            if (configs.Length == 1)
            {
                MessageBox.Show("Cannot delete the only remaining configuration.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Get the index of the selected config
            int selectedIndex = comboBoxConfig.SelectedIndex;
            // Get the config object from the configs array
            Config configToDelete = configs[selectedIndex];
            // Remove the config from the configs array
            configs = configs.Where((source, index) => index != selectedIndex).ToArray();
            // Delete the config from disk
            configToDelete.DeleteConfig();
            // Repopulate the configs in the GUI (stay near the same position when possible)
            int nextIndex = Math.Min(selectedIndex, configs.Length - 1);
            PopulateConfigs(nextIndex);
            LoadConfigToForm();
        }
        private void RefreshAudioDevicesAndApps()
        {
            // Load all Audio Devices into Memory
            audioDevices = Audio.GetAudioDevices();

            // Load all Audio Apps into Memory
            var audioAppsList = new List<AudioApp>();
            foreach (var device in audioDevices)
            {
                audioAppsList.AddRange(Audio.GetAudioDeviceApps(device.MMDevice));
            }
            audioApps = audioAppsList.ToArray();

            RebuildSessionCache();

            // Repopulate the audio devices in the GUI
            PopulateAudioDevices();
            
            // Update process controls to reflect the current state of audio sessions
            PopulateProcessControls();
            ApplyVolumeTargetUi();
            RefreshAllConfigComboItemTexts();
            UpdateConfigDisplayNamePlaceholder();
        }
        private void buttonRefreshAudio_Click(object sender, EventArgs e)
        {
            RefreshAudioDevicesAndApps();
            ShowMissingAudioDevicesWarningIfNeeded();
        }
        private void OpenAppSelectionForm(AudioApp[] audioApps, string appFileName)
        {
            using (var appSelectionForm = new AppSelection(audioApps, currentConfig.AppFriendlyName))
            {
                if (appSelectionForm.ShowDialog() == DialogResult.OK)
                {
                    // Get SelectedAppFriendlyName from appSelectionForm
                    string? selectedAppFriendlyName = appSelectionForm.SelectedAppFriendlyName;

                    // Validate the selected app name is not null or empty
                    if (string.IsNullOrWhiteSpace(selectedAppFriendlyName))
                    {
                        MessageBox.Show("Invalid application name selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Prefer the app row for the config's output device when the same title exists on multiple endpoints
                    var matchingApp = audioApps?
                        .Where(app => app != null && string.Equals(app.DeviceId, currentConfig.DeviceId, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault(app => !string.IsNullOrWhiteSpace(app!.AppFriendlyName) && app.AppFriendlyName.Equals(selectedAppFriendlyName, StringComparison.OrdinalIgnoreCase));
                    matchingApp ??= audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFriendlyName) && app.AppFriendlyName.Equals(selectedAppFriendlyName, StringComparison.OrdinalIgnoreCase));
                    
                    string selectedAppFileName;
                    string actualFriendlyName;
                    
                    if (matchingApp != null)
                    {
                        // Found by friendly name - use the app's actual file name
                        selectedAppFileName = matchingApp.AppFileName;
                        actualFriendlyName = matchingApp.AppFriendlyName;
                    }
                    else
                    {
                        // Not found by friendly name - user probably entered a filename manually
                        // Try to find by filename (device first)
                        matchingApp = audioApps?
                            .Where(app => app != null && string.Equals(app.DeviceId, currentConfig.DeviceId, StringComparison.OrdinalIgnoreCase))
                            .FirstOrDefault(app => !string.IsNullOrWhiteSpace(app!.AppFileName) && app.AppFileName.Equals(selectedAppFriendlyName, StringComparison.OrdinalIgnoreCase));
                        matchingApp ??= audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName.Equals(selectedAppFriendlyName, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchingApp != null)
                        {
                            // Found by filename - use the app's friendly name
                            selectedAppFileName = matchingApp.AppFileName;
                            actualFriendlyName = matchingApp.AppFriendlyName;
                        }
                        else
                        {
                            // Not found in active apps - assume it's a manual entry
                            selectedAppFileName = selectedAppFriendlyName;
                            actualFriendlyName = selectedAppFriendlyName;
                        }
                    }

                    // Update the current config's AppFileName and AppFriendlyName
                    currentConfig.AppFileName = selectedAppFileName;
                    currentConfig.AppFriendlyName = actualFriendlyName;

                    // Update the textbox text with the selected app name
                    textBoxAppSelected.Text = actualFriendlyName;
                    
                    // Reset process index to 0 when changing apps
                    currentConfig.ProcessIndex = 0;
                    
                    // Update process controls to reflect the new app's sessions
                    PopulateProcessControls();

                    // Save the current config
                    currentConfig.SaveConfig();
                    UpdateConfigComboItemAtSelectedIndex();
                    UpdateConfigDisplayNamePlaceholder();
                }
            }
        }

        private void buttonAppSet_Click(object? sender, EventArgs e)
        {
            OpenAppSelectionForm(audioApps, currentConfig.AppFileName);
        }

        private void checkBoxOpenAtStartup_CheckedChanged(object? sender, EventArgs e)
        {
            if (suspendOpenAtStartupEvents)
                return;

            try
            {
                StartupRegistration.SetRegistered(checkBoxOpenAtStartup.Checked);
            }
            catch (Exception ex)
            {
                suspendOpenAtStartupEvents = true;
                try
                {
                    checkBoxOpenAtStartup.Checked = !checkBoxOpenAtStartup.Checked;
                }
                finally
                {
                    suspendOpenAtStartupEvents = false;
                }

                MessageBox.Show(
                    $"Could not update the Windows startup setting.\n\n{ex.Message}",
                    "KBMixer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
