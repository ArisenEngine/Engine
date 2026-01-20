#include "RHIVkGPURenderPass.h"
#include "RHIVkGPUSubPass.h"
#include "Logger/Logger.h"
#include "../Devices/RHIVkDevice.h"
#include <vector>
#include <map>
#include <tuple> // For std::tie

// Helper for comparing VkAttachmentDescription
bool operator<(const VkAttachmentDescription& a, const VkAttachmentDescription& b) {
    return std::tie(a.flags, a.format, a.samples, a.loadOp, a.storeOp, a.stencilLoadOp, a.stencilStoreOp, a.initialLayout, a.finalLayout) <
           std::tie(b.flags, b.format, b.samples, b.loadOp, b.storeOp, b.stencilLoadOp, b.stencilStoreOp, b.initialLayout, b.finalLayout);
}

// Helper for comparing VkAttachmentReference
bool operator<(const VkAttachmentReference& a, const VkAttachmentReference& b) {
    return std::tie(a.attachment, a.layout) < std::tie(b.attachment, b.layout);
}

// Helper for comparing VkSubpassDescription
bool operator<(const VkSubpassDescription& a, const VkSubpassDescription& b) {
    // Compare scalar members
    if (a.flags != b.flags) return a.flags < b.flags;
    if (a.pipelineBindPoint != b.pipelineBindPoint) return a.pipelineBindPoint < b.pipelineBindPoint;
    if (a.inputAttachmentCount != b.inputAttachmentCount) return a.inputAttachmentCount < b.inputAttachmentCount;
    if (a.colorAttachmentCount != b.colorAttachmentCount) return a.colorAttachmentCount < b.colorAttachmentCount;
    if (a.preserveAttachmentCount != b.preserveAttachmentCount) return a.preserveAttachmentCount < b.preserveAttachmentCount;

    // Compare pointers to arrays (deep comparison)
    // Input attachments
    for (uint32_t i = 0; i < a.inputAttachmentCount; ++i) {
        if (a.pInputAttachments[i] < b.pInputAttachments[i]) return true;
        if (b.pInputAttachments[i] < a.pInputAttachments[i]) return false;
    }
    // Color attachments
    for (uint32_t i = 0; i < a.colorAttachmentCount; ++i) {
        if (a.pColorAttachments[i] < b.pColorAttachments[i]) return true;
        if (b.pColorAttachments[i] < a.pColorAttachments[i]) return false;
    }
    // Resolve attachments (if present)
    if (a.pResolveAttachments && b.pResolveAttachments) {
        if (*a.pResolveAttachments < *b.pResolveAttachments) return true;
        if (*b.pResolveAttachments < *a.pResolveAttachments) return false;
    } else if (a.pResolveAttachments && !b.pResolveAttachments) {
        return true;
    } else if (!a.pResolveAttachments && b.pResolveAttachments) {
        return false;
    }
    // Depth/Stencil attachment (if present)
    if (a.pDepthStencilAttachment && b.pDepthStencilAttachment) {
        if (*a.pDepthStencilAttachment < *b.pDepthStencilAttachment) return true;
        if (*b.pDepthStencilAttachment < *a.pDepthStencilAttachment) return false;
    } else if (a.pDepthStencilAttachment && !b.pDepthStencilAttachment) {
        return true;
    } else if (!a.pDepthStencilAttachment && b.pDepthStencilAttachment) {
        return false;
    }
    // Preserve attachments
    for (uint32_t i = 0; i < a.preserveAttachmentCount; ++i) {
        if (a.pPreserveAttachments[i] < b.pPreserveAttachments[i]) return true;
        if (b.pPreserveAttachments[i] < a.pPreserveAttachments[i]) return false;
    }

    return false; // They are equal
}

// Helper for comparing VkSubpassDependency
bool operator<(const VkSubpassDependency& a, const VkSubpassDependency& b) {
    return std::tie(a.srcSubpass, a.dstSubpass, a.srcStageMask, a.dstStageMask, a.srcAccessMask, a.dstAccessMask, a.dependencyFlags) <
           std::tie(b.srcSubpass, b.dstSubpass, b.srcStageMask, b.dstStageMask, b.srcAccessMask, b.dstAccessMask, b.dependencyFlags);
}

namespace ArisenEngine::RHI {

struct RenderPassCacheKey {
    std::vector<VkAttachmentDescription> attachments;
    std::vector<VkSubpassDescription> subpasses;
    std::vector<VkSubpassDependency> dependencies;

