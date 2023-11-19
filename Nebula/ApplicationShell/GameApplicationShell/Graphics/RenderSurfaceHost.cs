using System;
using Avalonia.Controls;
using Avalonia.Platform;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

            SurfaceId = API.PlatformAPI.CreateRenderSurface(parent.Handle, Engine.MessageHandle != null ? Marshal.GetFunctionPointerForDelegate(Engine.MessageHandle) : IntPtr.Zero, m_Width, m_Height);
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
           
            API.PlatformAPI.RemoveRenderSurface(SurfaceId);  
            
        }

        public void Dispose()
        {
            Debug.WriteLine("############# RenderSurfaceHost Dispose #################");
        }
    }
}
