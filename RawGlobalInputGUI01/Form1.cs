using System.Diagnostics;
using Linearstar.Windows.RawInput;

namespace RawGlobalInputGUI01
{
    public partial class Form1 : Form
    {
        private Dictionary<int, string> keyMap;

        public Form1()
        {
            InitializeComponent();
            GetRawInputDevices();
            RegisterRawInputDevices();
            InitializeKeyMap();
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

        private void InitializeKeyMap()
        {
            keyMap = new Dictionary<int, string>();
            for (int i = 0; i <= 255; i++)
            {
                keyMap[i] = ((Keys)i).ToString();
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;

            if (m.Msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(m.LParam);
                Debug.WriteLine("Input Type: " + data.Header.Type);
                Debug.WriteLine("Input Device Handle: " + data.Header.DeviceHandle);
                Debug.WriteLine("Input Size: " + data.Header.Size);
                Debug.WriteLine("Input Device: " + data.Device);

                if (data is RawInputKeyboardData keyboardData)
                {
                    int virtualKey = keyboardData.Keyboard.VirutalKey;
                    if (keyMap.ContainsKey(virtualKey))
                    {
                        Debug.WriteLine("Key Pressed: " + keyMap[virtualKey]);
                    }
                    Debug.WriteLine("Virtual Key: " + virtualKey);
                    Debug.WriteLine("Extra Information: " + keyboardData.Keyboard.ExtraInformation);
                    Debug.WriteLine("Flags: " + keyboardData.Keyboard.Flags);
                    Debug.WriteLine("Window Message: " + keyboardData.Keyboard.WindowMessage);
                    Debug.WriteLine("Scan Code: " + keyboardData.Keyboard.ScanCode);
                }
                else if (data is RawInputMouseData mouseData)
                {
                    Debug.WriteLine("Mouse Buttons: " + mouseData.Mouse.Buttons); // MouseWheel is the scroll wheel  
                    Debug.WriteLine("Mouse Button Data: " + mouseData.Mouse.ButtonData); // 120 is up, -120 is down  
                    Debug.WriteLine("Mouse Last X: " + mouseData.Mouse.LastX);
                    Debug.WriteLine("Mouse Last Y: " + mouseData.Mouse.LastY);
                }
            }

            base.WndProc(ref m);
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

            // Wait until a key is pressed (WndProc is called)  

            // Set a string variable to the key pressed  

            // Update the button text to the key pressed  

            // Re-enable the button  
            btnHotkey.Enabled = true;
        }
    }
}
