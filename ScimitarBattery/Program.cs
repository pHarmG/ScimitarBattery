using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;

namespace ScimitarBattery;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        InstallGlobalExceptionHandlers();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            ShowStartupError(ex);
        }
    }

    private static void InstallGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                ShowStartupError(ex);
            else
                ShowStartupError(new Exception("Unhandled exception: " + e.ExceptionObject));
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ShowStartupError(e.Exception);
            e.SetObserved();
        };
    }

    internal static void ShowStartupError(Exception ex)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScimitarBattery");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "startup-error.txt");
            var stamp = DateTimeOffset.Now.ToString("O");
            File.AppendAllText(path, $"[{stamp}] {ex}{Environment.NewLine}{Environment.NewLine}");
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
