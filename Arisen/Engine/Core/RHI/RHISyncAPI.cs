using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Arisen.Native.RHI
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RHISubmitDescriptor_Bridge
    {
        public IntPtr WaitSwapChain;
        public IntPtr SignalSwapChain;
        public IntPtr PWaitSemaphores;
        public uint WaitSemaphoreCount;
        public IntPtr PSignalSemaphores;
        public uint SignalSemaphoreCount;
    }

    public static class RHISyncAPI
    {
        private const string DllName = "Core.RHI.dll";

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong RHIDevice_Submit(IntPtr dev, uint index, uint generation, IntPtr bridgeDesc);

        [SuppressUnmanagedCodeSecurity, DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void RHISurface_InitSwapChain(IntPtr surface);
    }
}
