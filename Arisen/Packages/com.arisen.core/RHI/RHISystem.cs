using System.Collections.Concurrent;
using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public static class RHISystem
{
    private static RHIInstance? m_Instance;
    private static readonly ConcurrentDictionary<uint, RHIDevice> m_DeviceWrappers = new();
    private static bool m_PhysicalDevicePicked = false;

    public static RHIInstance? Instance => m_Instance;
    public static RHIDevice GetOrCreateDevice(uint windowId)
    {
        if (m_Instance == null)
            throw new InvalidOperationException("RHISystem must be initialized before creating devices.");

        if (m_DeviceWrappers.TryGetValue(windowId, out var cachedDevice))
            return cachedDevice;

        if (!m_PhysicalDevicePicked)
        {
            m_Instance.Value.PickPhysicalDevice(true);
            m_PhysicalDevicePicked = true;
        }

        var device = m_Instance.Value.CreateDevice(windowId);
        m_DeviceWrappers.TryAdd(windowId, device);

        return device;
    }

    public static bool Initialize(GraphicsAPI api, string appName = "ArisenApp", bool validationLayer = false)
    {
        try
        {
            // 1. Set the graphics API
            RHILoaderAPI.RHILoader_SetCurrentGraphicsAPI((int)api);

            // 2. Create Instance
            var instHandle = RHILoaderAPI.RHILoader_CreateInstance(
                appName, "ArisenEngine", validationLayer ? 1 : 0,
                0, 1, 3, 0, // Variant, Major, Minor, Patch (Vulkan 1.3)
                1, 0, 0, // App version
                1, 0, 0, // Engine version
                2 // Max frames in flight
            );

            if (instHandle == IntPtr.Zero)
                return false;

            m_Instance = new RHIInstance(instHandle);

            // 3. Defer Physical Device picking until Surface is created (handled by user/test framework).
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Shutdown()
    {
        m_DeviceWrappers.Clear();

        if (m_Instance != null)
        {
            m_Instance = null;
        }

        RHILoaderAPI.RHILoader_Dispose();
    }
}