#pragma once
#include "RHI/Resources/RHIAccelerationStructure.h"
#include <vulkan/vulkan_core.h>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkAccelerationStructure : public RHIAccelerationStructure
    {
    public:
        RHIVkAccelerationStructure(RHIVkDevice* device, VkAccelerationStructureKHR handle, UInt64 size, UInt64 address);
        ~RHIVkAccelerationStructure() override;

        void* GetHandle() const override { return (void*)m_Handle; }
        UInt64 GetDeviceAddress() const override { return m_DeviceAddress; }

    private:
        RHIVkDevice* m_Device;
        VkAccelerationStructureKHR m_Handle;
        UInt64 m_Size;
        UInt64 m_DeviceAddress;
    };
}
