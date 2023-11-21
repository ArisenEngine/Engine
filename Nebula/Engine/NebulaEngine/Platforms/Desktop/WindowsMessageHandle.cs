using System.Runtime.InteropServices;
using NebulaEngine.Rendering;

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
            // NCDESTROY event?
            if (msg.msg == 130)
            {
                isAlive = false;
            }
            else
            {
                isAlive = true;
            }
                
            Win32Native.TranslateMessage(ref msg);
            Win32Native.DispatchMessage(ref msg);
        }

        return isAlive;
    }
}