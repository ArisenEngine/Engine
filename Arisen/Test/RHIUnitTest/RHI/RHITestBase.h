#pragma once

#include "Framework/TestRunner.h"
#include "RHI/RHILoader.h"
#include "RHI/RHIInstance.h"
#include "Windows/RenderWindowAPI.h"
#include "Windows/PlatformTypes.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Base class for all RHI tests.
     * 
     * Provides common functionality:
     * - RHI instance creation
     * - Device and surface management
     * - Window creation
     * - Frame synchronization
     */
    class RHITestBase : public ITest
    {
    protected:
        RHI_InstanceHandle m_Instance = nullptr;
        RHI_DeviceHandle m_Device = nullptr;
        UInt32 m_WindowId = 0;
        UInt32 m_MaxFramesInFlight = 2;
        UInt32 m_FrameIndex = 0;

        /**
         * @brief Initialize RHI instance with default settings.
         */
        bool InitializeRHI(const char* appName = "RHI Unit Test")
        {
            RHI::InstanceInfo appInfo{
                appName,
                "Arisen Engine",
                true,  // Enable validation layers
                0, 1, 3, 0,  // Vulkan 1.3
                1, 0, 0,     // App version
                1, 0, 0,     // Engine version
                2            // Max frames in flight
            };

            RHI_SetGraphicsAPI(RHI::GraphicsAPI::Vulkan);
            m_Instance = RHI_CreateInstance(&appInfo);
            
            if (!m_Instance)
            {
                LOG_ERROR("Failed to create RHI instance");
                return false;
            }

            m_MaxFramesInFlight = RHI_Instance_GetMaxFramesInFlight(m_Instance);
            return true;
        }

        /**
         * @brief Create a render window.
         */
        bool CreateWindow(UInt32 width = 640, UInt32 height = 480)
        {
            m_WindowId = Platforms::CreateRenderWindow(nullptr, DefWindowProc, width, height);
            return m_WindowId != 0;
        }

        /**
         * @brief Initialize device and surface.
         */
        bool InitializeDeviceAndSurface()
        {
            if (!m_Instance || m_WindowId == 0)
            {
                LOG_ERROR("Instance or window not initialized");
                return false;
            }

            RHI_Instance_CreateSurface(m_Instance, m_WindowId);
            RHI_Instance_PickPhysicalDevice(m_Instance, true);
            RHI_Instance_InitLogicDevices(m_Instance);

            m_Device = RHI_Instance_GetLogicalDevice(m_Instance, m_WindowId);
            return m_Device != nullptr;
        }

        /**
         * @brief Advance to the next frame.
         */
        void NextFrame()
        {
            ++m_FrameIndex;
        }

        /**
         * @brief Get current frame index modulo max frames in flight.
         */
        UInt32 GetCurrentFrameIndex() const
        {
            return m_FrameIndex % m_MaxFramesInFlight;
        }

    public:
        virtual ~RHITestBase() = default;

        /**
         * @brief Default setup: Initialize RHI, create window, setup device.
         */
        bool Setup() override
        {
            if (!InitializeRHI(GetName()))
            {
                return false;
            }

            if (!CreateWindow())
            {
                LOG_ERROR("Failed to create window");
                return false;
            }

            if (!InitializeDeviceAndSurface())
            {
                LOG_ERROR("Failed to initialize device and surface");
                return false;
            }

            return SetupTest();
        }

        /**
         * @brief Default teardown: Cleanup RHI resources.
         */
        void Teardown() override
        {
            TeardownTest();

            if (m_Instance)
            {
                RHI_DestroyInstance(m_Instance);
                m_Instance = nullptr;
            }

            if (m_WindowId != 0)
            {
                Platforms::DestroyRenderWindow(m_WindowId);
                m_WindowId = 0;
            }
        }

        /**
         * @brief Test-specific setup. Override in derived classes.
         */
        virtual bool SetupTest() { return true; }

        /**
         * @brief Test-specific teardown. Override in derived classes.
         */
        virtual void TeardownTest() {}
    };
}
