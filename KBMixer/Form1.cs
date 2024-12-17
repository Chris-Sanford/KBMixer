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
        public Config currentConfig; // Current Configuration of the Application
        public int[] hotkeysToListenFor = Array.Empty<int>(); // Array of Hotkeys to Listen For
        public int[] hotkeysHeld = Array.Empty<int>(); // Array of Hotkeys that are currently being held down
        public bool listeningForHotkeyAdd = false;

        public Form1()
        {
            InitializeComponent();

            this.Resize += new EventHandler(MainForm_Resize);
            trayIcon.Click += new EventHandler(TrayIcon_Click);

            RegisterRawInputDevices();

            // Load all Audio Devices into Memory
            audioDevices = Audio.GetAudioDevices();

            // Load all Audio Apps into Memory
            foreach (var device in audioDevices)
            {
                audioApps = Audio.GetAudioDeviceApps(device.MMDevice);
            }

            // Get Configs from Disk, if exists
            configs = Configurations.LoadConfigsFromDisk();

            // If there are no configs from disk, create a default config
            if (configs.Length == 0)
            {
                buttonNewConfig_Click(null, null);
            }

            currentConfig = configs[0]; // Set the current config to the first config

            // Populate Configs into GUI
            PopulateConfigs();

            // Update the array of hotkeys to listen for from all available configs
            UpdateHotkeysToListenFor();
        }

        void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                trayIcon.Visible = true;
                this.Hide();
            }
        }

        void TrayIcon_Click(object sender, EventArgs e)
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
            }
            else if (keyUp) // If the key was released (Flags = Up)
            {
                hotkeysHeld = hotkeysHeld.Where(key => key != virtualKey).ToArray();
            }
            Debug.WriteLine("Hotkeys Held: " + string.Join(", ", hotkeysHeld.Select(key => ((Keys)key).ToString())));
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
                            .Where(app => matchingConfigs.Any(config => config.AppFileName == app.AppFileName))
                            .ToArray();

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

        // Specifically only for populating Configs into ComboBox
        // NOT getting the config objects themselves
        private void PopulateConfigs()
        {
            // Clear the combo box
            comboBoxConfig.Items.Clear();
            // Loop through all configs
            foreach (var config in configs)
            {
                // Add each config to the combo box
                comboBoxConfig.Items.Add(config.ConfigId);
            }
            // Select the first config in the combo box
            comboBoxConfig.SelectedIndex = 0;
            currentConfig = configs[0];
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
                    MessageBox.Show("Audio device from configuration is not available. Please set a desired Audio Output Device.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // Set the selected index to the matched device or default to 0
            deviceComboBox.SelectedIndex = selectedIndex;
        }

        private void PopulateAudioAppSelection()
        {
            textBoxAppSelected.Text = currentConfig.AppFileName;
        }

        private void PopulateHotkeys()
        {
            textboxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(key => ((Keys)key).ToString()));
        }

        private void PopulateProcessControls()
        {
            checkBoxControlSingleAppProcess.Checked = currentConfig.ControlSingleSession;
            processIndexSelector.Value = currentConfig.ProcessIndex;
        }

        private void comboBoxConfig_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentConfig = configs[comboBoxConfig.SelectedIndex]; // Update the current config object
            LoadConfigToForm();
        }

        private void deviceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update current config with the selected device
            currentConfig.DeviceId = audioDevices[deviceComboBox.SelectedIndex].MMDevice.ID;
            currentConfig.SaveConfig();
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

        private void buttonHotkeyReset_Click(object sender, EventArgs e)
        {
            // Clear the hotkey array
            hotkeysToListenFor = Array.Empty<int>();
            // Clear the textbox
            textboxHotkeys.Text = "";
            // Update Config Object
            currentConfig.Hotkeys = hotkeysToListenFor;
            // Save config to disk
            currentConfig.SaveConfig();
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
                textboxHotkeys.Text = string.Join(" + ", currentConfig.Hotkeys.Select(key => ((Keys)key).ToString()));

                // Save Config to Disk
                currentConfig.SaveConfig();

                // Append the hotkey to the array of hotkeys to listen for
                UpdateHotkeysToListenFor();
            }
            else
            {
                MessageBox.Show("This hotkey is already added.", "Duplicate Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            
        }

        private void checkBoxControlSingleAppProcess_CheckedChanged(object sender, EventArgs e)
        {
            currentConfig.ControlSingleSession = checkBoxControlSingleAppProcess.Checked;
            if (currentConfig.ControlSingleSession)
            {
                processIndexSelector.Enabled = true;
            }
            else
            {
                processIndexSelector.Enabled = false;
            }
            Debug.WriteLine("Current Config's ControlSingleSession: " + currentConfig.ControlSingleSession);
            currentConfig.SaveConfig();
        }

        private void processIndexSelector_ValueChanged(object sender, EventArgs e)
        {
            var selectedApp = audioApps.FirstOrDefault(app => app.AppFileName == currentConfig.AppFileName);
            if (selectedApp != null)
            {
                int maxIndex = selectedApp.Sessions.Count - 1;
                if (processIndexSelector.Value < 0 || processIndexSelector.Value > maxIndex)
                {
                    // I don't want to risk this message box throwing a warning at startup, especially at system startup if configured as such
                    // Consider circling back to this. Maybe only display warning for this in the GUI itself
                    //MessageBox.Show($"The current valid range of processes for the selected app is between 0 and {maxIndex}. Make sure the selected app is actively running, then try again.", "Invalid Process Index", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    processIndexSelector.Value = Math.Clamp(processIndexSelector.Value, 0, maxIndex);
                }
                else
                {
                    currentConfig.ProcessIndex = (int)processIndexSelector.Value;
                    currentConfig.SaveConfig();
                }
            }
            else
            {
                // This throws the error even when creating a new config from scratch
                //MessageBox.Show("Selected audio app not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonSaveConfig_Click(object sender, EventArgs e)
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
                AppFileName = "System Sounds",
                Hotkeys = Array.Empty<int>(),
                ControlSingleSession = false,
                ProcessIndex = 0
            };

            // Add the new config to the configs array
            configs = configs.Append(newConfig).ToArray();

            // Save the new config to disk
            newConfig.SaveConfig();

            // Update the current config to the new config
            currentConfig = newConfig;

            // Repopulate the configs in the GUI
            PopulateConfigs();

            // Select the new config in the combo box
            comboBoxConfig.SelectedIndex = configs.Length - 1;
        }

        private void buttonDeleteConfig_Click(object sender, EventArgs e)
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
            // Repopulate the configs in the GUI
            PopulateConfigs();
            // Select the first config in the combo box
            comboBoxConfig.SelectedIndex = 0;
        }
        private void buttonRefreshAudio_Click(object sender, EventArgs e)
        {
            // Load all Audio Devices into Memory
            audioDevices = Audio.GetAudioDevices();

            // Load all Audio Apps into Memory
            foreach (var device in audioDevices)
            {
                audioApps = Audio.GetAudioDeviceApps(device.MMDevice);
            }

            // Repopulate the audio devices in the GUI
            PopulateAudioDevices();
        }
        private void OpenAppSelectionForm(AudioApp[] audioApps, string appFileName)
        {
            using (var appSelectionForm = new AppSelection(audioApps, appFileName))
            {
                if (appSelectionForm.ShowDialog() == DialogResult.OK)
                {
                    // Get SelectedAppFileName from appSelectionForm
                    string selectedAppFileName = appSelectionForm.SelectedAppFileName;

                    Debug.WriteLine("SelectedAppFileName in Form1: " + selectedAppFileName);

                    // Update the current config's AppFileName
                    currentConfig.AppFileName = selectedAppFileName;

                    // Update the textbox text with the selected app name
                    textBoxAppSelected.Text = selectedAppFileName;

                    // Save the current config
                    currentConfig.SaveConfig();
                }
            }
        }

        private void buttonAppSet_Click(object sender, EventArgs e)
        {
            OpenAppSelectionForm(audioApps, currentConfig.AppFileName);
        }
    }
}
