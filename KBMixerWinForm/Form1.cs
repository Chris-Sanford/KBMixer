using NAudio.CoreAudioApi;

namespace KBMixerWinForm
{
    // the Form1 class is defined as a partial class to separate the auto-generated code
    // (created by the Windows Forms Designer) from the custom code defined here
    public partial class Form1 : Form
    {
        private MMDevice selectedDevice; // Store the selected device object in a class-level scope
        private AudioSessionControl selectedSession; // Store the selected session object in a class-level scope

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
                    appComboBox.Items.Add(sessions[i].GetSessionIdentifier);
                }
            }

            if (appComboBox.Items.Count > 0)
            {
                appComboBox.SelectedIndex = 0; // Set the selected app to the first one in the index
                selectedSession = selectedDevice.AudioSessionManager.Sessions[0]; // Set the selected session object
            }
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
            if (appComboBox.SelectedIndex >= 0)
            {
                if (selectedDevice != null)
                {
                    AudioSessionManager sessionManager = selectedDevice.AudioSessionManager;
                    var sessions = sessionManager.Sessions;

                    if (appComboBox.SelectedIndex < sessions.Count)
                    {
                        selectedSession = sessions[appComboBox.SelectedIndex]; // Update the selected session object
                    }
                }
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void VolumeUpButton_Click(object sender, EventArgs e)
        {
            if (selectedSession != null)
            {
                selectedSession.SimpleAudioVolume.Volume += 0.1f; // Increase the volume by 10%
            }
        }

        private void VolumeDownButton_Click(object sender, EventArgs e)
        {
            if (selectedSession != null)
            {
                selectedSession.SimpleAudioVolume.Volume -= 0.1f; // Decrease the volume by 10%
            }
        }
    }
}
