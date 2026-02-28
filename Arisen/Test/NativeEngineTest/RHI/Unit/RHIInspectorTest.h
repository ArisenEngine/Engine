#include <iostream>
#include <string>
#include "../RHITestBase.h"


#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIFactory.h"
#include "../../../../Core/Core.RHI/RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIInspector.h"


namespace ArisenEngine::Testing
{
    class RHIInspectorTest : public RHITestBase
    {
    public:
        const char* GetName() const override { return "RHIInspectorTest"; }
        TestCategory GetCategory() const override { return TestCategory::Unit; }
        bool IsHeadless() const override { return true; }

        bool SetupTest() override
        {
            return true;
        }

        bool Run() override
        {
            LOG_INFO("Running RHI Inspector Test...");


            auto* device = m_Device;
            if (!device) return false;

            const ArisenEngine::RHI::RHIResourceStats& initialStats = device->GetResourceStats();

            UInt32 initialBufferCount = initialStats.bufferCount.load();
            UInt64 initialMemory = initialStats.totalVideoMemoryAllocated.load();

            std::string initMsg = "Initial Buffer Count: " + std::to_string(initialBufferCount) + ", Memory: " +
                std::to_string(initialMemory) + " bytes";
            LOG_INFO(initMsg.c_str());


            // 1. Allocate a buffer


            ArisenEngine::RHI::RHIBufferDescriptor bufferDesc{
                0, 1024, RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr,
                RHI::ERHIMemoryUsage::GpuOnly
            };
            RHI::RHIBufferHandle buffer = m_Device->GetFactory()->CreateBuffer(
                std::move(bufferDesc), "InspectorTestBuffer");

            if (!buffer.IsValid())
            {
                LOG_ERROR("Buffer creation failed!");
                return false;
            }

            // Verify count increased
            const ArisenEngine::RHI::RHIResourceStats& midStats = device->GetResourceStats();

            UInt32 midBufferCount = midStats.bufferCount.load();
            UInt64 midMemory = midStats.totalVideoMemoryAllocated.load();

            std::string midMsg = "After Alloc - Buffer Count: " + std::to_string(midBufferCount) + ", Memory: " +
                std::to_string(midMemory) + " bytes";
            LOG_INFO(midMsg.c_str());


            if (midBufferCount != initialBufferCount + 1)
            {
                std::string errMsg = "Buffer count did not increase! Expected " + std::to_string(initialBufferCount + 1)
                    + ", got " + std::to_string(midBufferCount);
                LOG_ERROR(errMsg.c_str());


                return false;
            }

            if (midMemory <= initialMemory)
            {
                std::string errMsg = "Memory usage did not increase! Expected > " + std::to_string(initialMemory) +
                    ", got " + std::to_string(midMemory);
                LOG_ERROR(errMsg.c_str());

                return false;
            }

            // 2. Release buffer
            m_Device->GetFactory()->ReleaseBuffer(buffer);

            // Verify count decreased (might need to check deferred deletion if applicable, but handle count should perform immediate decrement in pool deallocate?)
            // RHIResourcePool decrements in Deallocate. Device::ReleaseBuffer calls ReleaseBufferInternal then Deallocate.
            // However, Deallocate only succeeds if the generation matches.

            const ArisenEngine::RHI::RHIResourceStats& endStats = device->GetResourceStats();

            UInt32 finalBufferCount = endStats.bufferCount.load();
            UInt64 finalMemory = endStats.totalVideoMemoryAllocated.load();

            std::string finalMsg = "After Release - Buffer Count: " + std::to_string(finalBufferCount) + ", Memory: " +
                std::to_string(finalMemory) + " bytes";
            LOG_INFO(finalMsg.c_str());


            if (finalBufferCount != initialBufferCount)

            {
                std::string errMsg = "Buffer count did not return to initial! Expected " +
                    std::to_string(initialBufferCount) + ", got " + std::to_string(finalBufferCount);
                LOG_ERROR(errMsg.c_str());


                return false;
            }

            // Memory might not decrease immediately if it's deferred delete.
            // But handle count SHOULD decrease immediately.

            return true;
        }

        void TeardownTest() override
        {
        }
    };
}
