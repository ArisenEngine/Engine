#pragma once

#include "Framework/TestRunner.h"
#include "RHI/RHILoader.h"
#include "RHI/Core/RHIInstance.h"
#include "Windowing/RenderWindowAPI.h"
#include "Common/PlatformTypes.h"
#include "../../../Engine/NativeEngine/RHI/InstanceExports.h"
#include "../../../Engine/NativeEngine/RHI/RHIExports.h"

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
        UInt32 m_WindowId = ~0u;
        UInt32 m_MaxFramesInFlight = 2;
        UInt32 m_FrameIndex = 0;

        /**
         * @brief Whether this test requires a window and swapchain.
         */
        virtual bool IsHeadless() const { return false; }

        /**
         * @brief Initialize RHI instance with default settings.
         */
        bool InitializeRHI(const char* appName = "RHI Unit Test")
        {
            RHI::RHIInstanceInfo appInfo{
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
        static LRESULT CALLBACK TestWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
        {
            switch (msg)
            {
            case WM_CLOSE:
                DestroyWindow(hwnd);
                return 0;
            case WM_DESTROY:
                PostQuitMessage(0);
                return 0;
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        /**
         * @brief Create a render window.
         */
        bool CreateAppWindow(UInt32 width = 640, UInt32 height = 480)
        {
            m_WindowId = HAL::CreateRenderWindow(nullptr, TestWndProc, width, height);
            // Platforms assumes Assert on failure, but returns InvalidID (~0) if Assert disabled/ignored
            // Valid valid IDs are 0, 1, ...
            // We check against ~0u (UINT32_MAX)
            return m_WindowId != ~0u;
        }

        /**
         * @brief Initialize device and surface.
         */
        bool InitializeDevice()
        {
            if (!m_Instance)
            {
                LOG_ERROR("Instance not initialized");
                return false;
            }

            if (!IsHeadless())
            {
                if (m_WindowId == ~0u)
                {
                    LOG_ERROR("Window not initialized for non-headless test");
                    return false;
                }
                RHI_Instance_CreateSurface(m_Instance, m_WindowId);
            }

            RHI_Instance_PickPhysicalDevice(m_Instance, !IsHeadless());
            RHI_Instance_InitLogicDevices(m_Instance);

            if (!IsHeadless())
            {
                m_Device = RHI_Instance_GetLogicalDevice(m_Instance, m_WindowId);
            }
            else
            {
                // For headless, we might need a way to get a device without a window.
                // Assuming RHI_Instance_GetLogicalDevice(m_Instance, ~0u) or similar works, 
                // but usually the first device is fine.
                m_Device = RHI_Instance_GetLogicalDevice(m_Instance, ~0u);
            }

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

            if (!IsHeadless())
            {
                if (!CreateAppWindow())
                {
                    LOG_ERROR("Failed to create window");
                    return false;
                }
            }

            if (!InitializeDevice())
            {
                LOG_ERROR("Failed to initialize device");
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

            if (m_WindowId != ~0u)
            {
                HAL::RemoveRenderSurface(m_WindowId);
                m_WindowId = ~0u;
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

