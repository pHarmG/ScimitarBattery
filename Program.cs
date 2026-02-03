using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace ScimitarBattery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    internal static void ShowStartupError(Exception ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScimitarBattery");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "startup-error.txt"), ex.ToString());
        }
        catch { }
        if (OperatingSystem.IsWindows())
            _ = MessageBoxWin32.MessageBoxW(IntPtr.Zero, ex.ToString(), "Scimitar Battery – startup error", 0x10);
    }

    private static class MessageBoxWin32
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
