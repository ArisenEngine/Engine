#include "RHIVkFrameBuffer.h"
#include "Logger/Logger.h"
#include "RHI/Memory/ImageView.h"

NebulaEngine::RHI::RHIVkFrameBuffer::RHIVkFrameBuffer(VkDevice device): FrameBuffer(), m_VkDevice(device)
{
    
}

NebulaEngine::RHI::RHIVkFrameBuffer::~RHIVkFrameBuffer() noexcept
{
   FreeFrameBuffer();
}

void NebulaEngine::RHI::RHIVkFrameBuffer::SetAttachment(ImageView* imageView, GPURenderPass* renderPass)
{
    // TODO: use cache?
    FreeFrameBuffer();

    m_ImageView = imageView;
    
    VkFramebufferCreateInfo createInfo {};
    createInfo.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
    createInfo.renderPass = static_cast<VkRenderPass>(renderPass->GetHandle());
    createInfo.attachmentCount = 1;
    createInfo.pAttachments = static_cast<const VkImageView*>(imageView->GetViewPointer());
    createInfo.width = imageView->GetWidth();
    createInfo.height = imageView->GetHeight();
    createInfo.layers = imageView->GetLayerCount();

    if (vkCreateFramebuffer(m_VkDevice, &createInfo, nullptr, &m_VkFrameBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkFrameBuffer::SetAttachment]: failed to create framebuffer!");
    }

    LOG_DEBUG("[RHIVkFrameBuffer::SetAttachment]: create vulkan frame buffer.");
    m_RenderArea.height = imageView->GetHeight();
    m_RenderArea.width = imageView->GetWidth();
    m_RenderArea.offsetX = 0;
    m_RenderArea.offsetY = 0;
}

NebulaEngine::RHI::Format NebulaEngine::RHI::RHIVkFrameBuffer::GetAttachFormat()
{
    ASSERT(m_ImageView != nullptr);
    return m_ImageView->GetFormat();
}

void NebulaEngine::RHI::RHIVkFrameBuffer::FreeFrameBuffer()
{
    if(m_VkFrameBuffer != VK_NULL_HANDLE)
    {
        vkDestroyFramebuffer(m_VkDevice, m_VkFrameBuffer, nullptr);
        LOG_DEBUG("## Destroy Vulkan Frame Buffer ##");
        m_VkFrameBuffer = VK_NULL_HANDLE;
    }

    m_ImageView = nullptr;
}
