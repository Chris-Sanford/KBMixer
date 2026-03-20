using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.IO;

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
                AudioSessionControl session;
                try
                {
                    session = sessions[i];
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Skipping audio session {i}: {ex.Message}");
                    continue;
                }

                if (!TryGetAppIdentity(session, out string appFileName, out string appFriendlyName))
                    continue;

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

        /// <summary>
        /// Resolves the executable key and UI label for a session. The old instance-id substring
        /// around "%b" breaks for many modern apps (e.g. Chrome); prefer process id and display name.
        /// </summary>
        private static bool TryGetAppIdentity(AudioSessionControl session, out string appFileName, out string appFriendlyName)
        {
            appFileName = "";
            appFriendlyName = "";

            try
            {
                if (session.IsSystemSoundsSession)
                {
                    appFileName = systemSoundsId;
                    appFriendlyName = "System Sounds";
                    return true;
                }

                uint pid = session.GetProcessID;
                if (pid != 0)
                {
                    try
                    {
                        using Process proc = Process.GetProcessById((int)pid);
                        string? exePath = null;
                        try
                        {
                            exePath = proc.MainModule?.FileName;
                        }
                        catch
                        {
                            // Some sessions are elevated or protected; ProcessName still works for volume routing.
                        }

                        appFileName = !string.IsNullOrEmpty(exePath)
                            ? Path.GetFileName(exePath)
                            : EnsureExeSuffix(proc.ProcessName);

                        if (string.IsNullOrEmpty(appFileName))
                            return false;

                        string display = session.DisplayName ?? "";
                        appFriendlyName = !string.IsNullOrWhiteSpace(display)
                            ? display.Trim()
                            : Path.GetFileNameWithoutExtension(appFileName);
                        return true;
                    }
                    catch
                    {
                        // Process may have exited between enumeration and lookup.
                    }
                }

                string displayName = (session.DisplayName ?? "").Trim();
                if (TryParseExeFromSessionInstanceId(session.GetSessionInstanceIdentifier, out string parsedExe))
                {
                    appFileName = parsedExe;
                    appFriendlyName = !string.IsNullOrEmpty(displayName)
                        ? displayName
                        : Path.GetFileNameWithoutExtension(parsedExe);
                    return true;
                }

                if (!string.IsNullOrEmpty(displayName))
                {
                    appFileName = displayName;
                    appFriendlyName = displayName;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TryGetAppIdentity failed: {ex.Message}");
            }

            return false;
        }

        private static string EnsureExeSuffix(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return "";
            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName
                : processName + ".exe";
        }

        /// <summary>
        /// Pulls the .exe segment from a session instance id without relying on a "%b" marker
        /// (format varies by OS build and app packaging).
        /// </summary>
        private static bool TryParseExeFromSessionInstanceId(string? instanceId, out string exeName)
        {
            exeName = "";
            if (string.IsNullOrEmpty(instanceId))
                return false;

            int exeIdx = instanceId.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx < 0)
                return false;

            int segmentEnd = exeIdx + 4;
            int start = instanceId.LastIndexOf('\\', exeIdx);
            if (start < 0)
                start = instanceId.LastIndexOf('/', exeIdx);
            if (start < 0)
                start = instanceId.LastIndexOf('|', exeIdx);
            start++;

            if (start >= segmentEnd || start < 0)
                return false;

            exeName = instanceId.Substring(start, segmentEnd - start);
            return exeName.Length > 0;
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
