#pragma once

#include "RHI/RHIRenderingTestBase.h"
#include <random>
#include <thread>
#include <chrono>

namespace ArisenEngine::Testing
{
    class RHIResizeStressTest : public RHIRenderingTestBase
    {
    public:
        const char* GetName() const override { return "RHIResizeStressTest"; }
        TestCategory GetCategory() const override { return TestCategory::Rendering; }
        
        // Return string description is not part of ITest interface, so remove it or just add as helper
        // virtual const char* GetDescription() const { return "Stress test for window resizing to detect race conditions and crashes."; }

        bool SetupTest() override
        {
            if (!RHIRenderingTestBase::SetupTest()) return false;
            
            InitCommonResources(); 
            
            // Ensure frame tickets are sized correctly
            m_FrameTickets.resize(m_MaxFramesInFlight, 0);

            return true;
        }

        void TeardownTest() override
        {
            TeardownCommonResources();
            RHIRenderingTestBase::TeardownTest();
        }

        void RenderFrame() override
        {
            static int frameCount = 0;
            frameCount++;

            // Perform resize every frame
            if (true)
            {
                static std::mt19937 rng(12345);
                static std::uniform_int_distribution<int> dist(800, 1600);
                
                int newWidth = dist(rng);
                int newHeight = (int)(newWidth * 9.0f / 16.0f);

                // Simulate resize event
                OnWindowResizing(HAL::GetWindowHandle(m_WindowId), newWidth, newHeight);
                
                // std::this_thread::sleep_for(std::chrono::milliseconds(20));
            }
          
            if (m_Device)
            {
                auto currentIndex = GetCurrentFrameIndex();
                
                // Wait for previous frame credential if valid
                if (m_FrameTickets[currentIndex] > 0)
                {
                    RHI_Device_WaitQueueTicket(m_Device, m_FrameTickets[currentIndex]);
                }
                
                auto cmd = RHI_Device_GetCommandBuffer(m_Device, m_CmdPool, currentIndex);
                RHI_Cmd_Begin(cmd, currentIndex, 0);

                auto imageHandle = RHI_SwapChain_BeginFrame(m_SwapChain, currentIndex);
                if (imageHandle)
                {
                    // Transition to present
                    RHI_Cmd_TransitionImageLayout(cmd, imageHandle, RHI::IMAGE_LAYOUT_PRESENT_SRC_KHR);
                    
                    RHI_Cmd_End(cmd);

                    RHI::RHISubmitDescriptor submitDesc = {};
                    submitDesc.WaitSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
                    submitDesc.SignalSwapChain = reinterpret_cast<RHI::RHISwapChain*>(m_SwapChain);
                    
                    m_FrameTickets[currentIndex] = RHI_Device_Submit(m_Device, cmd, reinterpret_cast<const ::RHISubmitDescriptor*>(&submitDesc));
                    
                    RHI_SwapChain_EndFrame(m_SwapChain, currentIndex);
                    
                // Release command buffer back to pool
                    RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
                }
                else
                {
                    RHI_Cmd_End(cmd);
                    RHI_Device_ReleaseCommandBuffer(m_Device, m_CmdPool, currentIndex, cmd);
                }

                if (frameCount >= 500)
                {
                    PostQuitMessage(0);
                }

                NextFrame();
            }
        }
    };
}
