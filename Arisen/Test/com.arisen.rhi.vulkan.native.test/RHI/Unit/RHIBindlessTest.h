#pragma once
#include "../RHITestBase.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"

namespace ArisenEngine::Testing
{
    /**
     * @brief Tests RHI Bindless Resource Architecture.
     */
    class RHIBindlessTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIBindlessTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool Run() override
        {
            LOG_INFO("Running Bindless Resource Test...");

            RHI::RHIBufferDescriptor bufDesc{
                0, 1024,
                RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT,
                RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };
            RHI::RHIBufferHandle testBuffer = m_Device->GetFactory()->CreateBuffer(
                std::move(bufDesc), "BindlessTestBuffer");

            // 2. Register with Bindless Manager
            UInt32 bufferIndex = m_Device->GetFactory()->RegisterBindlessResource(testBuffer);
            if (bufferIndex == 0xFFFFFFFF)
            {
                LOG_ERROR("Failed to register buffer with Bindless Manager");
                m_Device->GetFactory()->ReleaseBuffer(testBuffer);
                return false;
            }
            LOG_INFO("Buffer registered at bindless index: " + std::to_string(bufferIndex));

            RHI::RHIImageDescriptor imgDesc{
                RHI::IMAGE_TYPE_2D, 256, 256, 1, 1, 1,
                RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };
            RHI::RHIImageHandle testImage = m_Device->GetFactory()->
                                                      CreateImage(std::move(imgDesc), "BindlessTestImage");

            // Create a view for registration
            RHI::RHIImageViewDesc viewDesc{
                RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1
            };
            viewDesc.width = 256;
            viewDesc.height = 256;
            RHI::RHIImageViewHandle testView = m_Device->GetFactory()->CreateImageView(testImage, std::move(viewDesc));

            // 4. Register image (using the view handle, not the image handle)
            UInt32 imageIndex = m_Device->GetFactory()->RegisterBindlessResource(testView);
            if (imageIndex == 0xFFFFFFFF)
            {
                LOG_ERROR("Failed to register image with Bindless Manager");
                m_Device->GetFactory()->ReleaseImage(testImage);
                m_Device->GetFactory()->ReleaseBuffer(testBuffer);
                return false;
            }
            LOG_INFO("Image registered at bindless index: " + std::to_string(imageIndex));

            // Wait for all operations to complete before cleanup
            m_Device->DeviceWaitIdle();

            // Cleanup
            m_Device->GetFactory()->ReleaseImage(testImage);
            m_Device->GetFactory()->ReleaseBuffer(testBuffer);

            LOG_INFO("Bindless Resource Test Passed.");
            return true;
        }
    };
}
