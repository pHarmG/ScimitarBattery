using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// P/Invoke declarations and types for the Corsair iCUE SDK on macOS (libiCUESDK.dylib).
/// </summary>
internal static class MacCorsairNative
{
    private const string DllName = "iCUESDK";
    private const int CORSAIR_STRING_SIZE_M = 128;
    private static readonly string? SdkBasePath = ResolveSdkBasePath();
    private static int _resolverRegistered;

    public static void RegisterResolver()
    {
        if (Interlocked.Exchange(ref _resolverRegistered, 1) == 1)
            return;

        try
        {
            NativeLibrary.SetDllImportResolver(typeof(MacCorsairNative).Assembly, ResolveDllImport);
            MacCorsairLog.Write("DllImport resolver registered.");
        }
        catch (InvalidOperationException)
        {
            // Resolver can already be set by another startup path in the same assembly.
            MacCorsairLog.Write("DllImport resolver already registered.");
        }
    }

    internal static IntPtr ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        MacCorsairLog.Write($"ResolveDllImport: requested={libraryName}");
        if (!string.Equals(libraryName, DllName, StringComparison.Ordinal))
            return IntPtr.Zero;

        var candidates = BuildCandidates();
        MacCorsairLog.Write($"ResolveDllImport: baseDir={AppContext.BaseDirectory} sdkBase={SdkBasePath ?? "null"} candidates={candidates.Count}");

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;
            try
            {
                var handle = NativeLibrary.Load(path);
                MacCorsairLog.Write($"Loaded SDK dylib: {path}");
                return handle;
            }
            catch (Exception ex)
            {
                MacCorsairLog.Write($"Failed to load SDK dylib: {path} ({ex.GetType().Name})");
            }
        }

        MacCorsairLog.Write("Failed to resolve iCUESDK library from known paths.");
        return IntPtr.Zero;
    }

    public static bool TryLoadSdk(out string? error)
    {
        error = null;
        var candidates = BuildCandidates();
        MacCorsairLog.Write($"TryLoadSdk: candidates={candidates.Count}");
        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;
            try
            {
                NativeLibrary.Load(path);
                MacCorsairLog.Write($"TryLoadSdk: loaded {path}");
                return true;
            }
            catch (Exception ex)
            {
                MacCorsairLog.Write($"TryLoadSdk: failed {path} ({ex.GetType().Name})");
                error = ex.Message;
            }
        }
        return false;
    }

    private static List<string> BuildCandidates()
    {
        var candidates = new List<string>();
        if (SdkBasePath != null)
        {
            candidates.Add(Path.Combine(SdkBasePath, "iCUESDK.framework", "iCUESDK"));
            candidates.Add(Path.Combine(SdkBasePath, "libiCUESDK.dylib"));
        }

        var baseDir = AppContext.BaseDirectory;
        // When framework contents are copied into output without the container folder,
        // the executable lands directly at "<baseDir>/iCUESDK".
        candidates.Add(Path.Combine(baseDir, "iCUESDK"));
        candidates.Add(Path.Combine(baseDir, "iCUESDK.framework", "iCUESDK"));
        candidates.Add(Path.Combine(baseDir, "libiCUESDK.dylib"));
        candidates.Add(Path.Combine(baseDir, "runtimes", "osx", "native", "libiCUESDK.dylib"));

        return candidates.Distinct().ToList();
    }

    private static string? ResolveSdkBasePath()
    {
        try
        {
            var assemblyDir = Path.GetDirectoryName(typeof(MacCorsairNative).Assembly.Location);
            if (!string.IsNullOrWhiteSpace(assemblyDir))
            {
                var candidate = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "ScimitarBattery.Adapters.MacOS", "SDK"));
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // Ignore.
        }

        var repoCandidate = "/Users/gennadykutovoy/AppDev/ScimitarBattery/ScimitarBattery.Adapters.MacOS/SDK";
        if (Directory.Exists(repoCandidate))
            return repoCandidate;

        var appCandidate = Path.Combine(AppContext.BaseDirectory, "SDK");
        if (Directory.Exists(appCandidate))
            return appCandidate;

        return null;
    }

    [Flags]
    public enum CorsairDeviceType : int
    {
        CDT_Unknown = 0x00000000,
        CDT_Keyboard = 0x00000001,
        CDT_Mouse = 0x00000002,
        CDT_Mousemat = 0x00000004,
        CDT_Headset = 0x00000008,
        CDT_HeadsetStand = 0x00000010,
        CDT_FanLedController = 0x00000020,
        CDT_LedController = 0x00000040,
        CDT_MemoryModule = 0x00000080,
        CDT_Cooler = 0x00000100,
        CDT_Motherboard = 0x00000200,
        CDT_GraphicsCard = 0x00000400,
        CDT_Touchbar = 0x00000800,
        CDT_GameController = 0x00001000,
        CDT_All = unchecked((int)0xFFFFFFFF)
    }

    public enum CorsairDevicePropertyId : int
    {
        CDPI_BatteryLevel = 9
    }

    public enum CorsairAccessLevel : int
    {
        CAL_Shared = 0,
        CAL_ExclusiveLightingControl = 1,
        CAL_ExclusiveKeyEventsListening = 2,
        CAL_ExclusiveLightingControlAndKeyEventsListening = 3
    }

    public enum CorsairDataType : int
    {
        CT_Boolean = 0,
        CT_Int32 = 1,
        CT_Float64 = 2,
        CT_String = 3,
        CT_Boolean_Array = 16,
        CT_Int32_Array = 17,
        CT_Float64_Array = 18,
        CT_String_Array = 19
    }

    public enum CorsairError : int
    {
        CE_Success = 0,
        CE_NotConnected = 1,
        CE_NoControl = 2,
        CE_IncompatibleProtocol = 3,
        CE_InvalidArguments = 4,
        CE_InvalidOperation = 5,
        CE_DeviceNotFound = 6,
        CE_NotAllowed = 7
    }

    public enum CorsairSessionState : int
    {
        CSS_Invalid = 0,
        CSS_Closed = 1,
        CSS_Connecting = 2,
        CSS_Timeout = 3,
        CSS_ConnectionRefused = 4,
        CSS_ConnectionLost = 5,
        CSS_Connected = 6
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairDeviceFilter
    {
        public int deviceTypeMask;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct CorsairDeviceInfo
    {
        public CorsairDeviceType type;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CORSAIR_STRING_SIZE_M)]
        public string id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CORSAIR_STRING_SIZE_M)]
        public string serial;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CORSAIR_STRING_SIZE_M)]
        public string model;

        public int ledCount;
        public int channelCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairLedPosition
    {
        public int ledId;
        public double cx;
        public double cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairLedColor
    {
        public int ledId;
        public byte r;
        public byte g;
        public byte b;
        public byte a;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairDataType_Int32Array
    {
        public IntPtr items;
        public uint count;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairDataType_StringArray
    {
        public IntPtr items;
        public uint count;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CorsairDataValue
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.I1)]
        public bool boolean;

        [FieldOffset(0)]
        public int int32;

        [FieldOffset(0)]
        public double float64;

        [FieldOffset(0)]
        public IntPtr @string;

        [FieldOffset(0)]
        public CorsairDataType_Int32Array int32_array;

        [FieldOffset(0)]
        public CorsairDataType_StringArray string_array;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairProperty
    {
        public CorsairDataType type;
        public CorsairDataValue value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairVersion
    {
        public int major;
        public int minor;
        public int patch;
        public override string ToString() => $"{major}.{minor}.{patch}";
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairSessionDetails
    {
        public CorsairVersion clientVersion;
        public CorsairVersion serverVersion;
        public CorsairVersion serverHostVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorsairSessionStateChanged
    {
        public CorsairSessionState state;
        public CorsairSessionDetails details;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CorsairSessionStateChangedHandler(
        IntPtr context,
        ref CorsairSessionStateChanged eventData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CorsairAsyncCallback(
        IntPtr context,
        CorsairError error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CorsairConnect(
        CorsairSessionStateChangedHandler onStateChanged,
        IntPtr context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CorsairDisconnect();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CorsairGetSessionDetails(
        out CorsairSessionDetails details);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairGetDevices(
        ref CorsairDeviceFilter filter,
        int sizeMax,
        [Out] CorsairDeviceInfo[] devices,
        ref int size);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairGetDeviceInfo(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        out CorsairDeviceInfo deviceInfo);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairGetLedPositions(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        int sizeMax,
        [Out] CorsairLedPosition[] positions,
        ref int size);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairSetLedColors(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        int size,
        [In] CorsairLedColor[] colors);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairGetLedColors(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        int size,
        [In, Out] CorsairLedColor[] colors);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CorsairSetLedColorsFlushBufferAsync(
        CorsairAsyncCallback callback,
        IntPtr context);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CorsairSetLayerPriority(uint priority);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairRequestControl(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        CorsairAccessLevel accessLevel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairReleaseControl(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int CorsairReadDeviceProperty(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        CorsairDevicePropertyId propertyId,
        uint index,
        out CorsairProperty property);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int CorsairFreeProperty(ref CorsairProperty property);
}
