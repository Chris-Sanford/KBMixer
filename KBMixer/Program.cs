using System;
using System.Linq;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using System.Data;

class Program
{
    static void Main(string[] args)
    {
        // Initialize the device enumerator
        var enumerator = new MMDeviceEnumerator();

        // Enumerate the active (DeviceState.Active) audio output (DataFlow.Render) devices
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();

        // Print the index, ID, and name of each device
        Console.WriteLine("Active Audio Output Devices:");
        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"[{i}] Device ID: {devices[i].ID}");
            Console.WriteLine($"    Device Name: {devices[i].FriendlyName}");
        }

        // Prompt user to select a device by index and validate
        int deviceIndex;
        MMDevice selectedDevice = null;

        while (selectedDevice == null)
        {
            Console.Write("Enter the index of the device you want to control: ");
            if (!int.TryParse(Console.ReadLine(), out deviceIndex) || deviceIndex < 0 || deviceIndex >= devices.Count)
            {
                Console.WriteLine("Invalid device index.");
                continue;
            }

            selectedDevice = devices[deviceIndex];
        }

        Console.WriteLine("Selected device: " + selectedDevice.FriendlyName);

        // Get all Audio Sessions (applications playing audio) for the selected device
        var sessions = selectedDevice.AudioSessionManager.Sessions;

        // Print the index, ID, and display name of each session
        Console.WriteLine("Audio Sessions:");
        for (int i = 0; i < sessions.Count; i++)
        {
            Console.WriteLine(""); // Space out sessions for readability
            Console.WriteLine($"[{i}] Session Process ID: {sessions[i].GetProcessID}");
            Console.WriteLine($"    Session Display Name: {sessions[i].DisplayName}");
            Console.WriteLine($"    Volume: {sessions[i].SimpleAudioVolume.Volume}");
            Console.WriteLine($"    MasterPeakValue: {sessions[i].AudioMeterInformation.MasterPeakValue}");
            Console.WriteLine($"    Session Identifier: {sessions[i].GetSessionIdentifier}");
            Console.WriteLine($"    Session Instance Identifier: {sessions[i].GetSessionInstanceIdentifier}"); // Can use this to identify session/app/process based on exe name
            Console.WriteLine($"    State: {sessions[i].State}");
            Console.WriteLine($"    IconPath: {sessions[i].IconPath}"); // Could use this in the future in the GUI to show the icon of the application
        }

        // Prompt user to select a session and validate
        int sessionIndex;
        AudioSessionControl selectedSession = null;

        while (selectedSession == null)
        {
            Console.Write("Enter the index of the session you want to control: ");
            if (!int.TryParse(Console.ReadLine(), out sessionIndex) || sessionIndex < 0 || sessionIndex >= sessions.Count)
            {
                Console.WriteLine("Invalid session index.");
                continue;
            }

            selectedSession = sessions[sessionIndex];
        }

        Console.WriteLine(); // Space out selected session for readability
        Console.WriteLine("Selected session: ");
        Console.WriteLine($"    Session Process ID: {selectedSession.GetProcessID}");
        Console.WriteLine($"    Session Display Name: {selectedSession.DisplayName}");
        Console.WriteLine($"    Session Identifier: {selectedSession.GetSessionIdentifier}");
        Console.WriteLine($"    Session Instance Identifier: {selectedSession.GetSessionInstanceIdentifier}"); // Can use this to identify session/app/process based on exe name

        // Prompt user to enter a volume level to set for the selected session
        Console.Write("Enter the volume level (0-100) to set for the selected session: ");
        int volumeLevel;

        while (!int.TryParse(Console.ReadLine(), out volumeLevel) || volumeLevel < 0 || volumeLevel > 100)
        {
            Console.WriteLine("Invalid volume level. Please enter a value between 0 and 100.");
            Console.Write("Enter the volume level (0-100) to set for the selected session: ");
        }

        // Set the volume level for the selected session
        selectedSession.SimpleAudioVolume.Volume = volumeLevel / 100f;
    }
}
