#include "RHIVkAccelerationStructure.h"
#include "Core/RHIVkDevice.h"

namespace ArisenEngine::RHI
{
    RHIVkAccelerationStructure::RHIVkAccelerationStructure(RHIVkDevice* device, VkAccelerationStructureKHR handle,
                                                           UInt64 size, UInt64 address)
        : m_Device(device), m_Handle(handle), m_Size(size), m_DeviceAddress(address)
    {
    }

    RHIVkAccelerationStructure::~RHIVkAccelerationStructure()
    {
        // Handle is destroyed by the pool/internal destruction logic to ensure correct sequencing with deferred deletion
    }
}
