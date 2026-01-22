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

            // 1. Create a buffer
            RHI_BufferHandle testBuffer = RHI_Device_GetBufferHandle(m_Device, "BindlessTestBuffer");
            RHI::BufferDescriptor bufDesc{ 0, 1024, RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE };
            RHI_Buffer_Alloc(m_Device, testBuffer, &bufDesc);
            RHI_Buffer_AllocDeviceMemory(m_Device, testBuffer, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            // 2. Register with Bindless Manager
            UInt32 bufferIndex = RHI_Device_BindlessRegisterBuffer(m_Device, testBuffer);
            if (bufferIndex == 0xFFFFFFFF)
            {
                LOG_ERROR("Failed to register buffer with Bindless Manager");
                RHI_Device_ReleaseBufferHandle(m_Device, testBuffer);
                return false;
            }
            LOG_INFO("Buffer registered at bindless index: " + std::to_string(bufferIndex));

            // 3. Create an image
            RHI_ImageHandle testImage = RHI_Device_GetImageHandle(m_Device, "BindlessTestImage");
            RHI::ImageDescriptor imgDesc{
                RHI::IMAGE_TYPE_2D, 256, 256, 1, 1, 1,
                RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_TILING_OPTIMAL,
                RHI::IMAGE_LAYOUT_UNDEFINED, RHI::IMAGE_USAGE_SAMPLED_BIT,
                RHI::SAMPLE_COUNT_1_BIT, RHI::SHARING_MODE_EXCLUSIVE
            };
            RHI_Image_Alloc(m_Device, testImage, &imgDesc);
            RHI_Image_AllocDeviceMemory(m_Device, testImage, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);

            // Create a view for registration
            RHI::ImageViewDesc viewDesc{ RHI::IMAGE_VIEW_TYPE_2D, RHI::FORMAT_R8G8B8A8_UNORM, RHI::IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 };
            viewDesc.width = 256;
            viewDesc.height = 256;
            RHI_Image_AddImageView(m_Device, testImage, &viewDesc);

            // 4. Register image
            UInt32 imageIndex = RHI_Device_BindlessRegisterImage(m_Device, testImage);
            if (imageIndex == 0xFFFFFFFF)
            {
                LOG_ERROR("Failed to register image with Bindless Manager");
                RHI_Device_ReleaseImageHandle(m_Device, testImage);
                RHI_Device_ReleaseBufferHandle(m_Device, testBuffer);
                return false;
            }
            LOG_INFO("Image registered at bindless index: " + std::to_string(imageIndex));

            // Cleanup
            RHI_Device_ReleaseImageHandle(m_Device, testImage);
            RHI_Device_ReleaseBufferHandle(m_Device, testBuffer);

            LOG_INFO("Bindless Resource Test Passed.");
            return true;
        }
    };
}
