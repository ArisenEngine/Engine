using System.Runtime.InteropServices;

namespace NebulaEngine.API;

public class Debugger
{
    private const string ENGINE_DLL = "Engine.dll";
    
    [DllImport(ENGINE_DLL, EntryPoint = "Debugger_Log")]
    public static extern void Debugger_Log(string msg);
}