using System.Text;
using Linearstar.Windows.RawInput;

namespace RawGlobalInput
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            GetRawInputDevices();
        }

        private void GetRawInputDevices()
        {
            // Get the devices that can be handled with Raw Input.
            var devices = RawInputDevice.GetDevices();

            // Keyboards will be returned as RawInputKeyboard.
            var keyboards = devices.OfType<RawInputKeyboard>();

            // Mice will be RawInputMouse.
            var mice = devices.OfType<RawInputMouse>();

            // Populate information about these variables to the textbox in the GUI.
            var sb = new StringBuilder();
            sb.AppendLine("Devices:");
            foreach (var device in devices)
            {
                sb.AppendLine($"IsConnected: {device.IsConnected}");
                sb.AppendLine($"- {device.Handle}");
                sb.AppendLine($"- {device.DevicePath}");
            }

            sb.AppendLine("Keyboards:");
            foreach (var keyboard in keyboards)
            {
                sb.AppendLine($"- {keyboard.ProductName}");
                sb.AppendLine($"- {keyboard.DevicePath}");
            }

            sb.AppendLine("Mice:");
            foreach (var mouse in mice)
            {
                sb.AppendLine($"- {mouse.ProductName}");
            }

            richTextBox1.Text = sb.ToString();
        }
    }
}
