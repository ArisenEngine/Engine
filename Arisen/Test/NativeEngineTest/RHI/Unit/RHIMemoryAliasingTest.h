#pragma once
#include "../RHITestBase.h"
#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"

namespace ArisenEngine::Testing
{
    class RHIMemoryAliasingTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIMemoryAliasingTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            return true;
        }

        bool Run() override
        {
            LOG_INFO("Running Memory Aliasing Test...");

            // 1. Create a memory pool
            const UInt64 poolSize = 1024 * 1024; // 1MB
            RHI_MemoryPoolHandle pool = RHI_Device_CreateMemoryPool(m_Device, poolSize, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            if (pool == 0)
            {
                LOG_ERROR("Failed to create memory pool!");
                return false;
            }
            LOG_INFO("Memory pool created successfully.");

            // 2. Create aliased buffers
            ArisenEngine::RHI::RHIBufferDescriptor bufferDesc{};
            bufferDesc.size = 512 * 1024; // 512KB
            bufferDesc.usage = RHI::BUFFER_USAGE_STORAGE_BUFFER_BIT;
            bufferDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;

            // Buffer 1 at offset 0
            RHI_BufferHandle buffer1 = RHI_Device_CreateBufferAliased(m_Device, &bufferDesc, pool, 0, "AliasedBuffer1");
            // Buffer 2 at offset 256KB (overlapping or separate, both test the aliasing logic)
            // Let's make them separate for simple verification but in the same pool
            RHI_BufferHandle buffer2 = RHI_Device_CreateBufferAliased(m_Device, &bufferDesc, pool, 256 * 1024, "AliasedBuffer2");

            if (buffer1 == 0 || buffer2 == 0)
            {
                LOG_ERROR("Failed to create aliased buffers!");
                RHI_Device_ReleaseMemoryPool(m_Device, pool);
                return false;
            }
            LOG_INFO("Aliased buffers created successfully.");

            // 3. Create aliased images
            ArisenEngine::RHI::RHIImageDescriptor imageDesc{};
            imageDesc.imageType = RHI::IMAGE_TYPE_2D;
            imageDesc.format = RHI::EFormat::FORMAT_R8G8B8A8_UNORM;
            imageDesc.width = 256;
            imageDesc.height = 256;
            imageDesc.depth = 1;
            imageDesc.mipLevels = 1;
            imageDesc.arrayLayers = 1;
            imageDesc.usage = RHI::IMAGE_USAGE_SAMPLED_BIT | RHI::IMAGE_USAGE_TRANSFER_DST_BIT;
            imageDesc.imageLayout = RHI::IMAGE_LAYOUT_UNDEFINED;
            imageDesc.sampleCount = RHI::SAMPLE_COUNT_1_BIT;
            imageDesc.tiling = RHI::IMAGE_TILING_OPTIMAL;
            imageDesc.sharingMode = RHI::SHARING_MODE_EXCLUSIVE;

            // Image 1 at offset 0 (aliases with Buffer 1)
            RHI_ImageHandle image1 = RHI_Device_CreateImageAliased(m_Device, &imageDesc, pool, 0, "AliasedImage1");

            if (image1 == 0)
            {
                LOG_ERROR("Failed to create aliased image!");
                RHI_Device_ReleaseBuffer(m_Device, buffer1);
                RHI_Device_ReleaseBuffer(m_Device, buffer2);
                RHI_Device_ReleaseMemoryPool(m_Device, pool);
                return false;
            }
            LOG_INFO("Aliased image created successfully.");

            // Cleanup
            RHI_Device_ReleaseImage(m_Device, image1);
            RHI_Device_ReleaseBuffer(m_Device, buffer1);
            RHI_Device_ReleaseBuffer(m_Device, buffer2);
            RHI_Device_ReleaseMemoryPool(m_Device, pool);

            LOG_INFO("Memory Aliasing Test completed successfully.");
            return true;
        }

        void TeardownTest() override
        {
        }
    private:
    };
}
