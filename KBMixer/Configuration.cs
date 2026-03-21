using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text;

namespace KBMixer;

public class Config
{
    public Guid ConfigId { get; set; }
    public required string DeviceId { get; set; }
    public required string AppFileName { get; set; }
    public required string AppFriendlyName { get; set; }
    public required int[] Hotkeys { get; set; }
    public required bool ControlSingleSession { get; set; } = false;
    public required int ProcessIndex { get; set; } = 0;

    /// <summary>When true, hotkeys + wheel adjust this output device's master volume instead of a single app.</summary>
    public bool ControlDeviceMasterVolume { get; set; }

    /// <summary>When set, shown in the config list instead of the auto-generated name.</summary>
    public string? CustomDisplayName { get; set; }

    public string GetAutoDisplayName(string? deviceFriendlyName)
    {
        string keys = Hotkeys.Length == 0
            ? "(no hotkeys)"
            : string.Join(" + ", Hotkeys.Select(KeyDisplayNames.GetDisplayName));
        string dev = string.IsNullOrWhiteSpace(deviceFriendlyName) ? "(unknown device)" : deviceFriendlyName;

        if (ControlDeviceMasterVolume)
            return $"Control {dev} master volume with {keys}";

        string app = string.IsNullOrWhiteSpace(AppFriendlyName) ? "(no app)" : AppFriendlyName;
        return $"Control {app} with {keys} on {dev}";
    }

    public void SaveConfig()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string kbmixerPath = Path.Combine(appDataPath, "KBMixer");

        if (!Directory.Exists(kbmixerPath))
            Directory.CreateDirectory(kbmixerPath);

        string filePath = Path.Combine(kbmixerPath, $"{ConfigId}.json");
        string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, jsonString);
    }

    public void DeleteConfig()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string kbmixerPath = Path.Combine(appDataPath, "KBMixer");
        if (!Directory.Exists(kbmixerPath))
            return;
        string filePath = Path.Combine(kbmixerPath, $"{ConfigId}.json");
        if (File.Exists(filePath))
        {
            try { File.Delete(filePath); }
            catch (IOException ex) { Debug.WriteLine($"Failed to delete config: {ex.Message}"); }
        }
    }
}

public static class Configurations
{
    /// <summary>Non-null when the last <see cref="LoadConfigsFromDisk"/> encountered corrupt files.</summary>
    public static string? LastLoadError { get; private set; }

    public static Config[] LoadConfigsFromDisk()
    {
        LastLoadError = null;
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string kbmixerPath = Path.Combine(appDataPath, "KBMixer");

        if (!Directory.Exists(kbmixerPath))
            return Array.Empty<Config>();

        var configFiles = Directory.GetFiles(kbmixerPath, "*.json");
        var configs = new List<Config>();
        var failedConfigs = new List<(string FilePath, string JsonContent)>();

        foreach (var configFile in configFiles)
        {
            try
            {
                string jsonString = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<Config>(jsonString);
                if (config != null)
                    configs.Add(config);
                else
                    failedConfigs.Add((configFile, jsonString));
            }
            catch (Exception)
            {
                string jsonContent;
                try { jsonContent = File.ReadAllText(configFile); }
                catch { jsonContent = "[Unable to read file content]"; }
                failedConfigs.Add((configFile, jsonContent));
            }
        }

        if (failedConfigs.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Failed to load one or more configurations. These configurations will be deleted:");

            foreach (var (filePath, jsonContent) in failedConfigs)
            {
                sb.AppendLine($"\nFile: {Path.GetFileName(filePath)}");
                sb.AppendLine($"Content: {jsonContent}");
                try { File.Delete(filePath); }
                catch (Exception ex) { sb.AppendLine($"Failed to delete file: {ex.Message}"); }
            }

            LastLoadError = sb.ToString();
        }

        return configs.ToArray();
    }
}
