using Linearstar.Windows.RawInput;
using NAudio.CoreAudioApi;
using System.Diagnostics;

namespace KBMixerWinForm
{
    // the Form1 class is defined as a partial class to separate the auto-generated code
    // (created by the Windows Forms Designer) from the custom code defined here
    public partial class Form1 : Form
    {
        private const int WM_INPUT = 0x00FF;
        private const Single volumeIncrement = 0.05f;
        private const int mouseWheelUp = 120;
        private const int mouseWheelDown = -120;
        private const string mouseWheelButton = "MouseWheel";
        private const string keyUpCode = "Up";
        private const string up = "Up";
        private const string down = "Down";
        private const string systemSoundsId = "{6C26BA7D-F0B2-4225-B422-8168C5261E45}";

        // Consider building a constant dictionary with all keys from this class
        // to represent hotkey in user-friendly way
        // just take the values as seen in the descriptions of each key
        // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keys?view=windowsdesktop-9.0

        // ? means it's a nullable type (to resolve warnings)
        private MMDevice? selectedDevice; // Store the selected device object in a class-level scope
        private bool appSelected = false;
        private string selectedAppName; // To control per-app
        private string selectedSessionInstanceIdentifier; // To control per-session/process
        private KBMixerSession[]? kbMixerSessions; // to control per-app
        private SessionCollection naudioSessions; // to control per-session/process
        private int hotkey;
        private bool listeningForHotkeySet = false;
        private bool hotkeyHeld = false;
        private bool controlSingleAppProcess = false;
        private int indexOfProcessToControl = 0;

        public Form1()
        {
            InitializeComponent();
            PopulateAudioOutputDevices();
            PopulateAudioOutputSessions();
            RegisterRawInputDevices();
        }

        private void PopulateAudioOutputSessions()
        {
            appComboBox.Items.Clear();

            if (selectedDevice != null) // Access the selected device object
            {
                // Get the complete list of audio sessions from the selected device
                AudioSessionManager sessionManager = selectedDevice.AudioSessionManager;
                naudioSessions = sessionManager.Sessions;

                for (int i = 0; i < naudioSessions.Count; i++)
                {
                    // get the app name of the session by getting the substring
                    // between the last \ and the following %b from SessionInstanceIdentifier
                    string SessionInstanceIdentifier = naudioSessions[i].GetSessionInstanceIdentifier;
                    string appName = SessionInstanceIdentifier.Substring(
                        SessionInstanceIdentifier.LastIndexOf(@"\") + 1,
                        SessionInstanceIdentifier.IndexOf(@"%b") - SessionInstanceIdentifier.LastIndexOf(@"\") - 1);

                    if (appName.Contains(systemSoundsId))
                    {
                        appName = "System Sounds";
                    }

                    Debug.WriteLine("App Name: " + appName);

                    // Try to acquire an object from kbmixerSession whose appName equals the current appName
                    var existingSession = kbMixerSessions?.FirstOrDefault(s => s.AppName == appName);

                    // If the object exists, add the session to the Sessions array of that object
                    if (existingSession != null)
                    {
                        var sessionsList = existingSession.Sessions.ToList();
                        sessionsList.Add(naudioSessions[i]);
                        existingSession.Sessions = sessionsList.ToArray();
                    }
                    else
                    {
                        var newSession = new KBMixerSession(appName, new AudioSessionControl[] { naudioSessions[i] });
                        var kbMixerSessionsList = kbMixerSessions?.ToList() ?? new List<KBMixerSession>();
                        kbMixerSessionsList.Add(newSession);
                        kbMixerSessions = kbMixerSessionsList.ToArray();
                    }
                }

                // Add each app name from the kbMixerSessions array to the appComboBox
                foreach (var session in kbMixerSessions)
                {
                    appComboBox.Items.Add(session.AppName);
                }

                if (appComboBox.Items.Count > 0)
                {
                    appComboBox.SelectedIndex = 0; // Set the selected app to the first one in the index
                    selectedAppName = kbMixerSessions[0].AppName; // Set the selected app name
                    selectedSessionInstanceIdentifier = naudioSessions[0].GetSessionInstanceIdentifier; // Set the selected session object
                }
            }
        }

        private void RegisterRawInputDevices()
        {
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, Handle);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, Handle);
        }

        private void AdjustSessionVolume(string direction, float increment)
        {
            Debug.WriteLine("Adjusting volume by: " + increment);
            if (appSelected)
            {
                // if controlSingleAppProcess is true, only adjust the volume of the selected session
                if (controlSingleAppProcess)
                {
                    var session = naudioSessions[indexOfProcessToControl];
                    if (session != null)
                    {
                        Debug.WriteLine("Adjusting volume of: " + session.GetSessionInstanceIdentifier);
                        if (direction == up)
                        {
                            // Using Math.Min to ensure volume can reach 0 or 100 even if increment results in going over/under
                            session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + increment);
                        }
                        else if (direction == down)
                        {
                            session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - increment);
                        }
                    }
                }
                else // Adjust the volume of all sessions of the selected app
                {
                    // Loop through all sessions of the selected app
                    for (int i = 0; i < naudioSessions.Count; i++)
                    {
                        var session = naudioSessions[i];
                        string sessionAppName = session.GetSessionInstanceIdentifier.Substring(
                            session.GetSessionInstanceIdentifier.LastIndexOf(@"\") + 1,
                            session.GetSessionInstanceIdentifier.IndexOf(@"%b") - session.GetSessionInstanceIdentifier.LastIndexOf(@"\") - 1);

                        if (sessionAppName.Contains(systemSoundsId))
                        {
                            sessionAppName = "System Sounds";
                        }

                        if (sessionAppName == selectedAppName)
                        {
                            if (direction == up)
                            {
                                session.SimpleAudioVolume.Volume = Math.Min(1.0f, session.SimpleAudioVolume.Volume + increment);
                            }
                            else if (direction == down)
                            {
                                session.SimpleAudioVolume.Volume = Math.Max(0.0f, session.SimpleAudioVolume.Volume - increment);
                            }
                        }
                    }
                }
            }
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
                        //Debug.WriteLine("Hotkey pressed down: " + ((Keys)virtualKey).ToString());
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
                    if (hotkeyHeld && mouseData.Mouse.ButtonData == mouseWheelUp && appSelected)
                    {
                        AdjustSessionVolume(up, volumeIncrement);
                        //Debug.WriteLine("Mouse Buttons: " + mouseData.Mouse.Buttons); // MouseWheel is the scroll wheel  
                        //Debug.WriteLine("Mouse Button Data: " + mouseData.Mouse.ButtonData); // 120 is up, -120 is down  
                    }
                    else if (hotkeyHeld && mouseData.Mouse.ButtonData == mouseWheelDown && appSelected)
                    {
                        AdjustSessionVolume(down, volumeIncrement);
                    }
                    //Debug.WriteLine("Mouse Buttons: " + mouseData.Mouse.Buttons); // MouseWheel is the scroll wheel  
                    //Debug.WriteLine("Mouse Button Data: " + mouseData.Mouse.ButtonData); // 120 is up, -120 is down  
                    //Debug.WriteLine("Mouse Last X: " + mouseData.Mouse.LastX);
                    //Debug.WriteLine("Mouse Last Y: " + mouseData.Mouse.LastY);
                }
            }
            base.WndProc(ref m); // Continue processing the message as WndProc normally would
        }


