#pragma once

#include "Framework/TestRunner.h"
#include "RHI/RHILoader.h"
#include "RHI/Core/RHIInstance.h"
#include "Windowing/RenderWindowAPI.h"
#include "Common/PlatformTypes.h"
#include "../../../Engine/NativeEngine/RHI/InstanceExports.h"
#include "../../../Engine/NativeEngine/RHI/RHIExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"

#define GLM_FORCE_RADIANS
#define GLM_FORCE_DEPTH_ZERO_TO_ONE
#include <glm/glm.hpp>
#include <glm/gtc/constants.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <string>
#include <vector>


namespace ArisenEngine::Testing
{
    struct VertexAttributeDesc
    {
        std::string name;
        RHI::EFormat format;
        UInt32 offset;
        UInt32 location;
    };

    struct VertexLayout
    {
        std::vector<VertexAttributeDesc> attributes;
        UInt32 stride = 0;
    };

    struct GLTFVertex
    {
        glm::vec3 pos;
        glm::vec3 normal;
        glm::vec2 uv;
        glm::vec4 color;
    };

    struct GLTFMaterial
    {
        RHI_ImageHandle baseColorTexture = 0;
        RHI_ImageViewHandle baseColorView = 0;
        RHI_SamplerHandle sampler = 0;
    };

    struct GLTFPrimitive
    {
        UInt32 firstIndex;
        UInt32 indexCount;
        SInt32 materialIndex;
    };

    struct GLTFModel
    {
        RHI_BufferHandle vertexBuffer = 0;
        RHI_BufferHandle indexBuffer = 0;
        UInt32 indexCount = 0;
        VertexLayout layout;

        Containers::Vector<GLTFPrimitive> primitives;
        Containers::Vector<GLTFMaterial> materials;
        
        void Release(RHI_DeviceHandle device)
        {
            if (vertexBuffer) RHI_Device_ReleaseBuffer(device, vertexBuffer);
            if (indexCount > 0 && indexBuffer) RHI_Device_ReleaseBuffer(device, indexBuffer);
            vertexBuffer = 0;
            indexBuffer = 0;
            indexCount = 0;

            for (auto& mat : materials)
            {
                if (mat.baseColorTexture) RHI_Device_ReleaseImage(device, mat.baseColorTexture);
                if (mat.sampler) RHI_Device_ReleaseSampler(device, mat.sampler);
            }
            materials.clear();
            primitives.clear();
        }
    };

    /**
     * @brief Common Uniform Buffer Object for tests.
     */
    struct UniformBufferObject
    {
        alignas(16) glm::mat4 model;
        alignas(16) glm::mat4 view;
        alignas(16) glm::mat4 projection;
        alignas(16) float mipmapBias;
    };

    /**
     * @brief Base class for all RHI tests.
     * 
     * Provides common functionality:
     * - RHI instance creation
     * - Device and surface management
     * - Window creation
     * - Frame synchronization
     * - Input and Camera handling
     */
    class RHITestBase : public ITest
    {
    protected:
        using Clock = std::chrono::high_resolution_clock;
        Clock::time_point lastTime = Clock::now();
        double frameTime = 0.0;
        double fps = 0.0;
        Float32 s_FrameTimeSpacing = 0.0;
        
        RHI_InstanceHandle m_Instance = nullptr;
        RHI_DeviceHandle m_Device = nullptr;
        UInt32 m_WindowId = ~0u;
        UInt32 m_MaxFramesInFlight = 2;
        UInt32 m_FrameIndex = 0;

        // Input state
        bool m_Keys[256] = { false };
        float m_MouseX = 0.0f, m_MouseY = 0.0f;
        float m_MouseDX = 0.0f, m_MouseDY = 0.0f;
        bool m_MouseButtons[3] = { false }; // 0: Left, 1: Right, 2: Middle

        // Camera state
        glm::vec3 m_CameraPos = glm::vec3(0.0f, 1.0f, 5.0f);
        glm::vec3 m_CameraRot = glm::vec3(0.0f, -glm::half_pi<float>(), 0.0f); // pitch, yaw, roll

        void UpdateCamera(float deltaTime)
        {
            float speed = 5.0f * deltaTime;
            if (m_Keys[VK_SHIFT]) speed *= 30.0f;       // Turbo
            if (m_Keys[VK_CONTROL]) speed *= 0.1f;    // Precision

            glm::vec3 forward;
            forward.x = cos(m_CameraRot.y) * cos(m_CameraRot.x);
            forward.y = sin(m_CameraRot.x);
            forward.z = sin(m_CameraRot.y) * cos(m_CameraRot.x);
            forward = glm::normalize(forward);

            glm::vec3 right = glm::normalize(glm::cross(forward, glm::vec3(0, 1, 0)));
            // Align 'up' with world-up for E/Q movement, or use camera-up? 
            // Usually 'E' implies world-up in fly cams, but camera-relative is also common. 
            // Previous code used (0,1,0) which is World Up. Sticking to World Up for Q/E.

            if (m_Keys['W']) m_CameraPos += forward * speed;
            if (m_Keys['S']) m_CameraPos -= forward * speed;
            if (m_Keys['A']) m_CameraPos -= right * speed;
            if (m_Keys['D']) m_CameraPos += right * speed;
            if (m_Keys['E']) m_CameraPos += glm::vec3(0, 1, 0) * speed;
            if (m_Keys['Q']) m_CameraPos -= glm::vec3(0, 1, 0) * speed;

            // Only rotate if RMB is held
            if (m_MouseButtons[1]) 
            {
                float sensitivity = 0.005f;
                m_CameraRot.y += m_MouseDX * sensitivity;
                m_CameraRot.x -= m_MouseDY * sensitivity;
                
                // Clamp pitch
                m_CameraRot.x = glm::clamp(m_CameraRot.x, -glm::half_pi<float>() + 0.01f, glm::half_pi<float>() - 0.01f);
            }
        }

