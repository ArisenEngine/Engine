#pragma once
#include "../RHITestBase.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"

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
            RHI::RHIMemoryPoolHandle pool = m_Device->GetFactory()->CreateMemoryPool(
                poolSize, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
            if (!pool.IsValid())
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
            auto buffer1 = m_Device->GetFactory()->CreateBufferAliased(
                ArisenEngine::RHI::RHIBufferDescriptor(bufferDesc), pool, 0, "AliasedBuffer1");
            // Buffer 2 at offset 256KB
            auto buffer2 = m_Device->GetFactory()->CreateBufferAliased(
                ArisenEngine::RHI::RHIBufferDescriptor(bufferDesc), pool, 256 * 1024, "AliasedBuffer2");

            if (!buffer1.IsValid() || !buffer2.IsValid())
            {
                LOG_ERROR("Failed to create aliased buffers!");
                m_Device->GetFactory()->ReleaseMemoryPool(pool);
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
            auto image1 = m_Device->GetFactory()->CreateImageAliased(std::move(imageDesc), pool, 0, "AliasedImage1");

            if (!image1.IsValid())
            {
                LOG_ERROR("Failed to create aliased image!");
                m_Device->GetFactory()->ReleaseBuffer(buffer1);
                m_Device->GetFactory()->ReleaseBuffer(buffer2);
                m_Device->GetFactory()->ReleaseMemoryPool(pool);
                return false;
            }
            LOG_INFO("Aliased image created successfully.");

            // Cleanup
            m_Device->GetFactory()->ReleaseImage(image1);
            m_Device->GetFactory()->ReleaseBuffer(buffer1);
            m_Device->GetFactory()->ReleaseBuffer(buffer2);
            m_Device->GetFactory()->ReleaseMemoryPool(pool);

            LOG_INFO("Memory Aliasing Test completed successfully.");
            return true;
        }

        void TeardownTest() override
        {
        }

    private:
    };
}
