using System.Runtime.CompilerServices;

namespace ScimitarBattery.Adapters.MacOS;

internal static class MacCorsairNativeLoader
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        MacCorsairNative.RegisterResolver();
    }
}