        glm::mat4 GetViewMatrix()
        {
            glm::vec3 forward;
            forward.x = cos(m_CameraRot.y) * cos(m_CameraRot.x);
            forward.y = sin(m_CameraRot.x);
            forward.z = sin(m_CameraRot.y) * cos(m_CameraRot.x);
            return glm::lookAt(m_CameraPos, m_CameraPos + forward, glm::vec3(0, 1, 0));
        }

        glm::mat4 GetProjectionMatrix(float aspect)
        {
            glm::mat4 proj = glm::perspective(glm::radians(45.0f), aspect, 0.1f, 1000.0f);
            proj[1][1] *= -1; // Vulkan Y-down
            return proj;
        }

        /**
         * @brief Whether this test requires a window and swapchain.
         */
        virtual bool IsHeadless() const { return false; }


        virtual void RenderFrame() {};

        GLTFModel LoadGLTF(const std::string& path);

        /**
         * @brief Initialize RHI instance with default settings.
         */
        bool InitializeRHI(const char* appName = "RHI Unit Test")
        {
            RHI::RHIInstanceInfo appInfo{
                appName,
                "Arisen Engine",
                true,  // Enable validation layers for unit tests to catch issues
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
            RHITestBase* test = (RHITestBase*)HAL::GetWindowUserData(HAL::GetWindowId(hwnd));

            switch (msg)
            {
            case WM_CLOSE:
                DestroyWindow(hwnd);
                return 0;
            case WM_DESTROY:
                PostQuitMessage(0);
                return 0;
            case WM_KEYDOWN:
                if (test) test->m_Keys[wParam & 0xFF] = true;
                return 0;
            case WM_KEYUP:
                if (test) test->m_Keys[wParam & 0xFF] = false;
                return 0;
            case WM_LBUTTONDOWN: if (test) test->m_MouseButtons[0] = true; return 0;
            case WM_LBUTTONUP:   if (test) test->m_MouseButtons[0] = false; return 0;
            case WM_RBUTTONDOWN: 
                if (test) {
                    test->m_MouseButtons[1] = true; 
                    SetCapture(hwnd);
                    ShowCursor(FALSE);
                }
                return 0;
            case WM_RBUTTONUP:   
                if (test) {
                    test->m_MouseButtons[1] = false; 
                    ReleaseCapture();
                    ShowCursor(TRUE);
                }
                return 0;
            case WM_MBUTTONDOWN: if (test) test->m_MouseButtons[2] = true; return 0;
            case WM_MBUTTONUP:   if (test) test->m_MouseButtons[2] = false; return 0;
            case WM_MOUSEMOVE:
                if (test)
                {
                    float x = (float)LOWORD(lParam);
                    float y = (float)HIWORD(lParam);
                    test->m_MouseDX += x - test->m_MouseX;
                    test->m_MouseDY += y - test->m_MouseY;
                    test->m_MouseX = x;
                    test->m_MouseY = y;
                }
                return 0;
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        /**
         * @brief Create a render window.
         */
        bool CreateAppWindow(UInt32 width = 1280, UInt32 height = 720)
        {
            m_WindowId = HAL::CreateRenderWindow(nullptr, TestWndProc, width, height);
            if (m_WindowId != ~0u)
            {
                HAL::SetWindowUserData(m_WindowId, this);
                return true;
            }
            return false;
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

            lastTime = Clock::now();
            return m_Device != nullptr;
        }

        bool Run() override
        {
            MSG msg{};
            bool isRunning = true;
            
            while (isRunning)
            {
                m_MouseDX = 0;
                m_MouseDY = 0;

                while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessage(&msg);
                    if (msg.message == WM_QUIT)
                    {
                        isRunning = false;
                    }
                }

                if (!isRunning) break;

                RenderFrame();
            }

            return true;    
        }
        
        /**
         * @brief Advance to the next frame.
         */
        void NextFrame()
        {
            ++m_FrameIndex;

            // FPS Calculation
            auto currentTime = Clock::now();
            std::chrono::duration<double> delta = currentTime - lastTime;
            lastTime = currentTime;

            frameTime = delta.count();
            fps = (1.0 / frameTime) * 0.1 + fps * 0.9;
            s_FrameTimeSpacing += (Float32)frameTime;
            if (s_FrameTimeSpacing >= 1.0)
            {
                s_FrameTimeSpacing = 0.0;
                std::cout << "FPS:" << fps << ", Delta Time:"<< frameTime << std::endl;
            }
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

