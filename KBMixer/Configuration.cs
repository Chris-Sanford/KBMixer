using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Forms;

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
        public required string AppFriendlyName { get; set; }
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
            var failedConfigs = new List<(string FilePath, string JsonContent)>();

            foreach (var configFile in configFiles)
            {
                try
                {
                    string jsonString = File.ReadAllText(configFile);
                    var config = JsonSerializer.Deserialize<Config>(jsonString);
                    if (config != null)
                    {
                        configs.Add(config);
                    }
                    else
                    {
                        failedConfigs.Add((configFile, jsonString));
                    }
                }
                catch (Exception ex)
                {
                    // Capture the file path and content for failed deserialization
                    string jsonContent = string.Empty;
                    try
                    {
                        jsonContent = File.ReadAllText(configFile);
                    }
                    catch
                    {
                        jsonContent = "[Unable to read file content]";
                    }
                    failedConfigs.Add((configFile, jsonContent));
                }
            }

            // Handle failed configurations
            if (failedConfigs.Count > 0)
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Failed to load one or more configurations. These configurations will be deleted:");
                
                foreach (var (filePath, jsonContent) in failedConfigs)
                {
                    messageBuilder.AppendLine($"\nFile: {Path.GetFileName(filePath)}");
                    messageBuilder.AppendLine($"Content: {jsonContent}");
                    
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception deleteEx)
                    {
                        messageBuilder.AppendLine($"Failed to delete file: {deleteEx.Message}");
                    }
                }
                
                MessageBox.Show(messageBuilder.ToString(), "Configuration Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return configs.ToArray();
        }
    }
}