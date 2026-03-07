#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Handles/RHIHandle.h"
#include "../../Core.RHI/RHI/Core/RHICommon.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    struct RHIVkDescriptorBufferDesc
    {
        UInt64 size;
        bool isSamplerBuffer;
        bool isResourceBuffer;
    };

    class RHIVkDescriptorBuffer
    {
    public:
        RHIVkDescriptorBuffer(RHIVkDevice* device);
        ~RHIVkDescriptorBuffer();

        bool Initialize(const RHIVkDescriptorBufferDesc& desc);
        void Shutdown();

        RHIBufferHandle GetBufferHandle() const { return m_BufferHandle; }
        VkDeviceAddress GetDeviceAddress() const { return m_DeviceAddress; }
        void* GetMappedPointer() const { return m_MappedPointer; }

    private:
        RHIVkDevice* m_Device;
        RHIBufferHandle m_BufferHandle;
        VkDeviceAddress m_DeviceAddress{0};
        void* m_MappedPointer{nullptr};
        UInt64 m_Size{0};
    };
}
