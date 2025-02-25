using System;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ArisenEngine.Rendering
{
     public class RenderSurfaceHost : NativeControlHost, IDisposable
    {
        public string Name;
        private SurfaceType m_SurfaceType;
        private IntPtr m_Parent;
        
        internal RenderSurfaceHost(int width, int height, SurfaceType surfaceType)
        {
            m_SurfaceType = surfaceType;
            
            this.Width = width;
            this.Height = height;
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            m_Parent = parent.Handle;
            ArisenInstance.RegisterSurface(m_Parent, Name, m_SurfaceType, (int)Width, (int)Height);
            
            return new PlatformHandle(ArisenInstance.GetNativeHandle(m_Parent), m_SurfaceType + " Host");
        }

      
    
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            ArisenInstance.UnregisterSurface(m_Parent);
        }

        public void Dispose()
        {
            Debug.Logger.Log("############# RenderSurfaceHost Dispose #################");
            
        }

        public void Resize(int width, int height)
        {
            
        }
    }
}
