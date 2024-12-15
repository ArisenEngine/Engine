using System.Runtime.InteropServices;
using NebulaEngine.Debugger;
using NebulaEngine.Rendering;
using Logger = NebulaEngine.Debug.Logger;

namespace NebulaEngine.Platforms;

internal sealed class WindowsMessageHandle : MessageHandler
{
    
    public WindowsMessageHandle(): base()
    {
       
    }

    public override bool NextFrame()
    {
        Win32Native.NativeMessage msg;
        
        bool isAlive = false;
        
        while (Win32Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, Win32Native.PM_REMOVE) != 0)
        {
            isAlive = msg.msg != Win32Native.WM_QUIT;
            
            // ref from Stride Engine.
            isAlive = msg.msg != 130;
            
            Win32Native.TranslateMessage(ref msg);
            Win32Native.DispatchMessage(ref msg);
        }

        return isAlive;
    }
}