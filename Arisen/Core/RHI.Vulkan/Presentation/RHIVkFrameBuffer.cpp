#include <cstdio>
#include "Presentation/RHIVkFrameBuffer.h"
#include "RenderPass/RHIVkGPURenderPass.h"
#include "Logger/Logger.h"
#include "Core/RHIVkDevice.h"
#include <vulkan/vulkan_core.h>
#include <tuple>
#include <algorithm>

namespace ArisenEngine::RHI {

ArisenEngine::RHI::RHIVkFrameBuffer::RHIVkFrameBuffer(RHIVkDevice* device, UInt32 maxFramesInFlight): RHIFrameBuffer(maxFramesInFlight), m_Device(device)
{
    m_VkFrameBuffers.resize(maxFramesInFlight);
    for (int i = 0; i < maxFramesInFlight; ++i)
    {
        m_VkFrameBuffers[i] = VK_NULL_HANDLE;
    }
}

ArisenEngine::RHI::RHIVkFrameBuffer::~RHIVkFrameBuffer() noexcept
{
   // This destructor is called via RHIVkDevice::ReleaseFrameBuffer's deferred lambda.
   // Thus, FreeAllFrameBuffers() executes safely on the CPU after the GPU has finished the frame.
   FreeAllFrameBuffers();
}

void* ArisenEngine::RHI::RHIVkFrameBuffer::GetHandle(UInt32 currentFrameIndex)
{
    UInt32 frameIndex = currentFrameIndex % m_MaxFramesInFlight;
    
    // Lazy build if we have pending attachments but no framebuffer yet
    if (m_VkFrameBuffers[frameIndex] == VK_NULL_HANDLE) {
        auto it = m_PendingAttachments.find(frameIndex);
        auto rpIt = m_PendingRenderPasses.find(frameIndex);
        if (it != m_PendingAttachments.end() && rpIt != m_PendingRenderPasses.end()) {
            SetAttachments(currentFrameIndex, it->second, rpIt->second);
        }
    }

    if (m_VkFrameBuffers[frameIndex] == VK_NULL_HANDLE) {
         LOG_WARN(String::Format("[RHIVkFrameBuffer::GetHandle]: Returning VK_NULL_HANDLE for frame %d. Attachments not set?", currentFrameIndex));
    }
    
    return m_VkFrameBuffers[frameIndex];
}

void ArisenEngine::RHI::RHIVkFrameBuffer::SetAttachment(UInt32 frameIndex, RHIImageViewHandle imageView, RHIRenderPass* renderPass)
{
    SetAttachment(frameIndex, imageView, renderPass, 0);
}

void ArisenEngine::RHI::RHIVkFrameBuffer::SetAttachment(UInt32 frameIndex, RHIImageViewHandle imageView, RHIRenderPass* renderPass, UInt32 index)
{
    auto& attachments = m_PendingAttachments[frameIndex % m_MaxFramesInFlight];
    if (index >= attachments.size()) {
        attachments.resize(index + 1, RHIImageViewHandle::Invalid());
    }
    attachments[index] = imageView;
    m_PendingRenderPasses[frameIndex % m_MaxFramesInFlight] = renderPass;

    // We no longer auto-update here to avoid partial framebuffer creation.
    // The framebuffer will be built lazily in GetHandle() when needed by the command buffer.
    m_VkFrameBuffers[frameIndex % m_MaxFramesInFlight] = VK_NULL_HANDLE;
}

void ArisenEngine::RHI::RHIVkFrameBuffer::SetAttachments(UInt32 frameIndex, const Containers::Vector<RHIImageViewHandle>& imageViews, RHIRenderPass* renderPass)
{
    if (imageViews.empty()) return;

    m_ImageView = imageViews[0]; // Track primary for legacy GetAttachFormat
    
    std::vector<VkImageView> vkViews;
    for (auto h : imageViews)
    {
        auto* vkView = m_Device->GetImageViewPool()->Get(h);
        if (vkView) {
            vkViews.push_back(vkView->view);
        }
    }

    auto* primaryView = m_Device->GetImageViewPool()->Get(imageViews[0]);
    if (!primaryView) return;

    // LOG_DEBUGF("[RHIVkFrameBuffer::SetAttachments] FrameIndex={0} Width={1} Height={2}", frameIndex, primaryView->width, primaryView->height);

    FramebufferCacheKey key;
    key.renderPass = static_cast<VkRenderPass>(renderPass->GetHandle(frameIndex));
    key.attachments = vkViews;
    key.width = primaryView->width;
    key.height = primaryView->height;
    key.layers = 1; // Default

    auto it = m_FramebufferCache.find(key);
    if (it != m_FramebufferCache.end())
    {
        m_VkFrameBuffers[frameIndex % m_MaxFramesInFlight] = it->second;
    }
    else
    {
        if (key.width == 0 || key.height == 0)
        {
            LOG_WARNF("[RHIVkFrameBuffer::SetAttachments]: Creating framebuffer with zero dimensions ({0}x{1}). Clamping to 1x1.", key.width, key.height);
            key.width = (std::max)(1u, key.width);
            key.height = (std::max)(1u, key.height);
        }

        VkFramebufferCreateInfo createInfo {};
        createInfo.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
        createInfo.renderPass = key.renderPass;
        createInfo.attachmentCount = static_cast<uint32_t>(vkViews.size());
        createInfo.pAttachments = vkViews.data();
        createInfo.width = key.width;
        createInfo.height = key.height;
        createInfo.layers = key.layers;

        VkFramebuffer newFb = VK_NULL_HANDLE;
        auto device = static_cast<VkDevice>(m_Device->GetHandle());
        if (vkCreateFramebuffer(device, &createInfo, nullptr, &newFb) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkFrameBuffer::SetAttachments]: failed to create RHIFrameBuffer!");
        }

        m_VkFrameBuffers[frameIndex % m_MaxFramesInFlight] = newFb;
        m_FramebufferCache[key] = newFb;
        LOG_DEBUG("[RHIVkFrameBuffer::SetAttachments]: New Vulkan RHIFrameBuffer Cached.");
    }

