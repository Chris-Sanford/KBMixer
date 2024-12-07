using System.Diagnostics;
using Linearstar.Windows.RawInput;

namespace RawGlobalInputGUI01
{
    public partial class Form1 : Form
    {
        private const int WM_INPUT = 0x00FF;
        private const int mouseWheelUp = 120;
        private const int mouseWheelDown = -120;
        private const string mouseWheelButton = "MouseWheel";
        private const string keyUpCode = "Up";
        private int hotkey;
        private bool listeningForHotkeySet = false;
        private bool hotkeyHeld = false;

        // Consider building a constant dictionary with all keys from this class
        // to represent hotkey in user-friendly way
        // just take the values as seen in the descriptions of each key
        // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-9.0

        public Form1()
        {
            InitializeComponent();
            GetRawInputDevices(); // Is this really necessary for functionality? I think it's just listing/enumerating
            RegisterRawInputDevices();
        }

        private void GetRawInputDevices()
        {
            var devices = RawInputDevice.GetDevices();
            foreach (var device in devices)
            {
                richTextBox1.AppendText("Device Path: " + device.DevicePath + "\n");
                richTextBox1.AppendText("Device Type: " + device.DeviceType + "\n");
                richTextBox1.AppendText("Vendor ID: " + device.VendorId + "\n");
                richTextBox1.AppendText("Product ID: " + device.ProductId + "\n");
                richTextBox1.AppendText("\n");
                Debug.WriteLine("Device Path: " + device.DevicePath);
                Debug.WriteLine("Device Type: " + device.DeviceType);
                Debug.WriteLine("Vendor ID: " + device.VendorId);
                Debug.WriteLine("Product ID: " + device.ProductId);
                Debug.WriteLine("");
            }
        }

        private void RegisterRawInputDevices()
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

                //Debug.WriteLine("");
                //Debug.WriteLine("Input Type: " + data.Header.Type);
                //Debug.WriteLine("Input Device Handle: " + data.Header.DeviceHandle);
                //Debug.WriteLine("Input Size: " + data.Header.Size);
                //Debug.WriteLine("Input Device: " + data.Device);

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
                        Debug.WriteLine("Hotkey pressed down: " + ((Keys)virtualKey).ToString());
                    }
                    else
                    {
                        hotkeyHeld = false;
                    }

                    //Debug.WriteLine("Virtual Key: " + virtualKey);
                    //Debug.WriteLine("Extra Information: " + keyboardData.Keyboard.ExtraInformation);
                    //Debug.WriteLine("Flags: " + keyboardData.Keyboard.Flags);
                    //Debug.WriteLine("Window Message: " + keyboardData.Keyboard.WindowMessage);
                    //Debug.WriteLine("Scan Code: " + keyboardData.Keyboard.ScanCode);
                }
                else if (data is RawInputMouseData mouseData)
                {
                    bool isMouseWheel = mouseData.Mouse.Buttons.ToString() == mouseWheelButton; // gotta be a better way to do this
                    bool mouseWheelEvent = mouseData.Mouse.ButtonData == mouseWheelUp || mouseData.Mouse.ButtonData == mouseWheelDown;

                    // if hotkeyHeld is true, write debug output
                    if (hotkeyHeld && mouseWheelEvent)
                    {
                        Debug.WriteLine("Mouse Buttons: " + mouseData.Mouse.Buttons); // MouseWheel is the scroll wheel  
                        Debug.WriteLine("Mouse Button Data: " + mouseData.Mouse.ButtonData); // 120 is up, -120 is down  
                    }

                    //Debug.WriteLine("Mouse Buttons: " + mouseData.Mouse.Buttons); // MouseWheel is the scroll wheel  
                    //Debug.WriteLine("Mouse Button Data: " + mouseData.Mouse.ButtonData); // 120 is up, -120 is down  
                    //Debug.WriteLine("Mouse Last X: " + mouseData.Mouse.LastX);
                    //Debug.WriteLine("Mouse Last Y: " + mouseData.Mouse.LastY);
                }
            }

            base.WndProc(ref m); // Continue processing the message as WndProc normally would
        }

        void Form1_Load(object sender, EventArgs e)
        {

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
    }
}