        private void PopulateAudioOutputDevices()
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
            MMDeviceCollection devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (MMDevice device in devices)
            {
                deviceComboBox.Items.Add(device.FriendlyName);
                Debug.WriteLine("Device Audio Client: " + device.AudioClient);
                Debug.WriteLine("FriendlyName: " + device.FriendlyName);
                Debug.WriteLine("ID: " + device.ID);
                Debug.WriteLine("State: " + device.State);
                Debug.WriteLine("DataFlow: " + device.DataFlow);
                Debug.WriteLine("DeviceFriendlyName: " + device.DeviceFriendlyName);
                Debug.WriteLine("FriendlyName: " + device.FriendlyName);
                Debug.WriteLine("IconPath: " + device.IconPath);
                Debug.WriteLine("IsMuted: " + device.AudioEndpointVolume.Mute);
                Debug.WriteLine("MasterVolumeLevel: " + device.AudioEndpointVolume.MasterVolumeLevel);
                Debug.WriteLine("MasterVolumeLevelScalar: " + device.AudioEndpointVolume.MasterVolumeLevelScalar);
                Debug.WriteLine("MasterPeakValue: " + device.AudioMeterInformation.MasterPeakValue);
            }

            deviceComboBox.SelectedIndex = 0; // Set the selected device to the first one in the index
            selectedDevice = devices[0]; // Set the selected device object
            appSelected = true;
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
            selectedAppName = kbMixerSessions[appComboBox.SelectedIndex].AppName; // Update the selected app name
            selectedSessionInstanceIdentifier = kbMixerSessions[appComboBox.SelectedIndex].Sessions[0].GetSessionInstanceIdentifier; // Update the selected session object
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

        private void checkBoxControlSingleAppProcess_CheckedChanged(object sender, EventArgs e)
        {
            controlSingleAppProcess = checkBoxControlSingleAppProcess.Checked;
            if (controlSingleAppProcess) {
                indexOfProcessToControl = (int)processIndexSelector.Value;
            }

            Debug.WriteLine("Control Single App Process: " + controlSingleAppProcess);
            Debug.WriteLine("Index of Process to Control: " + indexOfProcessToControl);
        }
    }
}
