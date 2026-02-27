using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public class RHIInstance : IDisposable
{
    internal IntPtr Handle { get; }

    public RHIInstance(IntPtr handle)
    {
        Handle = handle;
    }

    public void PickPhysicalDevice(bool considerSurface)
    {
        RHIInstanceAPI.RHIInstance_PickPhysicalDevice(Handle, considerSurface ? 1 : 0);
    }

    public void InitLogicDevices()
    {
        RHIInstanceAPI.RHIInstance_InitLogicDevices(Handle);
    }

    public RHIDevice GetLogicalDevice(uint windowId)
    {
        var deviceHandle = RHIInstanceAPI.RHIInstance_GetLogicalDevice(Handle, windowId);
        return new RHIDevice(deviceHandle);
    }

    public void Dispose()
    {
        // Placeholder for instance cleanup if needed
    }
}
