using System;
using System.IO;
using System.Text.Json;

namespace ScimitarBattery
{
    /// <summary>
    /// Persists monitor settings to a JSON file in the user's AppData folder.
    /// </summary>
    internal static class SettingsStorage
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static string GetFilePath()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ScimitarBattery");
            return Path.Combine(folder, "settings.json");
        }

        /// <summary>
        /// Loads saved settings from disk. Returns null if the file does not exist or is invalid.
        /// </summary>
        internal static MonitorSettings? Load()
        {
            try
            {
                var path = GetFilePath();
                if (!File.Exists(path))
                    return null;

                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<MonitorSettings>(json, Options);
                if (settings == null)
                    return null;

                // Ensure clamped values are applied (deserializer may have set raw values)
                settings.PollingIntervalSeconds = settings.PollingIntervalSeconds;
                settings.AlertThresholdPercent = settings.AlertThresholdPercent;
                return settings;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the current settings to disk.
        /// </summary>
        internal static void Save(MonitorSettings settings)
        {
            try
            {
                var path = GetFilePath();
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                string json = JsonSerializer.Serialize(settings, Options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not save settings: {ex.Message}");
            }
        }
    }
}
