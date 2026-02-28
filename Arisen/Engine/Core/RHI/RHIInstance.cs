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

    internal void InitLogicDevices()
    {
        RHIInstanceAPI.RHIInstance_InitLogicDevices(Handle);
    }

    public void CreateSurface(uint windowId)
    {
        RHIInstanceAPI.RHIInstance_CreateSurface(Handle, windowId);
    }

    /// <summary>
    /// Creates a logic device for the picked physical device.
    /// In the future, this might allow selecting a specific physical device.
    /// </summary>
    public RHIDevice CreateDevice(uint windowId = 0)
    {
        RHIInstanceAPI.RHIInstance_CreateLogicDevice(Handle, windowId);
        var deviceHandle = RHIInstanceAPI.RHIInstance_GetLogicalDevice(Handle, windowId);
        return new RHIDevice(deviceHandle);
    }

    [Obsolete("Use CreateDevice instead")]
    public void CreateLogicDevice(uint windowId) => RHIInstanceAPI.RHIInstance_CreateLogicDevice(Handle, windowId);

    [Obsolete("Use CreateDevice instead")]
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
