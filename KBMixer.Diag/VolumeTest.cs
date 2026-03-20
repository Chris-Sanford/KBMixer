using KBMixer;
using NAudio.CoreAudioApi;
using System.Text.Json;

namespace KBMixer.Diag;

public static class VolumeTest
{
    public static void Run()
    {
        Console.WriteLine("=== Volume Control E2E Test ===\n");

        string cfgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KBMixer");
        Config? discordCfg = null;
        foreach (var f in Directory.GetFiles(cfgDir, "*.json"))
        {
            try
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(f));
                if (c != null && c.AppFileName.Contains("Discord", StringComparison.OrdinalIgnoreCase))
                { discordCfg = c; break; }
            }
            catch { }
        }

        if (discordCfg == null) { Console.WriteLine("No Discord config."); return; }

        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var allSessions = new List<(AudioSessionControl session, string deviceName)>();
        foreach (MMDevice dev in devices)
        {
            var sessions = Audio.CollectSessionsForConfig(dev, discordCfg);
            foreach (var s in sessions) allSessions.Add((s, dev.FriendlyName));
        }
        allSessions.Sort((a, b) => a.session.GetProcessID.CompareTo(b.session.GetProcessID));

        if (allSessions.Count < 2)
        {
            Console.WriteLine($"Only {allSessions.Count} session(s) — need 2 for this test.");
            return;
        }

        // Record original volumes
        float[] origVols = allSessions.Select(s => s.session.SimpleAudioVolume.Volume).ToArray();
        Console.WriteLine("Original volumes:");
        for (int i = 0; i < allSessions.Count; i++)
            Console.WriteLine($"  [{i}] PID={allSessions[i].session.GetProcessID} Vol={origVols[i]:P0} ({allSessions[i].deviceName})");

        // Test 1: Single-session mode — adjust only index 1
        Console.WriteLine("\n--- Test 1: Adjust ONLY index 1 down by 5% ---");
        var sessionList = allSessions.Select(e => e.session).ToList();
        Audio.AdjustSessionsVolume(sessionList, false, 1);
        float v0_after = allSessions[0].session.SimpleAudioVolume.Volume;
        float v1_after = allSessions[1].session.SimpleAudioVolume.Volume;
        Console.WriteLine($"  [{0}] Vol now: {v0_after:P0}  (was {origVols[0]:P0})  changed={Math.Abs(v0_after - origVols[0]) > 0.001f}");
        Console.WriteLine($"  [{1}] Vol now: {v1_after:P0}  (was {origVols[1]:P0})  changed={Math.Abs(v1_after - origVols[1]) > 0.001f}");
        bool test1Pass = Math.Abs(v0_after - origVols[0]) < 0.001f && Math.Abs(v1_after - origVols[1]) > 0.04f;
        Console.WriteLine($"  {(test1Pass ? "✓ PASS" : "✗ FAIL")}: Index 0 unchanged, index 1 decreased");

        // Restore
        allSessions[1].session.SimpleAudioVolume.Volume = origVols[1];

        // Test 2: All-sessions mode — adjust all
        Console.WriteLine("\n--- Test 2: Adjust ALL down by 5% ---");
        Audio.AdjustSessionsVolume(sessionList, false, null);
        float v0_all = allSessions[0].session.SimpleAudioVolume.Volume;
        float v1_all = allSessions[1].session.SimpleAudioVolume.Volume;
        Console.WriteLine($"  [{0}] Vol now: {v0_all:P0}  (was {origVols[0]:P0})");
        Console.WriteLine($"  [{1}] Vol now: {v1_all:P0}  (was {origVols[1]:P0})");
        bool test2Pass = Math.Abs(v0_all - origVols[0]) > 0.04f && Math.Abs(v1_all - origVols[1]) > 0.04f;
        Console.WriteLine($"  {(test2Pass ? "✓ PASS" : "✗ FAIL")}: Both decreased");

        // Restore original volumes
        for (int i = 0; i < allSessions.Count; i++)
            allSessions[i].session.SimpleAudioVolume.Volume = origVols[i];
        Console.WriteLine("\nVolumes restored to original values.");
        Console.WriteLine(test1Pass && test2Pass ? "\n=== ALL VOLUME TESTS PASSED ===" : "\n=== SOME TESTS FAILED ===");
    }
}
