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
        public int[] hotkeyVirtualKeys = Array.Empty<int>(); // Array of Hotkeys to Listen For
        public bool hotkeyHeld = false;
        public bool listeningForHotkeyAdd = false;

        public Form1()
        {
            InitializeComponent();
            RegisterRawInputDevices();

            // Load all Audio Devices into Memory
            audioDevices = Audio.GetAudioDevices();

            // Load all Audio Apps into Memory
            foreach (var device in audioDevices)
            {
                audioApps = Audio.GetAudioDeviceApps(device.MMDevice);
            }

            foreach (var app in audioApps)
            {
                Debug.WriteLine("AUDIO APP SESSION ID: " + app.Sessions[0].GetSessionInstanceIdentifier);
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

                    // If the key was pressed down (Flags = None) and the key is in the array of hotkeys
                    if (hotkeyVirtualKeys.Contains(virtualKey) && keyUp == false)
                    {
                        hotkeyHeld = true;
                    }
                    else // If the key was released (Flags = Up)
                    {
                        hotkeyHeld = false;
                    }
                }
                else if (data is RawInputMouseData mouseData)
                {
                    bool isMouseWheel = mouseData.Mouse.Buttons.ToString() == mouseWheelButton; // gotta be a better way to do this
                    bool wasUpOrDown = mouseData.Mouse.ButtonData == mouseWheelUp || mouseData.Mouse.ButtonData == mouseWheelDown; // ensure to not capture wheel button presses

                    // If the mouse input was a scroll up or down
                    if (isMouseWheel && wasUpOrDown)
                    {
                        // WORK IN PROGRESS wip WIP
                        // Identify the audio app associated to the held hotkey(s)

                        // Adjust the volume of the audio app (or the specified session) based on scroll direction
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

        private void appComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //selectedAppName = kbMixerSessions[appComboBox.SelectedIndex].AppName; // Update the selected app name
            //selectedSessionInstanceIdentifier = kbMixerSessions[appComboBox.SelectedIndex].Sessions[0].GetSessionInstanceIdentifier; // Update the selected session object
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
            hotkeyVirtualKeys = Array.Empty<int>();
            // Clear the textbox
            textboxHotkeys.Text = "";
            // Update Config Object
            currentConfig.Hotkeys = hotkeyVirtualKeys;
            // Save config to disk
            currentConfig.SaveConfig();
        }

        private void AddHotkey(int virtualKey)
        {
            listeningForHotkeyAdd = false; // prevent double-triggering conditional code block by immediately stop listening
            buttonHotkeyAdd.Enabled = true; // re-enable the button to allow a new hotkey to be added
            buttonHotkeyAdd.Text = "Add"; // Reset the button text, maybe make this a constant

            // Check if the hotkey already exists in the array
            if (!hotkeyVirtualKeys.Contains(virtualKey))
            {
                // Add the hotkey to the array
                hotkeyVirtualKeys = hotkeyVirtualKeys.Append(virtualKey).ToArray();

                // Update the textbox text with the array of hotkeys as a string, in "HK1 + HK2 + HK3" format
                textboxHotkeys.Text = string.Join(" + ", hotkeyVirtualKeys.Select(key => ((Keys)key).ToString()));
            }
            else
            {
                MessageBox.Show("This hotkey is already added.", "Duplicate Hotkey", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            currentConfig.Hotkeys = hotkeyVirtualKeys;

            // Save Config to Disk
            currentConfig.SaveConfig();
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
        private void buttonAddManualApp_Click(object sender, EventArgs e)
        {

        }

        private void labelConfig_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void processIndexSelector_ValueChanged(object sender, EventArgs e)
        {
            currentConfig.ProcessIndex = (int)processIndexSelector.Value;
            currentConfig.SaveConfig();
        }

        private void buttonSaveConfig_Click(object sender, EventArgs e)
        {
            currentConfig.SaveConfig();
        }

        private void textBoxEnter_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

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
