using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace NebulaEngine.Rendering
{
     public class RenderSurfaceHost : NativeControlHost, IDisposable
    {
        private int m_Width;
        private int m_Height;
        public string Name;
        private SurfaceType m_SurfaceType;
        private IntPtr m_Parent;
        
        internal RenderSurfaceHost(int width, int height, SurfaceType surfaceType)
        {
            m_Width = width;
            m_Height = height;
            m_SurfaceType = surfaceType;
        }

        
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            m_Parent = parent.Handle;
            NebulaInstance.RegisterSurface(m_Parent, Name, m_SurfaceType, m_Width, m_Height);
            
            return new PlatformHandle(NebulaInstance.GetNativeHandle(m_Parent), m_SurfaceType + " Host");
        }

      
    
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            NebulaInstance.UnregisterSurface(m_Parent);
        }

        public void Dispose()
        {
            Debug.Logger.Log("############# RenderSurfaceHost Dispose #################");
            
        }

    }
}
