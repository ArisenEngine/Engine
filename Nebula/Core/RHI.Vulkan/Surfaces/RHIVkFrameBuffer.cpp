#include "RHIVkFrameBuffer.h"

#include "Logger/Logger.h"

// NebulaEngine::RHI::RHIVkFrameBuffer::RHIVkFrameBuffer(VkDevice device, FrameBufferDesc&& desc) : FrameBuffer(), m_VkDevice(device)
// {
//     VkFramebufferCreateInfo createInfo {};
//     createInfo.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
//     createInfo.renderPass = static_cast<VkRenderPass>(desc.renderPass.GetHandle());
//     createInfo.attachmentCount = desc.attachmentCount;
//     createInfo.pAttachments = static_cast<const VkImageView*>(desc.attachments);
//     createInfo.width = desc.width;
//     createInfo.height = desc.height;
//     createInfo.layers = desc.layerCount;
//     
//     if (vkCreateFramebuffer(device, &createInfo, nullptr, &m_VkFrameBuffer) != VK_SUCCESS)
//     {
//         LOG_ERROR("[RHIVkFrameBuffer::RHIVkFrameBuffer]: failed to create framebuffer!");
//     }
// }

NebulaEngine::RHI::RHIVkFrameBuffer::RHIVkFrameBuffer(VkDevice device): FrameBuffer(), m_VkDevice(device)
{
    
}

NebulaEngine::RHI::RHIVkFrameBuffer::~RHIVkFrameBuffer() noexcept
{
    if(m_VkFrameBuffer != VK_NULL_HANDLE)
    {
        vkDestroyFramebuffer(m_VkDevice, m_VkFrameBuffer, nullptr);
        LOG_DEBUG("## Destroy Vulkan Frame Buffer ##");
    }
}
