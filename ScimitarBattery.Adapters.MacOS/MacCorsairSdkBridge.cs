namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// Public entry point for Corsair iCUE SDK connection on macOS.
/// </summary>
public static class MacCorsairSdkBridge
{
    private static MacCorsairNative.CorsairSessionStateChangedHandler? _handler;
    private static bool _connected;

    public static bool EnsureConnected(ref string? status)
    {
        MacCorsairNative.RegisterResolver();
        if (_connected)
            return true;

        _handler = OnSessionStateChanged;
        int err;
        try
        {
            if (!MacCorsairNative.TryLoadSdk(out var loadError))
            {
                status = "iCUE SDK not found. Ensure libiCUESDK.dylib is available.";
                if (!string.IsNullOrWhiteSpace(loadError))
                    MacCorsairLog.Write($"TryLoadSdk failed: {loadError}");
                return false;
            }
            err = MacCorsairNative.CorsairConnect(_handler, IntPtr.Zero);
        }
        catch (DllNotFoundException ex)
        {
            status = "iCUE SDK not found. Ensure libiCUESDK.dylib is next to the app.";
            MacCorsairLog.Write($"Connect failed: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            status = $"iCUE SDK connect failed: {ex.GetType().Name}";
            MacCorsairLog.Write($"Connect failed: {ex}");
            return false;
        }

        if (err == (int)MacCorsairNative.CorsairError.CE_Success)
        {
            _connected = true;
            status = null;
            MacCorsairLog.Write("Connect: success");
            return true;
        }

        status = err switch
        {
            (int)MacCorsairNative.CorsairError.CE_NotConnected =>
                "iCUE is not running or third-party control is disabled.",
            (int)MacCorsairNative.CorsairError.CE_IncompatibleProtocol =>
                "iCUE SDK protocol mismatch. Update iCUE.",
            _ => $"iCUE SDK connect failed (error {err})."
        };

        MacCorsairLog.Write($"Connect failed: err={err}");
        return false;
    }

    private static void OnSessionStateChanged(IntPtr ctx, ref MacCorsairNative.CorsairSessionStateChanged ev)
    {
        MacCorsairLog.Write($"Session state changed: {ev.state} (client={ev.details.clientVersion}, server={ev.details.serverVersion}, host={ev.details.serverHostVersion})");
    }
}
