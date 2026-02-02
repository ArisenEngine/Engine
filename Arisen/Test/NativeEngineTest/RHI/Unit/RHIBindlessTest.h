#pragma once
#include "../RHITestBase.h"
#include "../../Engine/NativeEngine/RHI/DescriptorExports.h"
#include "../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../Engine/NativeEngine/RHI/DeviceExports.h"

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
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            RHI_BufferHandle testBuffer = RHI_Device_CreateBuffer(m_Device, &bufDesc, "BindlessTestBuffer");

            // 2. Register with Bindless Manager
            UInt32 bufferIndex = RHI_Device_BindlessRegisterBuffer(m_Device, testBuffer);
            if (bufferIndex == 0xFFFFFFFF)
            {
                LOG_ERROR("Failed to register buffer with Bindless Manager");
                RHI_Device_ReleaseBuffer(m_Device, testBuffer);
                return false;
            }
            LOG_INFO("Buffer registered at bindless index: " + std::to_string(bufferIndex));

            RHI::RHIImageDescriptor imgDesc{
                RHI::IMAGE_TYPE_2D, 256, 256, 1, 1, 1,
                RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE,
                0, nullptr,
                RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT
            };
            RHI_ImageHandle testImage = RHI_Device_CreateImage(m_Device, &imgDesc, "BindlessTestImage");

            // Create a view for registration
            RHI::RHIImageViewDesc viewDesc{ RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
            viewDesc.width = 256;
            viewDesc.height = 256;
            RHI_ImageViewHandle testView = RHI_Image_AddImageView(m_Device, testImage, &viewDesc);

            // 4. Register image (using the view handle, not the image handle)
            UInt32 imageIndex = RHI_Device_BindlessRegisterImage(m_Device, testView);
            if (imageIndex == 0xFFFFFFFF)
            {
                LOG_ERROR("Failed to register image with Bindless Manager");
                RHI_Device_ReleaseImage(m_Device, testImage);
                RHI_Device_ReleaseBuffer(m_Device, testBuffer);
                return false;
            }
            LOG_INFO("Image registered at bindless index: " + std::to_string(imageIndex));

            // Wait for all operations to complete before cleanup
            RHI_Device_WaitIdle(m_Device);

            // Cleanup
            RHI_Device_ReleaseImage(m_Device, testImage);
            RHI_Device_ReleaseBuffer(m_Device, testBuffer);

            LOG_INFO("Bindless Resource Test Passed.");
            return true;
        }
    };
}
