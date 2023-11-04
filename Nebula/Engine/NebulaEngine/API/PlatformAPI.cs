using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEngine.API
{
    public static class PlatformAPI
    {
        private const string ENGINE_DLL = "Engine.dll";

        [DllImport(ENGINE_DLL, EntryPoint = "CreateRenderSurface")]
        public static extern int CreateRenderSurface(IntPtr host, int width, int height);

        [DllImport(ENGINE_DLL, EntryPoint = "RemoveRenderSurface")]
        public static extern void RemoveRenderSurface(int surfaceId);

        [DllImport(ENGINE_DLL, EntryPoint = "ResizeRenderSurface")]
        public static extern void ResizeRenderSurface(int surfaceId);

        [DllImport(ENGINE_DLL, EntryPoint = "GetWindowHandle")]
        public static extern IntPtr GetWindowHandle(int surfaceId);


    }
}
