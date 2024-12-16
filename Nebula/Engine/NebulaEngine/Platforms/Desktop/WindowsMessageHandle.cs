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
        
        bool isAlive = true;
        
        while (Win32Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, Win32Native.PM_REMOVE) != 0)
        {
            if (msg.msg == 130) // WM_NCDESTROY
            {
                isAlive = false;
            }
            else if (msg.msg == 0x0012) // WM_QUIT
            {
                isAlive = false;
            }
            
            Win32Native.TranslateMessage(ref msg);
            Win32Native.DispatchMessage(ref msg);
        }

        return isAlive;
    }
}