using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using System.Net.Mail;
using System;
using NebulaEngine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AvaloniaLib;
using static AvaloniaLib.Native.NativeAPI;
using NebulaEngine.API;

namespace NebulaEngine.Graphics
{
     public class RenderSurfaceHost : NativeControlHost, IDisposable
    {
        private readonly int m_Width = 800;
        private readonly int m_Height = 600;
        private IntPtr m_RenderSurfaceWindowHandle = IntPtr.Zero;

        //public WindowProc MessageHook;

        public IntPtr Handle
        {
            get { return m_RenderSurfaceWindowHandle; }
        }

        public int SurfaceId { get; private set; }

        public RenderSurfaceHost(double width, double height) 
        {
            m_Width = (int)width;
            m_Height = (int)height;
        }

        public void Resize()
        {
            Debug.WriteLine("Resized");
            PlatformAPI.ResizeRenderSurface(SurfaceId);
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            Debug.WriteLine("############ CreateNativeControlCore ##############");

            SurfaceId = API.PlatformAPI.CreateRenderSurface(parent.Handle, m_Width, m_Height);
            // TODO: Assert the id is valid
            m_RenderSurfaceWindowHandle = API.PlatformAPI.GetWindowHandle(SurfaceId);
            Debug.Assert(m_RenderSurfaceWindowHandle != IntPtr.Zero);

            //SetWindowLong(m_RenderSurfaceWindowHandle, GWL_WNDPROC, InternalWindowProc);

            return new PlatformHandle(m_RenderSurfaceWindowHandle, "");
        }

       // TODO: fix when application closed, this callback not be invoked
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            Debug.WriteLine("############ DestroyNativeControlCore ##############");
           
            base.DestroyNativeControlCore(control);
        }

        public void Dispose()
        {
            API.PlatformAPI.RemoveRenderSurface(SurfaceId);
        }



        //private IntPtr InternalWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
        //{
        //    if (MessageHook != null)
        //    {
        //        MessageHook.Invoke(hWnd, msg, wParam, lParam);

        //    }

        //    return IntPtr.Zero;
        //}

    }
}
