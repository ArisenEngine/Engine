using System;
using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI
{
    public static class RHILoader
    {
        public static void SetCurrentGraphicsAPI(GraphicsAPI api)
        {
            RHILoaderAPI.RHILoader_SetCurrentGraphicsAPI((int)api);
        }

        public static RHIInstance? CreateInstance(RHIInstanceInfo info)
        {
            IntPtr handle = RHILoaderAPI.RHILoader_CreateInstance(
                info.Name, info.EngineName, info.ValidationLayer ? 1 : 0,
                info.Variant, info.Major, info.Minor, info.Patch,
                info.AppMajor, info.AppMinor, info.AppPatch,
                info.EngineMajor, info.EngineMinor, info.EnginePatch,
                info.MaxFramesInFlight
            );

            if (handle == IntPtr.Zero) return null;
            return new RHIInstance(handle);
        }

        public static void Unload()
        {
            RHILoaderAPI.RHILoader_Dispose();
        }
    }
}