    m_RenderArea.height = key.height;
    m_RenderArea.width = key.width;
    m_RenderArea.offsetX = 0;
    m_RenderArea.offsetY = 0;
}

ArisenEngine::RHI::EFormat ArisenEngine::RHI::RHIVkFrameBuffer::GetAttachFormat()
{
    auto* vkView = m_Device->GetImageViewPool()->Get(m_ImageView);
    ASSERT(vkView != nullptr);
    return vkView->format;
}

void ArisenEngine::RHI::RHIVkFrameBuffer::FreeFrameBuffer(UInt32 currentFrameIndex)
{
    // Caching means we don't destroy per-frame.
    // Just clear the working reference.
    m_ImageView = RHIImageViewHandle::Invalid();
    m_VkFrameBuffers[currentFrameIndex % m_MaxFramesInFlight] = VK_NULL_HANDLE;
}

void ArisenEngine::RHI::RHIVkFrameBuffer::FreeAllFrameBuffers()
{
    auto device = static_cast<VkDevice>(m_Device->GetHandle());
    for (auto const& [key, fb] : m_FramebufferCache)
    {
        if (fb != VK_NULL_HANDLE)
        {
            vkDestroyFramebuffer(device, fb, nullptr);
        }
    }
    m_FramebufferCache.clear();
    std::fill(m_VkFrameBuffers.begin(), m_VkFrameBuffers.end(), (VkFramebuffer)VK_NULL_HANDLE);
    LOG_DEBUG("## Destroy All Cached Vulkan Frame Buffers ##");
    m_VkFrameBuffers.clear();
}

} // namespace ArisenEngine::RHI




