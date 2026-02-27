using Arisen.Native.RHI;

namespace ArisenEngine.Core.RHI;

public static class RHISystem
{
    private static NativeRHI.RHIInstance? m_Instance;
    private static NativeRHI.RHIDevice? m_Device;

    public static NativeRHI.RHIInstance? Instance => m_Instance;
    public static NativeRHI.RHIDevice? Device => m_Device;

    public static bool Initialize(NativeRHI.GraphicsAPI api, string appName = "ArisenApp", bool validationLayer = false)
    {
        try
        {
            // 1. Set the graphics API
            NativeRHI.RHILoader.SetCurrentGraphicsAPI(api);

            // 2. Create Instance Info
            var info = new NativeRHI.RHIInstanceInfo
            {
                Name = appName,
                EngineName = "ArisenEngine",
                ValidationLayer = validationLayer,
                Variant = 0, Major = 1, Minor = 3, Patch = 0, // Vulkan 1.3
                AppMajor = 1, AppMinor = 0, AppPatch = 0,
                EngineMajor = 1, EngineMinor = 0, EnginePatch = 0,
                MaxFramesInFlight = 2
            };

            // 3. Create Instance
            m_Instance = NativeRHI.RHILoader.CreateInstance(info);
            if (m_Instance == null)
            {
                Diagnostics.Logger.Error($"[RHISystem] Failed to create RHI instance for {api}");
                return false;
            }

            // 4. Initialization logic for physical devices
            m_Instance.PickPhysicalDevice(false);
            m_Instance.InitLogicDevices();

            Diagnostics.Logger.Info($"[RHISystem] Successfully initialized {api} RHI");
            return true;
        }
        catch (Exception e)
        {
            Diagnostics.Logger.Error($"[RHISystem] Exception during initialization: {e.Message}");
            return false;
        }
    }

    public static void Shutdown()
    {
        m_Device = null;
        m_Instance = null;
        NativeRHI.RHILoader.Unload();
        Diagnostics.Logger.Info("[RHISystem] RHI Shutdown completed");
    }

    internal static void SetLogicDevice(NativeRHI.RHIDevice device)
    {
        m_Device = device;
    }
}
