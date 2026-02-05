using System.Text.Json;

namespace ScimitarBattery.Core;

/// <summary>
/// Persists monitor settings to a JSON file. Config path is platform-agnostic (e.g. AppData on Windows).
/// </summary>
public static class SettingsStorage
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Config file path. On Windows: %AppData%\ScimitarBattery\settings.json.
    /// </summary>
    public static string GetConfigFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScimitarBattery");
        return Path.Combine(folder, "settings.json");
    }

    /// <summary>
    /// Loads saved settings. Returns null if file does not exist or is invalid (first run).
    /// </summary>
    public static MonitorSettings? Load()
    {
        try
        {
            var path = GetConfigFilePath();
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<MonitorSettings>(json, Options);
            if (settings == null)
                return null;

            settings.PollingIntervalSeconds = settings.PollingIntervalSeconds;
            settings.LowThresholdPercent = settings.LowThresholdPercent;
            settings.CriticalThresholdPercent = settings.CriticalThresholdPercent;
            if (!string.IsNullOrWhiteSpace(settings.LegacyBatteryColorMidHigh) ||
                !string.IsNullOrWhiteSpace(settings.LegacyBatteryColorMidLow))
            {
                if (string.Equals(settings.BatteryColorMid, "#FFFF00", StringComparison.OrdinalIgnoreCase))
                {
                    var midHigh = ParseColor(settings.LegacyBatteryColorMidHigh);
                    var midLow = ParseColor(settings.LegacyBatteryColorMidLow);
                    if (midHigh.HasValue && midLow.HasValue)
                    {
                        settings.BatteryColorMid = ToHex(
                            (byte)((midHigh.Value.r + midLow.Value.r) / 2),
                            (byte)((midHigh.Value.g + midLow.Value.g) / 2),
                            (byte)((midHigh.Value.b + midLow.Value.b) / 2));
                    }
                    else if (midHigh.HasValue)
                    {
                        settings.BatteryColorMid = ToHex(midHigh.Value.r, midHigh.Value.g, midHigh.Value.b);
                    }
                    else if (midLow.HasValue)
                    {
                        settings.BatteryColorMid = ToHex(midLow.Value.r, midLow.Value.g, midLow.Value.b);
                    }
                }

                settings.LegacyBatteryColorMidHigh = null;
                settings.LegacyBatteryColorMidLow = null;
            }
            return settings;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public static void Save(MonitorSettings settings)
    {
        try
        {
            var path = GetConfigFilePath();
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            string json = JsonSerializer.Serialize(settings, Options);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Caller may log; avoid throwing from persistence
        }
    }

    private static (byte r, byte g, byte b)? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        string s = hex.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal))
            s = s.Substring(1);
        if (s.Length != 6)
            return null;
        if (!byte.TryParse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r))
            return null;
        if (!byte.TryParse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g))
            return null;
        if (!byte.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return null;
        return (r, g, b);
    }

    private static string ToHex(byte r, byte g, byte b)
    {
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
