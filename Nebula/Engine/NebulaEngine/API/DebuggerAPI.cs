using NebulaEngine.Debugger;
using System.Runtime.InteropServices;
using System.Text;

namespace NebulaEngine.API
{
    public static class Debugger
    {
        private const string ENGINE_DLL = @"Engine.dll";

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Log", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_Log([MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName, [MarshalAs(UnmanagedType.LPStr)] string csharpStackTrace);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Info", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_Info([MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName, [MarshalAs(UnmanagedType.LPStr)] string csharpStackTrace);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Trace", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_Trace([MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName, [MarshalAs(UnmanagedType.LPStr)] string csharpStackTrace);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Warning", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_Warning([MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName, [MarshalAs(UnmanagedType.LPStr)] string csharpStackTrace);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Error", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_Error([MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName, [MarshalAs(UnmanagedType.LPStr)] string csharpStackTrace);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Fatal", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_Fatal([MarshalAs(UnmanagedType.LPStr)] string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName, [MarshalAs(UnmanagedType.LPStr)] string csharpStackTrace);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_BindCallback", CharSet = CharSet.Unicode)]
        internal static extern void Debugger_BindCallback(Logger.OnLogReceived callback);
    }

}

