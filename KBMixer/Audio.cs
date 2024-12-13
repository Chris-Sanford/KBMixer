using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace KBMixer
{
    public class AudioApp // Object class to represnt an audio application controllable by KBMixer
    {
        public required string AppFriendlyName { get; set; }
        public required string AppFileName { get; set; }
        public List<AudioSessionControl> Sessions { get; set; }

        private readonly MMDeviceEnumerator deviceEnumerator;

        public AudioApp()
        {
            Sessions = new List<AudioSessionControl>();
            deviceEnumerator = new MMDeviceEnumerator();
            RefreshSessions();
        }

        // Method to refresh the list of audio sessions
        public void RefreshSessions()
        {
            Sessions.Clear();
            var device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            var sessionEnumerator = sessionManager.Sessions;

            for (int i = 0; i < sessionEnumerator.Count; i++)
            {
                var session = sessionEnumerator[i];
                if (session.GetSessionIdentifier == AppFileName)
                {
                    Sessions.Add(session);
                }
            }
        }

        // Method to set the volume of a specific session
        public void SetVolume(float volume)
        {
            RefreshSessions(); // Ensure we have the latest sessions
            foreach (var session in Sessions)
            {
                session.SimpleAudioVolume.Volume = volume;
            }
        }
    }

    // Class that contains methods to interact with the audio sessions
    // these are methods that are used to build AudioApp objects
    public static class Audio 
    {
        public const Single volumeIncrement = 0.05f;
        public const string up = "Up";
        public const string down = "Down";
        public const string systemSoundsId = "{6C26BA7D-F0B2-4225-B422-8168C5261E45}";

        //private MMDevice? selectedDevice; // Store the selected device object in a class-level scope
        //private string selectedAppName; // To control per-app
        //private string selectedSessionInstanceIdentifier; // To control per-session/process
        //private KBMixerSession[]? kbMixerSessions; // to control per-app
        //private SessionCollection naudioSessions; // to control per-session/process
        //private bool controlSingleAppProcess = false;
        //private int indexOfProcessToControl = 0;

        //private void PopulateAudioOutputSessions()
        //{
        //    appComboBox.Items.Clear();

        //    if (selectedDevice != null) // Access the selected device object
        //    {
        //        // Get the complete list of audio sessions from the selected device
        //        AudioSessionManager sessionManager = selectedDevice.AudioSessionManager;
        //        naudioSessions = sessionManager.Sessions;

        //        for (int i = 0; i < naudioSessions.Count; i++)
        //        {
        //            // get the app name of the session by getting the substring
        //            // between the last \ and the following %b from SessionInstanceIdentifier
        //            string SessionInstanceIdentifier = naudioSessions[i].GetSessionInstanceIdentifier;
        //            string appName = SessionInstanceIdentifier.Substring(
        //                SessionInstanceIdentifier.LastIndexOf(@"\") + 1,
        //                SessionInstanceIdentifier.IndexOf(@"%b") - SessionInstanceIdentifier.LastIndexOf(@"\") - 1);

        //            if (appName.Contains(systemSoundsId))
        //            {
        //                appName = "System Sounds";
        //            }

        //            Debug.WriteLine("App Name: " + appName);

        //            // Try to acquire an object from kbmixerSession whose appName equals the current appName
        //            var existingSession = kbMixerSessions?.FirstOrDefault(s => s.AppName == appName);

        //            // If the object exists, add the session to the Sessions array of that object
        //            if (existingSession != null)
        //            {
        //                var sessionsList = existingSession.Sessions.ToList();
        //                sessionsList.Add(naudioSessions[i]);
        //                existingSession.Sessions = sessionsList.ToArray();
        //            }
        //            else
        //            {
        //                var newSession = new KBMixerSession(appName, new AudioSessionControl[] { naudioSessions[i] });
        //                var kbMixerSessionsList = kbMixerSessions?.ToList() ?? new List<KBMixerSession>();
        //                kbMixerSessionsList.Add(newSession);
        //                kbMixerSessions = kbMixerSessionsList.ToArray();
        //            }
        //        }

        //        // Add each app name from the kbMixerSessions array to the appComboBox
        //        foreach (var session in kbMixerSessions)
        //        {
        //            appComboBox.Items.Add(session.AppName);
        //        }

        //        if (appComboBox.Items.Count > 0)
        //        {
        //            appComboBox.SelectedIndex = 0; // Set the selected app to the first one in the index
        //            selectedAppName = kbMixerSessions[0].AppName; // Set the selected app name
        //            selectedSessionInstanceIdentifier = naudioSessions[0].GetSessionInstanceIdentifier; // Set the selected session object
        //        }
        //    }
        //}



        //private void AdjustSessionVolume(string direction, float increment)
        //{
        //    Debug.WriteLine("Adjusting volume by: " + increment);
        //    if (appSelected)
        //    {
        //        // if controlSingleAppProcess is true, only adjust the volume of the selected session
        //        if (controlSingleAppProcess)
        //        {
        //            var session = naudioSessions[indexOfProcessToControl];
        //            if (session != null)
        //            {
        //                Debug.WriteLine("Adjusting volume of: " + session.GetSessionInstanceIdentifier);
        //                if (direction == up)
        //                {
        //                    // Using Math.Min to ensure volume can reach 0 or 100 even if increment results in going over/under
        //                    session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + increment);
        //                }
        //                else if (direction == down)
        //                {
        //                    session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - increment);
        //                }
        //            }
        //        }
        //        else // Adjust the volume of all sessions of the selected app
        //        {
        //            // Loop through all sessions of the selected app
        //            for (int i = 0; i < naudioSessions.Count; i++)
        //            {
        //                var session = naudioSessions[i];
        //                string sessionAppName = session.GetSessionInstanceIdentifier.Substring(
        //                    session.GetSessionInstanceIdentifier.LastIndexOf(@"\") + 1,
        //                    session.GetSessionInstanceIdentifier.IndexOf(@"%b") - session.GetSessionInstanceIdentifier.LastIndexOf(@"\") - 1);

        //                if (sessionAppName.Contains(systemSoundsId))
        //                {
        //                    sessionAppName = "System Sounds";
        //                }

        //                if (sessionAppName == selectedAppName)
        //                {
        //                    if (direction == up)
        //                    {
        //                        session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + increment);
        //                    }
        //                    else if (direction == down)
        //                    {
        //                        session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - increment);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        //public void PopulateAudioOutputDevices()
        //{
        //    MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
        //    MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        //    foreach (MMDevice device in devices)
        //    {
        //        deviceComboBox.Items.Add(device.FriendlyName);
        //        Debug.WriteLine("Device Audio Client: " + device.AudioClient);
        //        Debug.WriteLine("FriendlyName: " + device.FriendlyName);
        //        Debug.WriteLine("ID: " + device.ID);
        //        Debug.WriteLine("State: " + device.State);
        //        Debug.WriteLine("DataFlow: " + device.DataFlow);
        //        Debug.WriteLine("DeviceFriendlyName: " + device.DeviceFriendlyName);
        //        Debug.WriteLine("FriendlyName: " + device.FriendlyName);
        //        Debug.WriteLine("IconPath: " + device.IconPath);
        //        Debug.WriteLine("IsMuted: " + device.AudioEndpointVolume.Mute);
        //        Debug.WriteLine("MasterVolumeLevel: " + device.AudioEndpointVolume.MasterVolumeLevel);
        //        Debug.WriteLine("MasterVolumeLevelScalar: " + device.AudioEndpointVolume.MasterVolumeLevelScalar);
        //        Debug.WriteLine("MasterPeakValue: " + device.AudioMeterInformation.MasterPeakValue);
        //    }

        //    deviceComboBox.SelectedIndex = 0; // Set the selected device to the first one in the index
        //    selectedDevice = devices[0]; // Set the selected device object
        //    appSelected = true;
        //}


    }
}
