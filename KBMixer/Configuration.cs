using System.Diagnostics;
using System.Text.Json;

namespace KBMixer
{
    // KXMixerConfig represents the configuration of the KBMixer application.
    // It is designed to be serialized and deserialized to and from a JSON file
    // and saved and loaded from disk.
    public class Config
    {
        public Guid ConfigId { get; set; }
        public required string DeviceId { get; set; }
        public required string AppFileName { get; set; }
        public required int[] Hotkeys { get; set; }
        public required bool ControlSingleSession { get; set; } = false;
        public required int ProcessIndex { get; set; } = 0;

        public void SaveConfig()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string kbmixerPath = Path.Combine(appDataPath, "KBMixer");

            if (!Directory.Exists(kbmixerPath))
            {
                Directory.CreateDirectory(kbmixerPath);
            }

            string filePath = Path.Combine(kbmixerPath, $"{ConfigId}.json");
            string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(filePath, jsonString);
        }

        // Method for Deleting Configuration from Disk
        public void DeleteConfig()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string kbmixerPath = Path.Combine(appDataPath, "KBMixer");
            if (!Directory.Exists(kbmixerPath))
            {
                return;
            }
            string filePath = Path.Combine(kbmixerPath, $"{ConfigId}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    Debug.WriteLine($"Deleting config: {filePath}");
                    File.Delete(filePath);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"Failed to delete config: {ex.Message}");
                }
            }
        }
    }

    public static class Configurations
    {
        // Method for Loading Configurations From Disk
        public static Config[] LoadConfigsFromDisk()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string kbmixerPath = Path.Combine(appDataPath, "KBMixer");

            if (!Directory.Exists(kbmixerPath))
            {
                return Array.Empty<Config>();
            }

            var configFiles = Directory.GetFiles(kbmixerPath, "*.json");
            var configs = new List<Config>();

            foreach (var configFile in configFiles)
            {
                string jsonString = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<Config>(jsonString);
                if (config != null)
                {
                    configs.Add(config);
                }
            }

            return configs.ToArray();
        }
    }
}