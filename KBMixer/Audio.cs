using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace KBMixer
{
    public class AudioDevice
    {
        public required MMDevice MMDevice { get; set; }
        public required AudioApp[] AudioApps { get; set; }
    }

    public class AudioApp // Object class to represnt an audio application controllable by KBMixer
    {
        // This should contain all of the fields/properties necessary
        // to enable interaction and manipulation of an audio application and its sessions
        // INCLUDING methods that can be called upon itself
        // such as modifying the volume of the app or its sessions
        // this should moreso represent what exists now and in memory
        // as opposed to what is stored in the config file that's meant to be loaded to in turn build this object\

        public const Single volumeIncrement = 0.05f;
        public const string up = "Up";
        public const string down = "Down";

        // AudioApps should be per-device:per-app rather than per-app regardless of device
        public required string DeviceId { get; set; }
        public required string AppFriendlyName { get; set; } // To display in GUI, esp. for System Sounds
        public required string AppFileName { get; set; } // In case of SystemSounds, GUID
        public required List<AudioSessionControl> Sessions { get; set; } // Collection of Sessions for this App

        public void AdjustVolume(bool isUp, int? processIndex = null)
        {
            // Adjust Volume of Specific Process
            if (processIndex.HasValue && processIndex.Value >= 0 && processIndex.Value < Sessions.Count)
            {
                var session = Sessions[processIndex.Value];
                if (isUp)
                {
                    session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + volumeIncrement);
                }
                else
                {
                    session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - volumeIncrement);
                }
            }
            else // Adjust Volume of All Processes for the Specified App
            {
                foreach (var session in Sessions)
                {
                    if (isUp)
                    {
                        session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + volumeIncrement);
                    }
                    else
                    {
                        session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - volumeIncrement);
                    }
                }
            }
        }
    }

    // Class that contains methods to interact with the audio sessions
    // these are methods that are used to build AudioApp objects
    public static class Audio 
    {
        public const string systemSoundsId = "%b#";

        public static AudioDevice[] GetAudioDevices()
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            List<AudioDevice> audioDevices = new List<AudioDevice>();
            foreach (MMDevice device in devices)
            {
                audioDevices.Add(new AudioDevice
                {
                    MMDevice = device,
                    AudioApps = GetAudioDeviceApps(device)
                });
            }
            return audioDevices.ToArray();
        }

        public static AudioApp[] GetAudioDeviceApps(MMDevice device)
        {
            List<AudioApp> audioApps = new List<AudioApp>();

            SessionCollection sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                string sessionInstanceIdentifier = session.GetSessionInstanceIdentifier;
                string appName = sessionInstanceIdentifier.Substring(
                    sessionInstanceIdentifier.LastIndexOf(@"\") + 1,
                    sessionInstanceIdentifier.IndexOf(@"%b") - sessionInstanceIdentifier.LastIndexOf(@"\") - 1);

                // Eventually update this to get a more friendly name for apps
                // or at the very least, remove the file extension
                string appFriendlyName = appName;

                if (sessionInstanceIdentifier.Contains(systemSoundsId))
                {
                    appName = systemSoundsId;
                    appFriendlyName = "System Sounds";
                }

                var existingApp = audioApps.FirstOrDefault(a => a.AppFileName == appName && a.DeviceId == device.ID);

                if (existingApp != null)
                {
                    existingApp.Sessions.Add(session);
                }
                else
                {
                    audioApps.Add(new AudioApp
                    {
                        DeviceId = device.ID,
                        AppFriendlyName = appFriendlyName,
                        AppFileName = appName,
                        Sessions = new List<AudioSessionControl> { session }
                    });
                }
            }

            return audioApps.ToArray();
        }

        public static AudioSessionControl GetAudioSessionControls(MMDevice device)
        {
            // MMDevice.AudioSessionManager.AudioSessionControl seems to be a class that exists for the purpose of allowing KBMixer
            // to control the main audio session of the device. It's not something that gets us information
            // on individual app session
            var sessionManager = device.AudioSessionManager;

            Debug.WriteLine("MMDevice.AudioSessionManager.AudioSessionControl.GetSessionInstanceIdentifier: " + sessionManager.AudioSessionControl.GetSessionInstanceIdentifier);


            return sessionManager.AudioSessionControl;
        }

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
