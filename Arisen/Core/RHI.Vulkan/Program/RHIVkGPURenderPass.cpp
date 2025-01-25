#include "RHIVkGPURenderPass.h"
#include "RHIVkGPUSubPass.h"
#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkGPURenderPass::RHIVkGPURenderPass(VkDevice device, UInt32 maxFramesInFlight): GPURenderPass(maxFramesInFlight), m_VkDevice(device)
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
    ASSERT(m_SubpassesToDispatch.size() > 0);
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

        // deal with dependency;
        auto dependency = subpass->GetDependency();
        VkSubpassDependency vkSubpassDepency;
        vkSubpassDepency.srcSubpass = dependency.previousIndex;
        vkSubpassDepency.dstSubpass = subpass->GetIndex();
        vkSubpassDepency.srcStageMask = dependency.previousStage;
        vkSubpassDepency.srcAccessMask = dependency.previousAccessMask;
        vkSubpassDepency.dstStageMask = dependency.currentStage;
        vkSubpassDepency.dstAccessMask = dependency.currentAccessMask;
        vkSubpassDepency.dependencyFlags = dependency.syncFlag;
        m_Dependencies[i] = vkSubpassDepency;
    }
    
    VkRenderPassCreateInfo renderPassInfo{};
    renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO;
    renderPassInfo.attachmentCount = static_cast<uint32_t>(m_AttachmentDescriptions.size());
    renderPassInfo.pAttachments = m_AttachmentDescriptions.data();
    renderPassInfo.subpassCount = static_cast<uint32_t>(m_SubpassDescriptions.size());
    renderPassInfo.pSubpasses = m_SubpassDescriptions.data();
    renderPassInfo.dependencyCount = static_cast<uint32_t>(m_Dependencies.size());
    renderPassInfo.pDependencies = m_Dependencies.data();
    
    if (vkCreateRenderPass(m_VkDevice, &renderPassInfo, nullptr, &m_VkRenderPasses[frameIndex % m_MaxFramesInFlight]) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkGPURenderPass::AllocRenderPass]: failed to create render pass!");
    }

    LOG_DEBUG("[RHIVkGPURenderPass::AllocRenderPass]: RenderPass Allocated.");
}

void ArisenEngine::RHI::RHIVkGPURenderPass::FreeRenderPass(UInt32 frameIndex)
{
    m_AttachmentDescriptions.clear();
    while (m_SubpassesToDispatch.size() > 0)
    {
        auto subpass = m_SubpassesToDispatch.back();
        m_SubpassesToDispatch.pop_back();
        subpass->ClearAll();
        m_SubpassPool.emplace_back(subpass);
    }
    
    if (m_VkRenderPasses[frameIndex % m_MaxFramesInFlight] != VK_NULL_HANDLE)
    {
        vkDestroyRenderPass(m_VkDevice, m_VkRenderPasses[frameIndex % m_MaxFramesInFlight], nullptr);
        LOG_DEBUG("## Destroy Vulkan Render Pass ##");
    }
}

void ArisenEngine::RHI::RHIVkGPURenderPass::FreeAllRenderPasses()
{
    for(int i = 0; i < m_MaxFramesInFlight; ++i)
    {
        if (m_VkRenderPasses[i] != VK_NULL_HANDLE)
        {
            vkDestroyRenderPass(m_VkDevice, m_VkRenderPasses[i], nullptr);
        }
    }
    m_VkRenderPasses.clear();
    LOG_DEBUG("## Destroy All Vulkan Render Passes ##");
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

