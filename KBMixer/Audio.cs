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
        public required string AppFileName { get; set; } // For Audio Control Logic
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
                string appFileName = sessionInstanceIdentifier.Substring(
                    sessionInstanceIdentifier.LastIndexOf(@"\") + 1,
                    sessionInstanceIdentifier.IndexOf(@"%b") - sessionInstanceIdentifier.LastIndexOf(@"\") - 1);

                // Eventually update this to get a more friendly name for apps
                // or at the very least, remove the file extension
                string appFriendlyName = appFileName;

                if (sessionInstanceIdentifier.EndsWith(systemSoundsId))
                {
                    appFileName = systemSoundsId;
                    appFriendlyName = "System Sounds";
                }

                var existingApp = audioApps.FirstOrDefault(a => a.AppFileName == appFileName && a.DeviceId == device.ID);

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
                        AppFileName = appFileName,
                        Sessions = new List<AudioSessionControl> { session }
                    });
                }
            }

            // Debug write the fields of all audio apps
            foreach (var app in audioApps)
            {
                Debug.WriteLine($"DeviceId: {app.DeviceId}, AppFriendlyName: {app.AppFriendlyName}, AppFileName: {app.AppFileName}, SessionsCount: {app.Sessions.Count}");
            }

            return audioApps.ToArray();
        }

        public static AudioSessionControl GetAudioSessionControls(MMDevice device)
        {
            // MMDevice.AudioSessionManager.AudioSessionControl seems to be a class that exists for the purpose of allowing KBMixer
            // to control the main audio session of the device. It's not something that gets us information
            // on individual app session
            var sessionManager = device.AudioSessionManager;
            return sessionManager.AudioSessionControl;
        }
    }
}
