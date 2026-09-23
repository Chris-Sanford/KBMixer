using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace KBMixer;

/// <summary>Identity of a live render endpoint used to re-bind profiles when endpoint ids rotate.</summary>
public sealed record DeviceIdentity(string Id, string FriendlyName, string? Description);

public enum DeviceReconcileResult
{
    /// <summary>Stored id is live and nothing needed updating.</summary>
    Unchanged,
    /// <summary>Stored id is live; missing name/description fields were filled in (save recommended).</summary>
    Backfilled,
    /// <summary>Stored id was gone but the same device was found by name/description and re-bound (save required).</summary>
    Rematched,
    /// <summary>Stored id is gone and no live device matches; caller should pick a fallback device.</summary>
    Orphaned
}

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

    /// <summary>Whether this profile has an app target or is configured to control the device master volume.</summary>
    [JsonIgnore]
    public bool HasTarget =>
        ControlDeviceMasterVolume ||
        !string.IsNullOrWhiteSpace(AppFileName) ||
        !string.IsNullOrWhiteSpace(AppFriendlyName);

    /// <summary>
    /// Friendly name of the output device at the time <see cref="DeviceId"/> was chosen. Windows endpoint IDs can
    /// rotate (driver reinstall, USB re-enumeration), so this lets us re-bind a profile to the same physical device.
    /// </summary>
    public string? DeviceFriendlyName { get; set; }

    /// <summary>
    /// Adapter / device-interface name of the output device (e.g. "HyperX Cloud Alpha S Game"). More stable than
    /// <see cref="DeviceFriendlyName"/>, which changes if the user renames the endpoint in Sound settings.
    /// </summary>
    public string? DeviceDescription { get; set; }

    /// <summary>Sets the endpoint id plus the stable identity fields so the profile can survive id rotation.</summary>
    public void SetDevice(string deviceId, string? deviceFriendlyName, string? deviceDescription = null)
    {
        DeviceId = deviceId;
        if (!string.IsNullOrWhiteSpace(deviceFriendlyName))
            DeviceFriendlyName = deviceFriendlyName;
        if (!string.IsNullOrWhiteSpace(deviceDescription))
            DeviceDescription = deviceDescription;
    }

    /// <summary>
    /// Re-point this profile at a live device when its stored endpoint id no longer exists.
    /// Match order: exact id → friendly name + description → friendly name → description (only if unique).
    /// </summary>
    public DeviceReconcileResult TryReconcileDevice(IReadOnlyList<DeviceIdentity> devices)
    {
        if (devices.Count == 0)
            return DeviceReconcileResult.Unchanged;

        var exact = devices.FirstOrDefault(d => string.Equals(d.Id, DeviceId, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
        {
            // Id is fine; opportunistically backfill identity fields for configs saved by older builds.
            bool changed = false;
            if (string.IsNullOrWhiteSpace(DeviceFriendlyName) && !string.IsNullOrWhiteSpace(exact.FriendlyName))
            {
                DeviceFriendlyName = exact.FriendlyName;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(DeviceDescription) && !string.IsNullOrWhiteSpace(exact.Description))
            {
                DeviceDescription = exact.Description;
                changed = true;
            }
            return changed ? DeviceReconcileResult.Backfilled : DeviceReconcileResult.Unchanged;
        }

        bool hasName = !string.IsNullOrWhiteSpace(DeviceFriendlyName);
        bool hasDesc = !string.IsNullOrWhiteSpace(DeviceDescription);
        if (!hasName && !hasDesc)
            return DeviceReconcileResult.Orphaned;

        DeviceIdentity? match = null;

        if (hasName && hasDesc)
            match = devices.FirstOrDefault(d => NameEquals(d.FriendlyName, DeviceFriendlyName) && NameEquals(d.Description, DeviceDescription));

        if (match == null && hasName)
            match = devices.FirstOrDefault(d => NameEquals(d.FriendlyName, DeviceFriendlyName));

        if (match == null && hasDesc)
        {
            var byDesc = devices.Where(d => NameEquals(d.Description, DeviceDescription)).ToList();
            if (byDesc.Count == 1)
                match = byDesc[0];
        }

        if (match == null)
            return DeviceReconcileResult.Orphaned;

        SetDevice(match.Id, match.FriendlyName, match.Description);
        return DeviceReconcileResult.Rematched;
    }

    static bool NameEquals(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a.Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    public string GetAutoDisplayName(string? deviceFriendlyName)
    {
        if (!HasTarget)
            return "New profile";

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
