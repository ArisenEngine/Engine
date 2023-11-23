using System.Runtime.InteropServices;
using System.Text;

namespace NebulaEngine.API
{
    public static class Debugger
    {
        private const string ENGINE_DLL = @"Engine.dll";

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Log", CharSet = CharSet.Unicode)]
        public static extern void Debugger_Log(string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Info", CharSet = CharSet.Unicode)]
        public static extern void Debugger_Info(string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Trace", CharSet = CharSet.Unicode)]
        public static extern void Debugger_Trace(string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Warning", CharSet = CharSet.Unicode)]
        public static extern void Debugger_Warning(string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Error", CharSet = CharSet.Unicode)]
        public static extern void Debugger_Error(string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName);

        [DllImport(ENGINE_DLL, EntryPoint = @"Debugger_Fatal", CharSet = CharSet.Unicode)]
        public static extern void Debugger_Fatal(string msg, [MarshalAs(UnmanagedType.LPStr)] string threadName);
    }

}

