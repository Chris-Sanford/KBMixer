using KBMixer.Diag;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var sb = new StringBuilder();

var enumerator = new MMDeviceEnumerator();
var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

sb.AppendLine("==============================");
sb.AppendLine("  KBMixer WASAPI Session Dump");
sb.AppendLine($"  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
sb.AppendLine("==============================");
sb.AppendLine();

foreach (MMDevice dev in devices)
{
    sb.AppendLine($"DEVICE: {dev.FriendlyName}");
    sb.AppendLine($"  ID: {dev.ID}");

    SessionCollection sessions;
    try
    {
        sessions = dev.AudioSessionManager.Sessions;
    }
    catch (Exception ex)
    {
        sb.AppendLine($"  *** Cannot get sessions: {ex.Message}");
        sb.AppendLine();
        continue;
    }

    sb.AppendLine($"  Session count: {sessions.Count}");
    sb.AppendLine();

    for (int i = 0; i < sessions.Count; i++)
    {
        AudioSessionControl s;
        try
        {
            s = sessions[i];
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  [{i}] *** ERROR reading session: {ex.Message}");
            continue;
        }

        sb.AppendLine($"  [{i}] ─────────────────────────────────");

        try
        {
            sb.AppendLine($"       PID              : {s.GetProcessID}");
            sb.AppendLine($"       DisplayName       : \"{s.DisplayName}\"");
            sb.AppendLine($"       State             : {s.State}");
            sb.AppendLine($"       IsSystemSounds    : {s.IsSystemSoundsSession}");
            sb.AppendLine($"       Volume            : {s.SimpleAudioVolume.Volume:P0}");
            sb.AppendLine($"       Muted             : {s.SimpleAudioVolume.Mute}");

            string? instanceId = null;
            try { instanceId = s.GetSessionInstanceIdentifier; } catch { }
            sb.AppendLine($"       InstanceId        : \"{instanceId}\"");

            uint pid = s.GetProcessID;
            if (pid != 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    sb.AppendLine($"       Process.Name      : {proc.ProcessName}");
                    try { sb.AppendLine($"       Process.MainModule: {proc.MainModule?.FileName}"); }
                    catch { sb.AppendLine($"       Process.MainModule: <access denied>"); }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"       Process            : <not found: {ex.Message}>");
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"       *** Error reading properties: {ex.Message}");
        }

        sb.AppendLine();
    }

    sb.AppendLine();
}

string outPath = Path.Combine(Path.GetTempPath(), "KBMixer-diag.txt");
File.WriteAllText(outPath, sb.ToString());
Console.WriteLine(sb.ToString());
Console.WriteLine($"\nWritten to: {outPath}");

Console.WriteLine("\n");
Validate.Run();

Console.WriteLine("\n");
VolumeTest.Run();
