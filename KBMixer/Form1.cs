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

            // Load all Audio Devices and Audio Apps into Memory
            audioDevices = Audio.GetAudioDevices();

            foreach (var device in audioDevices)
            {
                audioApps = Audio.GetAudioDeviceApps(device.MMDevice);
            }

            // Get Configs from Disk, if exists
            configs = Configurations.LoadConfigsFromDisk();

            // If there are no configs from disk, create a default config
            if (configs.Length == 0)
            {
                Config defaultConfig = new Config
                {
                    DeviceId = audioDevices[0].MMDevice.ID,
                    AppFileName = audioDevices[0].AudioApps[0].AppFileName,
                    Hotkeys = Array.Empty<int>(),
                    ControlSingleSession = false,
                    ProcessIndex = 0
                };
                configs = new Config[] { defaultConfig };
            }

            // Populate Configs into GUI
            PopulateConfigs();

            audioDevices = Audio.GetAudioDevices(); // Get all audio devices

            foreach (var device in audioDevices)
            {
                audioApps = Audio.GetAudioDeviceApps(device.MMDevice);
            }


            PopulateAudioDevices();
            PopulateAudioApps();
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
                comboBoxConfig.Items.Add(config.AppFileName);
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
            }

            // Set the selected index to the matched device or default to 0
            deviceComboBox.SelectedIndex = selectedIndex;
        }

        private void PopulateAudioApps()
        {
            // Update this so it only populates audio apps pertaining to the selected device / current config
            // Clear the combo box
            appComboBox.Items.Clear();
            // Loop through all audio apps
            foreach (var app in audioApps)
            {
                // Add each app to the combo box
                appComboBox.Items.Add(app.AppFriendlyName);
            }

            // Add the app from the current config to the combo box
            appComboBox.SelectedIndex = 0;
        }

        private void comboBoxConfig_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentConfig = configs[comboBoxConfig.SelectedIndex]; // Update the current config object
            PopulateAudioDevices();
            PopulateAudioApps();
        }

        private void deviceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            //MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            //selectedDevice = devices[deviceComboBox.SelectedIndex]; // Update the selected device object
            //PopulateAudioOutputSessions();
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
        }

        private void checkBoxControlSingleAppProcess_CheckedChanged(object sender, EventArgs e)
        {
            currentConfig.ControlSingleSession = checkBoxControlSingleAppProcess.Checked;
            if (currentConfig.ControlSingleSession)
            {
                processIndexSelector.Enabled = true;
                currentConfig.ProcessIndex = (int)processIndexSelector.Value;
            }
            else
            {
                processIndexSelector.Enabled = false;
            }

            // Write all properties of the Config class to debug output
            Debug.WriteLine($"ConfigId: {currentConfig.ConfigId}");
            Debug.WriteLine($"DeviceId: {currentConfig.DeviceId}");
            Debug.WriteLine($"AppFileName: {currentConfig.AppFileName}");
            Debug.WriteLine($"Hotkeys: {currentConfig.Hotkeys}");
            Debug.WriteLine($"ControlSingleSession: {currentConfig.ControlSingleSession}");
            Debug.WriteLine($"ProcessIndex: {currentConfig.ProcessIndex}");
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
        }

        private void buttonSaveConfig_Click(object sender, EventArgs e)
        {
            currentConfig.SaveConfig();
        }
        private void checkBoxSetAppManual_CheckedChanged(object sender, EventArgs e)
        {
            textBoxAppManual.ReadOnly = !checkBoxSetAppManual.Checked;
            appComboBox.Enabled = !checkBoxSetAppManual.Checked;
        }
    }
}
