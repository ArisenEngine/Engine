#include <cstdio>
#include "Presentation/RHIVkFrameBuffer.h"
#include "Logger/Logger.h"
#include "Core/RHIVkDevice.h"
#include <vulkan/vulkan_core.h>

namespace ArisenEngine::RHI
{
    ArisenEngine::RHI::RHIVkFrameBuffer::RHIVkFrameBuffer(RHIVkDevice* device, UInt32 maxFramesInFlight):
        RHIFrameBuffer(maxFramesInFlight), m_Device(device)
    {
        m_VkFrameBuffers.resize(maxFramesInFlight, VK_NULL_HANDLE);
    }

    ArisenEngine::RHI::RHIVkFrameBuffer::~RHIVkFrameBuffer() noexcept
    {
        auto device = static_cast<VkDevice>(m_Device->GetHandle());
        for (auto fb : m_VkFrameBuffers)
        {
            if (fb != VK_NULL_HANDLE)
            {
                vkDestroyFramebuffer(device, fb, nullptr);
            }
        }
        m_VkFrameBuffers.clear();
    }

    void* ArisenEngine::RHI::RHIVkFrameBuffer::GetHandle(UInt32 currentFrameIndex)
    {
        return m_VkFrameBuffers[currentFrameIndex % m_MaxFramesInFlight];
    }

    ArisenEngine::RHI::EFormat ArisenEngine::RHI::RHIVkFrameBuffer::GetAttachFormat()
    {
        auto* vkView = m_Device->GetImageViewPool()->Get(m_ImageView);
        if (!vkView) return EFormat::FORMAT_UNDEFINED;
        return vkView->format;
    }
} // namespace ArisenEngine::RHI
