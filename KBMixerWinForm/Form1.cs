using NAudio.CoreAudioApi;

namespace KBMixerWinForm
{
    // the Form1 class is defined as a partial class to separate the auto-generated code
    // (created by the Windows Forms Designer) from the custom code defined here
    public partial class Form1 : Form
    {
        private MMDevice selectedDevice; // Store the selected device object in a class-level scope

        public Form1()
        {
            InitializeComponent();
            PopulateAudioOutputDevices();
        }

        private void PopulateAudioOutputDevices()
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (MMDevice device in devices)
            {
                deviceComboBox.Items.Add(device.FriendlyName);
            }

            if (deviceComboBox.Items.Count > 0)
            {
                deviceComboBox.SelectedIndex = 0;
                selectedDevice = devices[0]; // Set the selected device object
                PopulateAudioOutputSessions();
            }
        }

        private void PopulateAudioOutputSessions()
        {
            appComboBox.Items.Clear();

            if (selectedDevice != null) // Access the selected device object
            {
                AudioSessionManager sessionManager = selectedDevice.AudioSessionManager;
                var sessions = sessionManager.Sessions;

                for (int i = 0; i < sessions.Count; i++)
                {
                    appComboBox.Items.Add(sessions[i].DisplayName);
                }
            }
        }

        private void deviceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            selectedDevice = devices[deviceComboBox.SelectedIndex]; // Update the selected device object
            PopulateAudioOutputSessions();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