    // Custom comparison operator for std::map
    bool operator<(const RenderPassCacheKey& other) const {
        if (attachments < other.attachments) return true;
        if (other.attachments < attachments) return false;

        if (subpasses < other.subpasses) return true;
        if (other.subpasses < subpasses) return false;

        if (dependencies < other.dependencies) return true;
        if (other.dependencies < dependencies) return false;

        return false; // They are equal
    }
};

} // namespace ArisenEngine::RHI

ArisenEngine::RHI::RHIVkGPURenderPass::RHIVkGPURenderPass(RHIVkDevice* device, UInt32 maxFramesInFlight): GPURenderPass(maxFramesInFlight), m_Device(device)
{
    m_VkRenderPasses.resize(maxFramesInFlight);
    for(int i = 0; i < maxFramesInFlight; ++i)
    {
        m_VkRenderPasses[i] = VK_NULL_HANDLE;
    }
}

ArisenEngine::RHI::RHIVkGPURenderPass::~RHIVkGPURenderPass() noexcept
{
    m_SubpassPool.clear();
    m_SubpassesToDispatch.clear();
    FreeAllRenderPasses();
    
}

void* ArisenEngine::RHI::RHIVkGPURenderPass::GetHandle(UInt32 frameIndex)
{
    ASSERT(m_VkRenderPasses[frameIndex % m_MaxFramesInFlight] != VK_NULL_HANDLE);
    return m_VkRenderPasses[frameIndex % m_MaxFramesInFlight];
}

void ArisenEngine::RHI::RHIVkGPURenderPass::AddAttachmentAction(EFormat format, ESampleCountFlagBits sample,
                                                                AttachmentLoadOp colorLoadOp, AttachmentStoreOp colorStoreOp, AttachmentLoadOp stencilLoadOp,
                                                                AttachmentStoreOp stencilStoreOp, EImageLayout initialLayout, EImageLayout finalLayout)
{
    VkAttachmentDescription colorAttachment{};
    colorAttachment.format = static_cast<VkFormat>(format);
    colorAttachment.samples = static_cast<VkSampleCountFlagBits>(sample);
    colorAttachment.loadOp = static_cast<VkAttachmentLoadOp>(colorLoadOp);
    colorAttachment.storeOp = static_cast<VkAttachmentStoreOp>(colorStoreOp);
    colorAttachment.stencilLoadOp = static_cast<VkAttachmentLoadOp>(stencilLoadOp);
    colorAttachment.stencilStoreOp = static_cast<VkAttachmentStoreOp>(stencilStoreOp);
    colorAttachment.initialLayout = static_cast<VkImageLayout>(initialLayout);
    colorAttachment.finalLayout = static_cast<VkImageLayout>(finalLayout);
    
    m_AttachmentDescriptions.emplace_back(colorAttachment);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPURenderPass::GetAttachmentCount()
{
    return static_cast<UInt32>(m_AttachmentDescriptions.size());
}

void ArisenEngine::RHI::RHIVkGPURenderPass::AllocRenderPass(UInt32 frameIndex)
{
    ASSERT(!m_SubpassesToDispatch.empty());
    m_SubpassDescriptions.resize(m_SubpassesToDispatch.size());
    m_Dependencies.resize(m_SubpassesToDispatch.size());
    
    for (int i = 0; i < m_SubpassesToDispatch.size(); ++i)
    {
        auto subpass = m_SubpassesToDispatch[i];
        auto description = subpass->GetDescriptions();
        VkSubpassDescription vkDesc {};
        vkDesc.pipelineBindPoint = static_cast<VkPipelineBindPoint>(description.bindPoint);
        vkDesc.colorAttachmentCount = description.colorRefCount;
        vkDesc.pColorAttachments = static_cast<const VkAttachmentReference*>(description.colorReferences);
        vkDesc.preserveAttachmentCount = description.preserveCount;
        vkDesc.pPreserveAttachments = static_cast<const uint32_t*>(description.preserves);
        
        if (description.inputRefCount.has_value() && description.inputReferences.has_value())
        {
            vkDesc.inputAttachmentCount = description.inputRefCount.value();
            vkDesc.pInputAttachments = static_cast<const VkAttachmentReference*>(description.inputReferences.value());
        }
        if (description.resolveReference.has_value())
        {
            vkDesc.pResolveAttachments = static_cast<const VkAttachmentReference*>(description.resolveReference.value());
        }
        if (description.depthStencilReference.has_value())
        {
            vkDesc.pDepthStencilAttachment = static_cast<const VkAttachmentReference*>(description.depthStencilReference.value());
        }
        if (description.flag.has_value())
        {
            vkDesc.flags = static_cast<VkSubpassDescriptionFlags>(description.flag.value());
        }
        m_SubpassDescriptions[i] = vkDesc;

        auto dependency = subpass->GetDependency();
        VkSubpassDependency vkSubpassDependency;
        vkSubpassDependency.srcSubpass = dependency.previousIndex;
        vkSubpassDependency.dstSubpass = subpass->GetIndex();
        vkSubpassDependency.srcStageMask = dependency.previousStage;
        vkSubpassDependency.srcAccessMask = dependency.previousAccessMask;
        vkSubpassDependency.dstStageMask = dependency.currentStage;
        vkSubpassDependency.dstAccessMask = dependency.currentAccessMask;
        vkSubpassDependency.dependencyFlags = dependency.syncFlag;
        m_Dependencies[i] = vkSubpassDependency;
    }
    
    RenderPassCacheKey key;
    key.attachments = { m_AttachmentDescriptions.begin(), m_AttachmentDescriptions.end() };
    key.subpasses = { m_SubpassDescriptions.begin(), m_SubpassDescriptions.end() };
    key.dependencies = { m_Dependencies.begin(), m_Dependencies.end() };

    auto it = m_RenderPassCache.find(key);
    if (it != m_RenderPassCache.end())
    {
        m_VkRenderPasses[frameIndex % m_MaxFramesInFlight] = it->second;
        return;
    }

    VkRenderPassCreateInfo renderPassInfo{};
    renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
    renderPassInfo.attachmentCount = static_cast<uint32_t>(m_AttachmentDescriptions.size());
    renderPassInfo.pAttachments = m_AttachmentDescriptions.data();
    renderPassInfo.subpassCount = static_cast<uint32_t>(m_SubpassDescriptions.size());
    renderPassInfo.pSubpasses = m_SubpassDescriptions.data();
    renderPassInfo.dependencyCount = static_cast<uint32_t>(m_Dependencies.size());
    renderPassInfo.pDependencies = m_Dependencies.data();
    
    VkRenderPass newPass = VK_NULL_HANDLE;
    auto device = static_cast<VkDevice>(m_Device->GetHandle());
    if (vkCreateRenderPass(device, &renderPassInfo, nullptr, &newPass) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkGPURenderPass::AllocRenderPass]: failed to create render pass!");
    }

    m_VkRenderPasses[frameIndex % m_MaxFramesInFlight] = newPass;
    m_RenderPassCache[key] = newPass;

    LOG_DEBUG("[RHIVkGPURenderPass::AllocRenderPass]: New RenderPass Cached.");
}

