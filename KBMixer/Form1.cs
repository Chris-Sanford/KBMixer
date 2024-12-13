using Linearstar.Windows.RawInput;
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

        public Config currentConfig; // Current Configuration of the Application
        public int[] hotkeyVirtualKeys = Array.Empty<int>(); // Array of Hotkeys to Listen For
        public bool hotkeyHeld = false;
        public bool listeningForHotkeySet = false;

        public Form1()
        {
            InitializeComponent();
            RegisterRawInputDevices();

            // For Testing Purposes
            currentConfig = new Config
            {
                DeviceId = "defaultDeviceId",
                AppFileName = "defaultAppFileName",
                Hotkeys = Array.Empty<int>()
            }; // Create an instance of the Config class
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
                    if (listeningForHotkeySet)
                    {
                        listeningForHotkeySet = false; // prevent double-triggering conditional code block by immediately stop listening
                        btnHotkey.Enabled = true; // re-enable the button to allow the hotkey to be changed
                        btnHotkey.Text = ((Keys)virtualKey).ToString(); // represent button press with friendly key name
                        hotkeyVirtualKeys = hotkeyVirtualKeys.Append(virtualKey).ToArray(); // set the hotkey to be used for volume control
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

        private void btnHotkey_Click(object sender, EventArgs e)
        {
            // Update the button text to say "Press a key..."  
            btnHotkey.Text = "Press a key...";

            // Disable the button so the function can't be called again until the hotkey is set  
            btnHotkey.Enabled = false;

            // Set a flag/variable to indicate that we are waiting for a key press
            // so that WndProc knows to process the key press
            listeningForHotkeySet = true;
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
    }
}
