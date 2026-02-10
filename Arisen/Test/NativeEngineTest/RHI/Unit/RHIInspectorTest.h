#include <iostream>
#include "../RHITestBase.h"



#include "../../../Engine/NativeEngine/RHI/HandlesExports.h"
#include "../../../Engine/NativeEngine/RHI/CommandBufferExports.h"
#include "../../../Engine/NativeEngine/RHI/DeviceExports.h"
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




            auto* device = reinterpret_cast<ArisenEngine::RHI::RHIDevice*>(m_Device);
            if (!device) return false;

            const ArisenEngine::RHI::RHIResourceStats& initialStats = device->GetResourceStats();

            UInt32 initialBufferCount = initialStats.bufferCount.load();
            UInt64 initialMemory = initialStats.totalVideoMemoryAllocated.load();

            LOG_INFO("Initial Buffer Count: {}, Memory: {} bytes", initialBufferCount, initialMemory);

            // 1. Allocate a buffer



            ArisenEngine::RHI::RHIBufferDescriptor bufferDesc{ 0, 1024, RHI::BUFFER_USAGE_VERTEX_BUFFER_BIT, RHI::SHARING_MODE_EXCLUSIVE, 0, nullptr, RHI::MEMORY_PROPERTY_DEVICE_LOCAL_BIT };
            RHI_BufferHandle buffer = RHI_Device_CreateBuffer(m_Device, &bufferDesc, "InspectorTestBuffer");

            if (buffer == 0)
            {
                LOG_ERROR("Buffer creation failed!");
                return false;
            }

            // Verify count increased
            const ArisenEngine::RHI::RHIResourceStats& midStats = device->GetResourceStats();

            UInt32 midBufferCount = midStats.bufferCount.load();
            UInt64 midMemory = midStats.totalVideoMemoryAllocated.load();
            
            outFile << "DEBUG: After Alloc - Buffer Count: " << midBufferCount << ", Memory: " << midMemory << std::endl;
            // LOG_INFO("After Alloc - Buffer Count: {}, Memory: {} bytes", midBufferCount, midMemory);



            if (midBufferCount != initialBufferCount + 1)
            {
                LOG_ERROR("Buffer count did not increase! Expected {}, got {}", initialBufferCount + 1, midBufferCount);
                return false;
            }

            if (midMemory <= initialMemory)
            {
                 LOG_ERROR("Memory usage did not increase! Expected > {}, got {}", initialMemory, midMemory);
                 return false;
            }

            // 2. Release buffer
            RHI_Device_ReleaseBuffer(m_Device, buffer);

            // Verify count decreased (might need to check deferred deletion if applicable, but handle count should perform immediate decrement in pool deallocate?)
            // RHIResourcePool decrements in Deallocate. Device::ReleaseBuffer calls ReleaseBufferInternal then Deallocate.
            // However, Deallocate only succeeds if the generation matches.
            
            const ArisenEngine::RHI::RHIResourceStats& endStats = device->GetResourceStats();

            UInt32 finalBufferCount = endStats.bufferCount.load();
            UInt64 finalMemory = endStats.totalVideoMemoryAllocated.load();

            LOG_INFO("After Release - Buffer Count: {}, Memory: {} bytes", finalBufferCount, finalMemory);

            if (finalBufferCount != initialBufferCount)

            {
                LOG_ERROR("Buffer count did not return to initial! Expected {}, got {}", initialBufferCount, finalBufferCount);
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
