namespace ScimitarBattery.Adapters.Windows;

/// <summary>
/// Public entry point for Corsair iCUE SDK connection. Used by the host app on startup.
/// </summary>
public static class CorsairSdkBridge
{
    private static readonly object Sync = new();
    private static CorsairNative.CorsairSessionStateChangedHandler? _handler;
    private static bool _connected;

    /// <summary>
    /// Connects to iCUE. Returns 0 on success, non-zero error code on failure.
    /// </summary>
    public static int Connect()
    {
        lock (Sync)
        {
            if (_connected)
                return 0;

            _handler ??= OnSessionStateChanged;
            int err = CorsairNative.CorsairConnect(_handler, IntPtr.Zero);
            if (err == 0)
                _connected = true;
            return err;
        }
    }

    public static bool EnsureConnected(ref string? status)
    {
        if (Connect() == 0)
        {
            status = null;
            return true;
        }

        status = "iCUE SDK not available. Start iCUE and enable SDK in settings.";
        return false;
    }

    public static void InvalidateConnection()
    {
        lock (Sync)
        {
            _connected = false;
        }
    }

    private static void OnSessionStateChanged(IntPtr ctx, ref CorsairNative.CorsairSessionStateChanged ev)
    {
        lock (Sync)
        {
            _connected = ev.state == CorsairNative.CorsairSessionState.CSS_Connected;
        }
    }
}
