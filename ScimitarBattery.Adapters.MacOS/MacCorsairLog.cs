using System.Text;

namespace ScimitarBattery.Adapters.MacOS;

internal static class MacCorsairLog
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ScimitarBattery", "mac-icue.log");

    public static void Write(string message)
    {
        try
        {
            var folder = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line, Encoding.UTF8);
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
