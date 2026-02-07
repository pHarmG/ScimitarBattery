using System.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace ScimitarBattery.Adapters.MacOS;

public static class MacStartupManager
{
    private const string Label = "com.scimitarbattery.monitor";

    private static string LaunchAgentsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "LaunchAgents");

    private static string PlistPath => Path.Combine(LaunchAgentsDir, $"{Label}.plist");

    public static bool IsEnabled()
    {
        try
        {
            if (!File.Exists(PlistPath))
                return false;

            var doc = XDocument.Load(PlistPath);
            var dict = doc.Root?.Element("dict");
            if (dict == null)
                return false;

            // Basic validation to avoid stale/foreign plist files.
            string? label = ReadStringForKey(dict, "Label");
            return string.Equals(label, Label, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySetEnabled(bool enabled, out string? error)
    {
        error = null;
        try
        {
            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    error = "Unable to resolve app path for startup.";
                    return false;
                }

                Directory.CreateDirectory(LaunchAgentsDir);
                File.WriteAllText(PlistPath, BuildPlist(exePath), new UTF8Encoding(false));

                // Best-effort apply in current session; login behavior works regardless.
                TryLaunchCtl("unload", PlistPath);
                TryLaunchCtl("load", PlistPath);
            }
            else
            {
                TryLaunchCtl("unload", PlistPath);
                if (File.Exists(PlistPath))
                    File.Delete(PlistPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string BuildPlist(string exePath)
    {
        static string Escape(string s) =>
            s.Replace("&", "&amp;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal)
             .Replace("\"", "&quot;", StringComparison.Ordinal)
             .Replace("'", "&apos;", StringComparison.Ordinal);

        return $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{Escape(Label)}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{Escape(exePath)}</string>
        <string>--autostart</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <false/>
</dict>
</plist>
""";
    }

    private static void TryLaunchCtl(string verb, string path)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/launchctl",
                Arguments = $"{verb} \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit(1500);
        }
        catch
        {
            // Ignore launchctl failures; plist is still persisted for next login.
        }
    }

    private static string? ReadStringForKey(XElement dict, string key)
    {
        var nodes = dict.Elements().ToList();
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            if (nodes[i].Name.LocalName == "key" &&
                string.Equals(nodes[i].Value, key, StringComparison.Ordinal) &&
                nodes[i + 1].Name.LocalName == "string")
            {
                return nodes[i + 1].Value;
            }
        }

        return null;
    }
}
