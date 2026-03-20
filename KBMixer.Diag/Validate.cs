using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace KBMixer.Diag;

public static class Validate
{
    public static void Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== KBMixer Validation ===\n");

        // Load the user's Discord config
        string cfgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KBMixer");
        Config? discordCfg = null;
        foreach (var f in Directory.GetFiles(cfgDir, "*.json"))
        {
            try
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(f));
                if (c != null && c.AppFileName.Contains("Discord", StringComparison.OrdinalIgnoreCase))
                {
                    discordCfg = c;
                    break;
                }
            }
            catch { }
        }

        if (discordCfg == null)
        {
            Console.WriteLine("ERROR: No Discord config found.");
            return;
        }

        Console.WriteLine($"Config: AppFileName={discordCfg.AppFileName} FriendlyName={discordCfg.AppFriendlyName}");
        Console.WriteLine($"        DeviceId={discordCfg.DeviceId}");
        Console.WriteLine($"        ControlSingleSession={discordCfg.ControlSingleSession} ProcessIndex={discordCfg.ProcessIndex}");
        Console.WriteLine();

        // Enumerate all devices
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

        // Collect all matching sessions across ALL devices (same logic as Form1.CollectAllMatchingSessionsAcrossDevices)
        var allSessions = new List<(AudioSessionControl session, string deviceName, string deviceId)>();
        foreach (MMDevice dev in devices)
        {
            var sessions = Audio.CollectSessionsForConfig(dev, discordCfg);
            foreach (var s in sessions)
                allSessions.Add((s, dev.FriendlyName, dev.ID));
        }

        // Sort same way as Form1
        allSessions.Sort((a, b) =>
        {
            int c = a.session.GetProcessID.CompareTo(b.session.GetProcessID);
            return c != 0 ? c : string.CompareOrdinal(
                a.session.GetSessionInstanceIdentifier ?? "",
                b.session.GetSessionInstanceIdentifier ?? "");
        });

        Console.WriteLine($"Sessions found across ALL devices: {allSessions.Count}");
        Console.WriteLine();

        for (int i = 0; i < allSessions.Count; i++)
        {
            var (s, devName, devId) = allSessions[i];
            Console.WriteLine($"  [{i}] PID={s.GetProcessID}  Vol={s.SimpleAudioVolume.Volume:P0}  Device=\"{devName}\"");
        }
        Console.WriteLine();

        // === VALIDATION CHECKS ===
        bool pass = true;

        // Check 1: Should find at least 2 Discord sessions (one per HyperX endpoint)
        if (allSessions.Count >= 2)
            Console.WriteLine("✓ PASS: Found 2+ Discord sessions across devices");
        else
        {
            Console.WriteLine($"✗ FAIL: Expected 2+ Discord sessions, found {allSessions.Count}");
            pass = false;
        }

        // Check 2: Sessions should be on different devices (different PIDs)
        var pids = allSessions.Select(s => s.session.GetProcessID).Distinct().ToList();
        if (pids.Count >= 2)
            Console.WriteLine("✓ PASS: Sessions are from different PIDs");
        else if (allSessions.Count >= 2)
            Console.WriteLine("  INFO: Multiple sessions from same PID (same-process multi-stream)");
        else
            Console.WriteLine("  INFO: Only one session found");

        // Check 3: "All" mode should adjust every session
        Console.WriteLine("\n--- Simulating 'All' mode (ControlSingleSession=false) ---");
        Console.WriteLine($"  Would adjust {allSessions.Count} session(s) simultaneously");

        // Check 4: "Single" mode with index 0 should only adjust one
        Console.WriteLine("\n--- Simulating 'Single' mode (ControlSingleSession=true) ---");
        for (int idx = 0; idx < allSessions.Count; idx++)
        {
            var (s, devName, _) = allSessions[idx];
            Console.WriteLine($"  Index {idx}: Would adjust PID={s.GetProcessID} on \"{devName}\" (current vol={s.SimpleAudioVolume.Volume:P0})");
        }

        Console.WriteLine();
        Console.WriteLine(pass ? "=== ALL CHECKS PASSED ===" : "=== SOME CHECKS FAILED ===");
    }
}
