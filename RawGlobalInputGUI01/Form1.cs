using System.Diagnostics;
using Linearstar.Windows.RawInput;

namespace RawGlobalInputGUI01
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            GetRawInputDevices();
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

                Debug.WriteLine("Device Type: " + device.DeviceType);
            }
        }

        private void RegisterRawInputDevices()
        {
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, Handle);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, Handle);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;

            if (m.Msg == WM_INPUT)
            {
                var data = RawInputData.FromHandle(m.LParam);
                richTextBox1.AppendText("Input Type: " + data.Header.Type + "\n");
                richTextBox1.AppendText("Input Device Handle: " + data.Header.DeviceHandle + "\n");
                richTextBox1.AppendText("Input Size: " + data.Header.Size + "\n");
                richTextBox1.AppendText("Input Device: " + data.Device + "\n");
                richTextBox1.AppendText("\n");
                Debug.WriteLine("Input Type: " + data.Header.Type);
            }
            base.WndProc(ref m);
        }

        void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
