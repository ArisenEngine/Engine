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
        internal static extern int CreateRenderSurface(IntPtr host, IntPtr messageHandle, int width, int height);
        
        // TODO: implement in cpp file
        [DllImport(ENGINE_DLL, EntryPoint = "CreateFullScreenRenderSurface")]
        internal static extern int CreateFullScreenRenderSurface(IntPtr host, IntPtr messageHandle);

        [DllImport(ENGINE_DLL, EntryPoint = "RemoveRenderSurface")]
        internal static extern void RemoveRenderSurface(int surfaceId);

        [DllImport(ENGINE_DLL, EntryPoint = "ResizeRenderSurface")]
        internal static extern void ResizeRenderSurface(int surfaceId);

        [DllImport(ENGINE_DLL, EntryPoint = "GetWindowHandle")]
        internal static extern IntPtr GetWindowHandle(int surfaceId);


    }
}
