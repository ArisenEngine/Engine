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
            var instHandle = RHILoaderAPI.RHILoader_CreateInstance(
                appName, "ArisenEngine", validationLayer ? 1 : 0,
                0, 1, 3, 0, // Variant, Major, Minor, Patch (Vulkan 1.3)
                1, 0, 0,    // App version
                1, 0, 0,    // Engine version
                2           // Max frames in flight
            );

            if (instHandle == IntPtr.Zero)
                return false;

            m_Instance = new RHIInstance(instHandle);

            // 3. Initialization logic
            m_Instance.PickPhysicalDevice(false);
            // We can choose to initialize a default device here if needed,
            // or let the user/test framework do it via Instance.CreateDevice().
            
            return true;
        }
        catch (Exception)
        {
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
    }
}
