using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public enum GraphicsAPI
{
    None = 0,
    Vulkan = 1,
    DX12 = 2
}

public static class Graphics
{
    private static NativeRHI.RHIInstance? m_Instance;
    private static NativeRHI.RHIDevice? m_Device;

    public static NativeRHI.RHIInstance? Instance => m_Instance;
    public static NativeRHI.RHIDevice? Device => m_Device;

    public static bool Initialize(GraphicsAPI api, string appName = "ArisenApp", bool validationLayer = false)
    {
        try
        {
            // 1. Set the graphics API
            NativeRHI.RHILoader.SetCurrentGraphicsAPI((NativeRHI.GraphicsAPI)api);

            // 2. Create Instance Info
            var info = new NativeRHI.RHIInstanceInfo
            {
                name = appName,
                engineName = "ArisenEngine",
                validationLayer = validationLayer,
                variant = 0, major = 1, minor = 3, patch = 0, // Vulkan 1.3
                appMajor = 1, appMinor = 0, appPatch = 0,
                engineMajor = 1, engineMinor = 0, enginePatch = 0,
                maxFramesInFlight = 2
            };

            // 3. Create Instance
            m_Instance = NativeRHI.RHILoader.CreateInstance(info);
            if (m_Instance == null)
            {
                Diagnostics.Logger.Error($"[Graphics] Failed to create RHI instance for {api}");
                return false;
            }

            // 4. Initialization logic for physical devices
            m_Instance.PickPhysicalDevice(false);
            m_Instance.InitLogicDevices();

            Diagnostics.Logger.Info($"[Graphics] Successfully initialized {api} RHI");
            return true;
        }
        catch (Exception e)
        {
            Diagnostics.Logger.Error($"[Graphics] Exception during initialization: {e.Message}");
            return false;
        }
    }

    public static void Shutdown()
    {
        m_Device = null;
        m_Instance = null;
        NativeRHI.RHILoader.Unload();
        Diagnostics.Logger.Info("[Graphics] RHI Shutdown completed");
    }

    internal static void SetLogicDevice(NativeRHI.RHIDevice device)
    {
        m_Device = device;
    }
}
