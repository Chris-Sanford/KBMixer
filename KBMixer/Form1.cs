using Linearstar.Windows.RawInput;
using NAudio.CoreAudioApi;
using System.Diagnostics;

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

        private const int LayoutFieldLeft = 56;
        private const int LayoutMarginRight = 12;
        private const int LayoutFieldToButtonGap = 8;
        private const int LayoutTwinButtonWidth = 64;
        private const int LayoutTwinButtonGap = 6;
        private const int LayoutActionStripWidth = LayoutTwinButtonWidth + LayoutTwinButtonGap + LayoutTwinButtonWidth;

        public Form1()
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

            // Update the array of hotkeys to listen for from all available configs
            UpdateHotkeysToListenFor();

            ApplyResponsiveLayout();
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

            labelInstructions.Left = LayoutFieldLeft;
            labelInstructions.Width = Math.Max(200, cw - LayoutFieldLeft - LayoutMarginRight);

            SyncConfigComboDropDownWidth();
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

                        // Create the array of AudioApps that match the app file names
                        var matchingAudioApps = audioApps
                            ?.Where(app => app != null && !string.IsNullOrWhiteSpace(app.AppFileName) && matchingConfigs.Any(config => config.AppFileName == app.AppFileName))
                            .ToArray() ?? Array.Empty<AudioApp>();

                        // Adjust the volume of the audio apps (or the specified session) based on scroll direction
                        foreach (var app in matchingAudioApps)
                        {
                            var config = matchingConfigs.FirstOrDefault(c => c.AppFileName == app.AppFileName);
                            if (config != null && config.ControlSingleSession)
                            {
                                app.AdjustVolume(mouseData.Mouse.ButtonData == mouseWheelUp, config.ProcessIndex);
                            }
                            else
                            {
                                app.AdjustVolume(mouseData.Mouse.ButtonData == mouseWheelUp);
                            }
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
                {
                    selectedIndex = i; // select it
                }
                else
                {
                    //MessageBox.Show("Audio device from configuration is not available. Please set a desired Audio Output Device.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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
            
            // Get the selected app to determine the valid range for process index
            var selectedApp = audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName == currentConfig.AppFileName);
            if (selectedApp != null && selectedApp.Sessions.Count > 0)
            {
                int maxIndex = selectedApp.Sessions.Count - 1;
                processIndexSelector.Minimum = 0;
                processIndexSelector.Maximum = maxIndex;
                
                // Ensure the current value is within valid range
                if (currentConfig.ProcessIndex > maxIndex)
                {
                    currentConfig.ProcessIndex = maxIndex;
                    processIndexSelector.Value = maxIndex;
                }
                else
                {
                    processIndexSelector.Value = currentConfig.ProcessIndex;
                }
                
                processIndexSelector.Enabled = currentConfig.ControlSingleSession;
            }
            else
            {
                // No sessions available, disable the control
                processIndexSelector.Value = 0;
                processIndexSelector.Enabled = false;
                
                // If control single session is enabled but there are no sessions, update the config
                if (currentConfig.ControlSingleSession)
                {
                    currentConfig.ProcessIndex = 0;
                }
            }
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
            if (currentConfig.ControlSingleSession)
            {
                // Get the selected app to determine the valid range for process index
                var selectedApp = audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName == currentConfig.AppFileName);
                if (selectedApp != null && selectedApp.Sessions.Count > 0)
                {
                    int maxIndex = selectedApp.Sessions.Count - 1;
                    processIndexSelector.Minimum = 0;
                    processIndexSelector.Maximum = maxIndex;
                    
                    // Ensure the current value is within valid range
                    if (currentConfig.ProcessIndex > maxIndex)
                    {
                        currentConfig.ProcessIndex = maxIndex;
                        processIndexSelector.Value = maxIndex;
                    }
                    else
                    {
                        processIndexSelector.Value = currentConfig.ProcessIndex;
                    }
                    
                    processIndexSelector.Enabled = true;
                }
                else
                {
                    // No sessions available, disable the control
                    processIndexSelector.Value = 0;
                    processIndexSelector.Enabled = false;
                    currentConfig.ProcessIndex = 0;
                }
            }
            else
            {
                processIndexSelector.Enabled = false;
            }
            currentConfig.SaveConfig();
        }

        private void processIndexSelector_ValueChanged(object sender, EventArgs e)
        {
            var selectedApp = audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName == currentConfig.AppFileName);
            if (selectedApp != null)
            {
                int maxIndex = selectedApp.Sessions.Count - 1;
                if (maxIndex < 0)
                {
                    // No sessions available
                    processIndexSelector.Enabled = false;
                    processIndexSelector.Value = 0;
                    currentConfig.ProcessIndex = 0;
                }
                else
                {
                    // Set the minimum and maximum values for the NumericUpDown control
                    processIndexSelector.Minimum = 0;
                    processIndexSelector.Maximum = maxIndex;
                    
                    // Ensure the value is within valid range
                    if (processIndexSelector.Value > maxIndex)
                    {
                        processIndexSelector.Value = maxIndex;
                    }
                    
                    // Update the config with the new process index
                    currentConfig.ProcessIndex = (int)processIndexSelector.Value;
                    currentConfig.SaveConfig();
                }
            }
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
                ProcessIndex = 0
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

            // Repopulate the audio devices in the GUI
            PopulateAudioDevices();
            
            // Update process controls to reflect the current state of audio sessions
            PopulateProcessControls();
            RefreshAllConfigComboItemTexts();
            UpdateConfigDisplayNamePlaceholder();
        }
        private void buttonRefreshAudio_Click(object sender, EventArgs e)
        {
            RefreshAudioDevicesAndApps();
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

                    // Try to find the corresponding AudioApp by friendly name first
                    var matchingApp = audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFriendlyName) && app.AppFriendlyName.Equals(selectedAppFriendlyName, StringComparison.OrdinalIgnoreCase));
                    
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
                        // Try to find by filename
                        matchingApp = audioApps?.FirstOrDefault(app => app != null && !string.IsNullOrWhiteSpace(app.AppFileName) && app.AppFileName.Equals(selectedAppFriendlyName, StringComparison.OrdinalIgnoreCase));
                        
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
    }
}
