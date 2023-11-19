using System;
using Avalonia.Controls;
using Avalonia.Platform;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using EnvDTE;
using NebulaEngine.API;

namespace NebulaEngine.Graphics
{
     public class RenderSurfaceHost : NativeControlHost, IDisposable
    {
        private readonly int m_Width = 800;
        private readonly int m_Height = 600;
        private IntPtr m_RenderSurfaceWindowHandle = IntPtr.Zero;
        
        public IntPtr Handle
        {
            get { return m_RenderSurfaceWindowHandle; }
        }

        public int SurfaceId { get; private set; }
        
        public Engine Engine { get; private set; }

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

            Engine = new Engine();
            SurfaceId = API.PlatformAPI.CreateRenderSurface(parent.Handle,  Engine.MessageHandle, m_Width, m_Height);
            // TODO: Assert the id is valid
            m_RenderSurfaceWindowHandle = API.PlatformAPI.GetWindowHandle(SurfaceId);
            Debug.Assert(m_RenderSurfaceWindowHandle != IntPtr.Zero);
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Task.Run((() =>
                {
                    var engineContext = new EngineContext(m_RenderSurfaceWindowHandle);
                    Engine.Run(engineContext);
                    Engine.Dispose();
                    
                }));

            }, DispatcherPriority.Background);
            
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
