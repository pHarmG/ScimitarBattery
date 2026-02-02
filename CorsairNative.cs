using System;
using System.Runtime.InteropServices;

namespace ScimitarBattery
{
    /// <summary>
    /// P/Invoke declarations and types for the Corsair iCUE SDK (iCUESDK.x64_2019.dll).
    /// </summary>
    internal static class CorsairNative
    {
        private const string DllName = "iCUESDK.x64_2019.dll";
        private const int CORSAIR_STRING_SIZE_M = 128;

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
            CDT_All = unchecked((int)0xFFFFFFFF)
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

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CorsairSessionStateChangedHandler(
            IntPtr context,
            ref CorsairSessionStateChanged eventData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CorsairConnect(
            CorsairSessionStateChangedHandler onStateChanged,
            IntPtr context);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int CorsairGetDevices(
            ref CorsairDeviceFilter filter,
            int sizeMax,
            [Out] CorsairDeviceInfo[] devices,
            ref int size);

        public enum CorsairDevicePropertyId : int
        {
            CDPI_BatteryLevel = 9
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
            [FieldOffset(0)] public int int32;
            [FieldOffset(0)] public double float64;
            [FieldOffset(0)] public IntPtr @string;
            [FieldOffset(0)] public CorsairDataType_Int32Array int32_array;
            [FieldOffset(0)] public CorsairDataType_StringArray string_array;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CorsairProperty
        {
            public CorsairDataType type;
            public CorsairDataValue value;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int CorsairReadDeviceProperty(
            [MarshalAs(UnmanagedType.LPStr)] string deviceId,
            CorsairDevicePropertyId propertyId,
            uint index,
            out CorsairProperty property);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CorsairFreeProperty(ref CorsairProperty property);

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
    }
}
