using Linearstar.Windows.RawInput;
using NAudio.CoreAudioApi;
using System.Diagnostics;

namespace KBMixer
{
    public partial class Form1 : Form
    {
        private const int WM_INPUT = 0x00FF;

        public Form1()
        {
            InitializeComponent();
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
                    int virtualKey = keyboardData.Keyboard.VirutalKey;
                    bool keyUp = keyboardData.Keyboard.Flags.ToString() == keyUpCode; // gotta be a better way to do this

                    if (listeningForHotkeySet)
                    {
                        listeningForHotkeySet = false; // prevent double-triggering conditional code block by immediately stop listening
                        btnHotkey.Enabled = true; // re-enable the button to allow the hotkey to be changed
                        btnHotkey.Text = ((Keys)virtualKey).ToString(); // rep button press with key name
                        hotkey = virtualKey; // set the hotkey to be used for volume control
                    }

                    // If Input from Keyboard and Flags = None, key was pressed down
                    // If Input from Keyboard and Flags = Up, key was released
                    // If hotkey was pressed down, set hotkeyHeld to true

                    if (virtualKey == hotkey && keyUp == false)
                    {
                        hotkeyHeld = true;
                    }
                    else
                    {
                        hotkeyHeld = false;
                    }
                }
                else if (data is RawInputMouseData mouseData)
                {
                    bool isMouseWheel = mouseData.Mouse.Buttons.ToString() == mouseWheelButton; // gotta be a better way to do this
                    bool mouseWheelEvent = mouseData.Mouse.ButtonData == mouseWheelUp || mouseData.Mouse.ButtonData == mouseWheelDown;

                    // if hotkeyHeld is true, write debug output
                    if (hotkeyHeld && mouseData.Mouse.ButtonData == mouseWheelUp && appSelected)
                    {
                        AdjustSessionVolume(up, volumeIncrement);
                    }
                    else if (hotkeyHeld && mouseData.Mouse.ButtonData == mouseWheelDown && appSelected)
                    {
                        AdjustSessionVolume(down, volumeIncrement);
                    }
                }
            }
            base.WndProc(ref m); // Continue processing the message as WndProc normally would
        }

        private void deviceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            selectedDevice = devices[deviceComboBox.SelectedIndex]; // Update the selected device object
            PopulateAudioOutputSessions();
        }

        private void appComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedAppName = kbMixerSessions[appComboBox.SelectedIndex].AppName; // Update the selected app name
            selectedSessionInstanceIdentifier = kbMixerSessions[appComboBox.SelectedIndex].Sessions[0].GetSessionInstanceIdentifier; // Update the selected session object
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
            controlSingleAppProcess = checkBoxControlSingleAppProcess.Checked;
            if (controlSingleAppProcess)
            {
                indexOfProcessToControl = (int)processIndexSelector.Value;
            }
        }
    }
}
