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
        private int m_Width;
        private int m_Height;
        private GameInstance m_GameInstance;
        public string Name;
        
        public RenderSurfaceHost(int width, int height)
        {
            m_Width = width;
            m_Height = height;
        }

        
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            IntPtr host = parent.Handle;
            m_GameInstance = new GameInstance(host, Name, m_Width, m_Height);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                m_GameInstance.Run();
                m_GameInstance.Dispose();
                
            }, DispatcherPriority.Render);
            
            return new PlatformHandle(m_GameInstance, "");
        }

      
    
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            
        }

        public void Dispose()
        {
            Debug.WriteLine("############# RenderSurfaceHost Dispose #################");
            
        }

    }
}
