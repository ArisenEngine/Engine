﻿using System.Runtime.InteropServices;
using ArisenEngine.Debug;
using ArisenEngine.Rendering;

namespace ArisenEngine.Platforms;

internal class WindowsProcHandler : WindowProcessor
{
    private Win32Native.WndProc m_WndProc;
    
    private delegate void ResizeCallback(IntPtr hwnd, int width, int height);
    
    private ResizeCallback m_ResizeCallback;
    
    internal WindowsProcHandler(RenderSurface renderSurface): base(renderSurface)
    {
        m_WndProc = WindowProc;
        m_ResizeCallback = OnResizeDone;
        
        m_ProcPtr = Marshal.GetFunctionPointerForDelegate(m_WndProc);
     
        m_ResizeCallbackPtr = Marshal.GetFunctionPointerForDelegate(m_ResizeCallback);
        
    }

    private void OnResizeDone(IntPtr hwnd, int width, int height)
    {
        Console.WriteLine($"OnResizeDone hwnd:{hwnd}, width:{width}, height:{height}");
    }
    
    private IntPtr WindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32Native.WM_SYSCOMMAND:
                if ((wParam.ToInt32() & 0xFFF0) == Win32Native.SC_CLOSE)
                {
                    OnClose();
                }
                break;
            case Win32Native.WM_SIZE:
                OnResizing();
                break;
            case Win32Native.WM_EXITSIZEMOVE:
                OnResized();
                break;
            case Win32Native.WM_DESTROY:
                Win32Native.PostQuitMessage(0);
                OnDestroy();
                break;
        }
        return IntPtr.Zero;
    }

    protected override void OnResizing()
    {
        m_RenderSurface.OnResizing();
    }

    protected override void OnResized()
    {
       // TODO: fix callback not trigger bug
       m_RenderSurface.OnResized();
    }

    protected override void OnCreate()
    {
        m_RenderSurface.OnCreate();
    }

    protected override void OnDestroy()
    { 
        Logger.Log(" Windows Proc : OnDestroy "); 
        m_RenderSurface.OnDestroy();
    }

    protected override void OnClose()
    {
        Logger.Log(" Windows Proc : OnClose ");
        // m_RenderSurface.DisposeSurface();
    }
}