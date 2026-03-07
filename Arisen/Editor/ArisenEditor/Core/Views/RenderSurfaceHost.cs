using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine;
using ArisenEngine.Core.Diagnostics;

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
            ArisenApplication.RegisterSurface(m_Parent, Name, m_SurfaceType, (int)Width, (int)Height);
            
            return new PlatformHandle(IntPtr.Zero, m_SurfaceType + " Host"); // Placeholder if GetNativeHandle is missing
        }

      
    
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            ArisenApplication.UnregisterSurface(m_Parent);
        }

        public void Dispose()
        {
            Logger.Log($"############# RenderSurfaceHost Dispose:{m_SurfaceType} #################");
            
        }

        public void Resize(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }
    }
}
