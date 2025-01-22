#pragma once
#include "RHI/Memory/DeviceMemory.h"
#include "vulkan_core.h"

namespace ArisenEngine::RHI
{
    class Device;

    class RHIVkDeviceMemory final : public DeviceMemory
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDeviceMemory)
        explicit RHIVkDeviceMemory(Device* device, VkBuffer buffer);

        ~RHIVkDeviceMemory() noexcept override;
        bool AllocDeviceMemory(UInt32 memoryPropertiesBits) override;
        bool AllocDeviceMemory(UInt32 memoryPropertiesBits, Containers::Vector<BufferHandle*> handles) override;
        
        void FreeDeviceMemory() override;
        void* GetHandle() const override;

        void MemoryCopy(void const* src, const UInt32 offset, const UInt32 size) override;

    private:
        VkDeviceMemory m_VkDeviceMemory;
        Device* m_Device;
        VkBuffer m_VkBuffer;
    };
}
