using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public static class RHISystem
{
    private static RHIInstance? m_Instance;
    private static RHIDevice? m_Device;

    public static RHIInstance? Instance => m_Instance;
    public static RHIDevice? Device => m_Device;

    public static bool Initialize(GraphicsAPI api, string appName = "ArisenApp", bool validationLayer = false)
    {
        try
        {
            // 1. Set the graphics API
            RHILoaderAPI.RHILoader_SetCurrentGraphicsAPI((int)api);

            // 2. Create Instance
            // Using Vulkan 1.3 defaults as in old code
            var instHandle = RHILoaderAPI.RHILoader_CreateInstance(
                appName, "ArisenEngine", validationLayer ? 1 : 0,
                0, 1, 3, 0, // Variant, Major, Minor, Patch (Vulkan 1.3)
                1, 0, 0,    // App version
                1, 0, 0,    // Engine version
                2           // Max frames in flight
            );

            if (instHandle == IntPtr.Zero)
            {
                // Diagnostics.Logger.Error($"[RHISystem] Failed to create RHI instance for {api}");
                return false;
            }

            m_Instance = new RHIInstance(instHandle);

            // 3. Initialization logic for physical devices
            m_Instance.PickPhysicalDevice(false);
            m_Instance.InitLogicDevices();

            // Diagnostics.Logger.Info($"[RHISystem] Successfully initialized {api} RHI");
            return true;
        }
        catch (Exception e)
        {
            // Diagnostics.Logger.Error($"[RHISystem] Exception during initialization: {e.Message}");
            return false;
        }
    }

    public static void Shutdown()
    {
        m_Device = null;
        if (m_Instance != null)
        {
            m_Instance.Dispose();
            m_Instance = null;
        }
        RHILoaderAPI.RHILoader_Dispose();
        // Diagnostics.Logger.Info("[RHISystem] RHI Shutdown completed");
    }

    public static void SetLogicDevice(RHIDevice device)
    {
        m_Device = device;
    }
}
