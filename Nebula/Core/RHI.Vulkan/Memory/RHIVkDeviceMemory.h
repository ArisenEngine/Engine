#pragma once
#include "RHI/Memory/DeviceMemory.h"
#include "vulkan_core.h"

namespace NebulaEngine::RHI
{
    class Device;

    class RHIVkDeviceMemory final : public DeviceMemory
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDeviceMemory)
        explicit RHIVkDeviceMemory(Device* device, VkBuffer buffer);

        ~RHIVkDeviceMemory() noexcept override;
        bool AllocDeviceMemory(u32 memoryPropertiesBits) override;
        bool AllocDeviceMemory(u32 memoryPropertiesBits, Containers::Vector<BufferHandle*> handles) override;
        
        void FreeDeviceMemory() override;
        void* GetHandle() const override;

        void MemoryCopy(void const* src, const u32 offset, const u32 size) override;

    private:
        VkDeviceMemory m_VkDeviceMemory;
        Device* m_Device;
        VkBuffer m_VkBuffer;
    };
}
