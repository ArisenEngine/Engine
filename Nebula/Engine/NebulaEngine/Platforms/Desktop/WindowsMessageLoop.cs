using System.Net.Mime;

namespace NebulaEngine.Platforms;

internal class WindowsMessageLoop : IMessageLoop
{
    private IntPtr m_ControlHandle;
    private bool m_IsControlAlive;
    public WindowsMessageLoop(IntPtr handle)
    {
        m_ControlHandle = handle;
        m_IsControlAlive = true;
    }
    
    public void Dispose()
    {
        m_ControlHandle = IntPtr.Zero;
    }

    public bool NextFrame()
    {
        var localHandle = m_ControlHandle;
        
        if (localHandle != IntPtr.Zero)
        {
            // Previous code not compatible with Application.AddMessageFilter but faster then DoEvents
            Win32Native.NativeMessage msg;
            while (Win32Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, Win32Native.PM_REMOVE) != 0)
            {
                // NCDESTROY event?
                if (msg.msg == 130)
                {
                    m_IsControlAlive = false;
                }
                
                Win32Native.TranslateMessage(ref msg);
                Win32Native.DispatchMessage(ref msg);
            }

            return m_IsControlAlive;
        }

        return false;
    }
    
    internal static IntPtr InternalMessageHandle(IntPtr hwnd, int msg, IntPtr wparam,
        IntPtr lparam /*, ref bool handled*/)
    {
        switch (msg)
        {
            
        }

        return IntPtr.Zero;
    }
}