void ArisenEngine::RHI::RHIVkGPURenderPass::FreeRenderPass(UInt32 frameIndex)
{
    // Caching means we don't destroy per-frame anymore.
    // Just clear the working data.
    m_AttachmentDescriptions.clear();
    while (!m_SubpassesToDispatch.empty())
    {
        auto subpass = m_SubpassesToDispatch.back();
        m_SubpassesToDispatch.pop_back();
        subpass->ClearAll();
        m_SubpassPool.emplace_back(subpass);
    }
}

void ArisenEngine::RHI::RHIVkGPURenderPass::FreeAllRenderPasses()
{
    auto device = static_cast<VkDevice>(m_Device->GetHandle());
    for (auto const& [key, pass] : m_RenderPassCache)
    {
        if (pass != VK_NULL_HANDLE)
        {
            vkDestroyRenderPass(device, pass, nullptr);
        }
    }
    m_RenderPassCache.clear();
    std::fill(m_VkRenderPasses.begin(), m_VkRenderPasses.end(), (VkRenderPass)VK_NULL_HANDLE);
    LOG_DEBUG("## Destroy All Cached Vulkan Render Passes ##");
}

ArisenEngine::RHI::GPUSubPass* ArisenEngine::RHI::RHIVkGPURenderPass::AddSubPass()
{
    std::shared_ptr<GPUSubPass> subpass;
    if (m_SubpassPool.size() > 0)
    {
        subpass = m_SubpassPool.back();
        static_cast<RHIVkGPUSubPass*>(subpass.get())->Bind(static_cast<UInt32>(m_SubpassesToDispatch.size()));
        m_SubpassPool.pop_back();
    }
    else
    {
        subpass = std::make_shared<RHIVkGPUSubPass>(this, m_SubpassesToDispatch.size());
    }

    m_SubpassesToDispatch.emplace_back(subpass);

    return subpass.get();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkGPURenderPass::GetSubPassCount()
{
    return static_cast<UInt32>(m_SubpassesToDispatch.size());
}

