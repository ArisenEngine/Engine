using System;
using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;

namespace ArisenEngine.Core.Lifecycle;

public static class NativeRuntime
{
    private static bool m_IsInitialized = false;

    public static bool Initialize()
    {
        if (m_IsInitialized) return true;

        try
        {
            // Initialize Diagnostics first
            Diagnostics.Logger.Initialize(true);

            // Initialize Graphics RHI (Default to Vulkan for now)
            if (RHISystem.Initialize(GraphicsAPI.Vulkan, validationLayer: true))
            {
                // Defer physical device picking and surface creation to actual surface registration
                m_IsInitialized = true;
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            // Fallback to console if logger is not ready, but usually EngineInit handles logger
            Console.WriteLine($"[NativeRuntime] Failed to initialize native engine: {e.Message}");
        }

        return false;
    }

    public static void Shutdown()
    {
        if (!m_IsInitialized) return;

        try
        {
            RHISystem.Shutdown();
            // Arisen.Native.Core.EngineInit.Shutdown();
            m_IsInitialized = false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[NativeRuntime] Error during native engine shutdown: {e.Message}");
        }
    }
}
