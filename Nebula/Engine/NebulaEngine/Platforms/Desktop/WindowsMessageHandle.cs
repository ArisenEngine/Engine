using System.Runtime.InteropServices;
using NebulaEngine.Graphics;

namespace NebulaEngine.Platforms;

internal class WindowsMessageHandle : MessageHandler
{
    private Win32Native.WndProc m_WndProc;
    public WindowsMessageHandle(IRenderSurface renderSurface): base(renderSurface)
    {
        m_WndProc = new Win32Native.WndProc(WindowProc);
        m_CallbackPtr = Marshal.GetFunctionPointerForDelegate(m_WndProc);
    }

    private IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32Native.WM_SIZE:
                OnResizing();
                break;
            case Win32Native.WM_EXITSIZEMOVE:
                OnResized();
                break;
        }
        return IntPtr.Zero;
    }

  

    protected override void OnResizing()
    {
        m_RenderSurface?.OnResizing();
    }

    protected override void OnResized()
    {
        m_RenderSurface?.OnResized();
    }

    protected override void OnCreate()
    {
        
    }

    protected override void OnDestroy()
    {
        throw new NotImplementedException();
    }

    public override bool NextFrame()
    {
        Win32Native.NativeMessage msg;
        
        bool isAlive = false;
        
        while (isAlive && Win32Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, Win32Native.PM_REMOVE) != 0)
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

        return isAlive && m_RenderSurface.IsValid();
    }
}