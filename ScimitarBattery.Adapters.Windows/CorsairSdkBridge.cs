namespace ScimitarBattery.Adapters.Windows;

/// <summary>
/// Public entry point for Corsair iCUE SDK connection. Used by the host app on startup.
/// </summary>
public static class CorsairSdkBridge
{
    private static CorsairNative.CorsairSessionStateChangedHandler? _handler;

    /// <summary>
    /// Connects to iCUE. Returns 0 on success, non-zero error code on failure.
    /// </summary>
    public static int Connect()
    {
        _handler = OnSessionStateChanged;
        return CorsairNative.CorsairConnect(_handler, IntPtr.Zero);
    }

    private static void OnSessionStateChanged(IntPtr ctx, ref CorsairNative.CorsairSessionStateChanged ev) { }
}
