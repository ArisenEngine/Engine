using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEngine.API
{
    public static class RenderSurfaceAPI
    {
        private const string ENGINE_DLL = "Engine.dll";

        [DllImport(ENGINE_DLL, EntryPoint = "CreateRenderSurface")]
        public static extern int CreateRenderSurface(IntPtr host, IntPtr messageHandle, int width, int height);
        
        // TODO: implement in cpp file
        [DllImport(ENGINE_DLL, EntryPoint = "CreateFullScreenRenderSurface")]
        public static extern int CreateFullScreenRenderSurface(IntPtr host, IntPtr messageHandle);

        [DllImport(ENGINE_DLL, EntryPoint = "RemoveRenderSurface")]
        public static extern void RemoveRenderSurface(int surfaceId);

        [DllImport(ENGINE_DLL, EntryPoint = "ResizeRenderSurface")]
        public static extern void ResizeRenderSurface(int surfaceId);

        [DllImport(ENGINE_DLL, EntryPoint = "GetWindowHandle")]
        public static extern IntPtr GetWindowHandle(int surfaceId);


    }
}
