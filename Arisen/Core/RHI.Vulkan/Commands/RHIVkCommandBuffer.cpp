#include "Commands/RHIVkCommandBuffer.h"

#include "Commands/RHIVkCommandBufferPool.h"
#include "Core/RHIVkDevice.h"
#include "Pipeline/RHIVkGPUPipeline.h"
#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "Utils/RHIVkInitializer.h"
#include "Descriptors/RHIVkBindlessManager.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Sync/RHIBufferMemoryBarrier.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Sync/RHIMemoryBarrier.h"
#include "Concurrency/SyncScope.h"
#include "Allocation/RHIVkMemoryAllocator.h"


namespace ArisenEngine::RHI
{

RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
{
    // Ensure resources are released if the buffer is destroyed before submission
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* registry = vkDevice->GetResourceRegistry();
    if (registry)
    {
        for (auto h : m_TrackedResourceHandles)
        {
            registry->Release(h, RHIQueueType::Graphics, 0);
        }
    }
}

RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool, ECommandBufferLevel level)
: RHICommandBuffer(device, pool, level),
m_OwnerThreadId(std::this_thread::get_id()),
m_OwnerThreadIndex(ThreadRegistry::GetThreadIndex())
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    m_VkCommandPool = pool->GetCurrentThreadSlot().commandPool;

    // Alloc Memory
    {
        VkCommandBufferAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
        allocInfo.commandPool = m_VkCommandPool;
        allocInfo.level = (level == COMMAND_BUFFER_LEVEL_PRIMARY) ? VK_COMMAND_BUFFER_LEVEL_PRIMARY : VK_COMMAND_BUFFER_LEVEL_SECONDARY;
        allocInfo.commandBufferCount = 1;

        // todo: separate alloc memory and free memory
        if (::vkAllocateCommandBuffers(m_VkDevice, &allocInfo, &m_VkCommandBuffer) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::RHIVkCommandBuffer]: failed to allocate command buffers!");
        }
    }
    
    SetState(ECommandBufferState::Initial);
}


void RHIVkCommandBuffer::BeginRenderPass(RenderPassBeginDesc&& desc)
{
    ASSERT(GetState() == ECommandBufferState::Recording);
    UInt32 frameIndex = GetCurrentFrameIndex();
    
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* rp = vkDevice->GetRenderPassPool()->Get(desc.renderPass);
    auto* fb = vkDevice->GetFrameBufferPool()->Get(desc.frameBuffer);

    if (!rp || !fb) {
        LOG_ERROR("[RHIVkCommandBuffer::BeginRenderPass]: invalid renderPass or RHIFrameBuffer handle!");
        return;
    }

    VkRenderPassBeginInfo renderPassInfo{};
    renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;
    
    // Retrieve backend objects for the specific frame
    auto* rpObj = static_cast<RHIVkGPURenderPass*>(rp->renderPassObj);
    if (rpObj) {
         renderPassInfo.renderPass = static_cast<VkRenderPass>(rpObj->GetHandle(frameIndex));
    } else {
         renderPassInfo.renderPass = rp->renderPass;
    }
    
    if (fb->frameBufferObj) {
        auto* fbObj = static_cast<RHIVkFrameBuffer*>(fb->frameBufferObj);
        renderPassInfo.framebuffer = static_cast<VkFramebuffer>(fbObj->GetHandle(frameIndex));
    } else {
        renderPassInfo.framebuffer = fb->framebuffer;
    }
    
    renderPassInfo.renderArea.offset = {0, 0};
    
    // Use actual RHIFrameBuffer dimensions if available
    renderPassInfo.renderArea.extent = { (UInt32)fb->width, (UInt32)fb->height };
    if (fb->frameBufferObj) {
        auto* fbObj = static_cast<RHIVkFrameBuffer*>(fb->frameBufferObj);
        renderPassInfo.renderArea.extent.width = fbObj->GetRenderArea().width;
        renderPassInfo.renderArea.extent.height = fbObj->GetRenderArea().height;
    }
    
    // Fallback if dimensions are unknown
    if (renderPassInfo.renderArea.extent.width == 0 || renderPassInfo.renderArea.extent.height == 0) {
        renderPassInfo.renderArea.extent.width = 1280;
        renderPassInfo.renderArea.extent.height = 720;
    }
    
    renderPassInfo.clearValueCount = desc.clearValueCount;
    renderPassInfo.pClearValues = reinterpret_cast<const VkClearValue*>(desc.pClearValues);

    ::vkCmdBeginRenderPass(m_VkCommandBuffer, &renderPassInfo, static_cast<VkSubpassContents>(desc.subpassContents));

    SetState(ECommandBufferState::RecordingPass);
}

void RHIVkCommandBuffer::EndRenderPass()
{
    ASSERT(GetState() == ECommandBufferState::RecordingPass);
    ::vkCmdEndRenderPass(m_VkCommandBuffer);
    SetState(ECommandBufferState::Recording);
}

void RHIVkCommandBuffer::BeginRendering(const RHIRenderingInfo& info)
{
    ASSERT(GetState() == ECommandBufferState::Recording);

    VkRenderingInfoKHR vkInfo = {};
    vkInfo.sType = VK_STRUCTURE_TYPE_RENDERING_INFO_KHR;
    vkInfo.renderArea.offset = { info.RHIRenderArea.x, info.RHIRenderArea.y };
    vkInfo.renderArea.extent = { info.RHIRenderArea.width, info.RHIRenderArea.height };
    vkInfo.layerCount = info.layerCount;
    vkInfo.colorAttachmentCount = info.colorAttachmentCount;

    m_VkColorAttachments.clear();
    m_VkColorAttachments.reserve(info.colorAttachmentCount);

    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());

    for (UInt32 i = 0; i < info.colorAttachmentCount; ++i)
    {
        const auto& att = info.pColorAttachments[i];
        auto* view = vkDevice->GetImageViewPool()->Get(att.imageView);
        if (!view) continue;
   
        VkRenderingAttachmentInfoKHR vkAtt{};
        vkAtt.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO_KHR;
        vkAtt.imageView = view->view;
        vkAtt.imageLayout = static_cast<VkImageLayout>(att.imageLayout);
        vkAtt.loadOp = static_cast<VkAttachmentLoadOp>(att.loadOp);
        vkAtt.storeOp = static_cast<VkAttachmentStoreOp>(att.storeOp);
        
        // Copy clear value
        std::memcpy(&vkAtt.clearValue, &att.clearValue, sizeof(VkClearValue));
        if (info.pResolveAttachments != nullptr)
        {
            const auto& resolveAtt = info.pResolveAttachments[i];
            auto* resolveView = vkDevice->GetImageViewPool()->Get(resolveAtt.imageView);
            if (resolveView)
            {
                vkAtt.resolveImageView = resolveView->view;
                vkAtt.resolveImageLayout = static_cast<VkImageLayout>(resolveAtt.imageLayout);
                vkAtt.resolveMode = VK_RESOLVE_MODE_AVERAGE_BIT;
            }
        }

        m_VkColorAttachments.emplace_back(vkAtt);
    }

    if (info.pDepthAttachment != nullptr)
    {
        const auto& att = *info.pDepthAttachment;
        auto* view = vkDevice->GetImageViewPool()->Get(att.imageView);
        if (view) {
            m_VkDepthAttachment.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO_KHR;
            m_VkDepthAttachment.imageView = view->view;
            m_VkDepthAttachment.imageLayout = static_cast<VkImageLayout>(att.imageLayout);
            m_VkDepthAttachment.loadOp = static_cast<VkAttachmentLoadOp>(att.loadOp);
            m_VkDepthAttachment.storeOp = static_cast<VkAttachmentStoreOp>(att.storeOp);
            std::memcpy(&m_VkDepthAttachment.clearValue, &att.clearValue, sizeof(VkClearValue));
        }
    }

    if (info.pStencilAttachment != nullptr)
    {
        const auto& att = *info.pStencilAttachment;
        auto* view = vkDevice->GetImageViewPool()->Get(att.imageView);
        if (view) {
            m_VkStencilAttachment.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO_KHR;
            m_VkStencilAttachment.imageView = view->view;
            m_VkStencilAttachment.imageLayout = static_cast<VkImageLayout>(att.imageLayout);
            m_VkStencilAttachment.loadOp = static_cast<VkAttachmentLoadOp>(att.loadOp);
            m_VkStencilAttachment.storeOp = static_cast<VkAttachmentStoreOp>(att.storeOp);
            std::memcpy(&m_VkStencilAttachment.clearValue, &att.clearValue, sizeof(VkClearValue));
        }
    }

    vkInfo.pColorAttachments = m_VkColorAttachments.data();
    
    if (info.pDepthAttachment != nullptr)
    {
        vkInfo.pDepthAttachment = &m_VkDepthAttachment;
    }

    if (info.pStencilAttachment != nullptr)
    {
        vkInfo.pStencilAttachment = &m_VkStencilAttachment;
    }

    if (vkDevice->vkCmdBeginRenderingKHR)
    {
        vkDevice->vkCmdBeginRenderingKHR(m_VkCommandBuffer, &vkInfo);
    }
    else
    {
        LOG_ERROR("[RHIVkCommandBuffer::BeginRendering]: vkCmdBeginRenderingKHR not found!");
    }

    SetState(ECommandBufferState::RecordingPass);
}

void RHIVkCommandBuffer::EndRendering()
{
    ASSERT(GetState() == ECommandBufferState::RecordingPass);

    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (vkDevice->vkCmdEndRenderingKHR)
    {
        vkDevice->vkCmdEndRenderingKHR(m_VkCommandBuffer);
    }
    else
    {
        LOG_ERROR("[RHIVkCommandBuffer::EndRendering]: vkCmdEndRenderingKHR not found!");
    }
    SetState(ECommandBufferState::Recording);
}


void RHIVkCommandBuffer::TrackDescriptorPoolUse(RHIDescriptorPool* pool, UInt32 poolId)
{
    if (pool == nullptr) return;
    // avoid duplicates
    for (const auto& t : m_TrackedDescriptorPools)
    {
        if (t.pool == pool && t.poolId == poolId) return;
    }
    m_TrackedDescriptorPools.emplace_back(TrackedPoolUse{ pool, poolId });
}

void RHIVkCommandBuffer::CaptureResource(RHIBufferHandle buffer)
{
    if (!buffer.IsValid()) return;
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(buffer);
    if (!buf) return;

    auto handle = buf->registryHandle;
    
    // Performance: skip redundant tracking
    for (const auto& h : m_TrackedResourceHandles)
    {
        if (h.index == handle.index && h.generation == handle.generation) goto check_mem;
    }

    if (handle.IsValid())
    {
        m_TrackedResourceHandles.emplace_back(handle);
        vkDevice->GetResourceRegistry()->Retain(handle);
    }
    
check_mem:
    // Memory is now managed by VMA and bound to the buffer/image. 
    // We don't have a separate RHIDeviceMemory handle to track for resource lifetime in the same way.
    return;
}

void RHIVkCommandBuffer::CaptureResource(RHIImageHandle image)
{
    if (!image.IsValid()) return;
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* img = vkDevice->GetImagePool()->Get(image);
    if (!img) return;

    auto handle = img->registryHandle;
    
    for (const auto& h : m_TrackedResourceHandles)
    {
        if (h.index == handle.index && h.generation == handle.generation) goto check_mem;
    }

    if (handle.IsValid())
    {
        m_TrackedResourceHandles.emplace_back(handle);
        vkDevice->GetResourceRegistry()->Retain(handle);
    }

check_mem:
    // Memory is now managed by VMA and bound to the buffer/image.
    return;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Begin()
{
    // If Begin() is called without frameIndex, we assume it's already set or not needed for this buffer
    Begin(GetCurrentFrameIndex(), 0, nullptr);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Begin(UInt32 frameIndex, UInt32 commandBufferUsage, const RHICommandBufferInheritanceInfo* pInheritanceInfo)
{
    ASSERT(GetState() == ECommandBufferState::Initial);
    SetCurrentFrameIndex(frameIndex);

    VkCommandBufferInheritanceInfo inheritanceInfo{};
    if (GetLevel() == COMMAND_BUFFER_LEVEL_SECONDARY && pInheritanceInfo)
    {
        inheritanceInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_INHERITANCE_INFO;
        auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
        
        if (pInheritanceInfo->renderPass.IsValid())
        {
            auto* rp = vkDevice->GetRenderPassPool()->Get(pInheritanceInfo->renderPass);
            if (rp)
            {
                auto* rpObj = static_cast<RHIVkGPURenderPass*>(rp->renderPassObj);
                inheritanceInfo.renderPass = rpObj ? static_cast<VkRenderPass>(rpObj->GetHandle(frameIndex)) : rp->renderPass;
            }
        }
        
        inheritanceInfo.subpass = pInheritanceInfo->subpass;
        
        if (pInheritanceInfo->frameBuffer.IsValid())
        {
            auto* fb = vkDevice->GetFrameBufferPool()->Get(pInheritanceInfo->frameBuffer);
            if (fb)
            {
                auto* fbObj = static_cast<RHIVkFrameBuffer*>(fb->frameBufferObj);
                inheritanceInfo.framebuffer = fbObj ? static_cast<VkFramebuffer>(fbObj->GetHandle(frameIndex)) : fb->framebuffer;
            }
        }
        
        if (pInheritanceInfo->occlusionQueryEnable)
        {
            inheritanceInfo.occlusionQueryEnable = VK_TRUE;
            inheritanceInfo.queryFlags = pInheritanceInfo->occlusionQueryFlags;
        }
        inheritanceInfo.pipelineStatistics = pInheritanceInfo->pipelineStatistics;
    }

    m_VkBeginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    m_VkBeginInfo.flags = commandBufferUsage;
    m_VkBeginInfo.pInheritanceInfo = (GetLevel() == COMMAND_BUFFER_LEVEL_SECONDARY && pInheritanceInfo) ? &inheritanceInfo : nullptr;

    if (vkBeginCommandBuffer(m_VkCommandBuffer, &m_VkBeginInfo) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to begin recording command buffer!");
    }

    SetState(ECommandBufferState::Recording);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::End()
{
// ASSERT(m_WaitSemaphores.size() == m_WaitStages.size()); // Removed
    
    if (vkEndCommandBuffer(m_VkCommandBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::End]: failed to record command buffer!");
    }
    
    SetState(ECommandBufferState::Executable);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::ExecuteCommands(Containers::Vector<RHICommandBuffer*>&& secondaryBuffers)
{
    ASSERT(GetState() == ECommandBufferState::Recording || GetState() == ECommandBufferState::RecordingPass);
    ASSERT(GetLevel() == COMMAND_BUFFER_LEVEL_PRIMARY);

    m_VkSecondaryCommandBuffers.clear();
    m_VkSecondaryCommandBuffers.reserve(secondaryBuffers.size());
    for (auto* buf : secondaryBuffers)
    {
        ASSERT(buf->GetLevel() == COMMAND_BUFFER_LEVEL_SECONDARY);
        ASSERT(buf->ReadyForSubmit());
        auto* vkBuf = static_cast<RHIVkCommandBuffer*>(buf);
        m_VkSecondaryCommandBuffers.push_back(vkBuf->m_VkCommandBuffer);
    }

    if (!m_VkSecondaryCommandBuffers.empty())
    {
        ::vkCmdExecuteCommands(m_VkCommandBuffer, static_cast<uint32_t>(m_VkSecondaryCommandBuffers.size()), m_VkSecondaryCommandBuffers.data());
    }
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth, Float32 maxDepth)
{
    const VkViewport viewport
    {
        x, y, width, height, minDepth, maxDepth
    };
    vkCmdSetViewport(m_VkCommandBuffer, 0, 1, &viewport);
    
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height)
{
    const VkViewport viewport
   {
       x, y, width, height
   };
    vkCmdSetViewport(m_VkCommandBuffer, 0, 1, &viewport);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height)
{
    const VkRect2D scissor =
    {
        {0, 0}, {width, height}
    };

    vkCmdSetScissor(m_VkCommandBuffer, 0, 1, &scissor);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetLineWidth(Float32 lineWidth)
{
    vkCmdSetLineWidth(m_VkCommandBuffer, lineWidth);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp, Float32 depthBiasSlopeFactor)
{
    vkCmdSetDepthBias(m_VkCommandBuffer, depthBiasConstantFactor, depthBiasClamp, depthBiasSlopeFactor);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetBlendConstants(const Float32 blendConstants[4])
{
    vkCmdSetBlendConstants(m_VkCommandBuffer, blendConstants);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SetStencilReference(UInt32 faceMask, UInt32 reference)
{
    vkCmdSetStencilReference(m_VkCommandBuffer, static_cast<VkStencilFaceFlags>(faceMask), reference);
}

void RHIVkCommandBuffer::SetCullMode(ECullModeFlagBits cullMode)
{
    // Note: requires VK_EXT_extended_dynamic_state or Vulkan 1.3
    ::vkCmdSetCullMode(m_VkCommandBuffer, static_cast<VkCullModeFlags>(cullMode));
}

void RHIVkCommandBuffer::SetFrontFace(EFrontFace frontFace)
{
    ::vkCmdSetFrontFace(m_VkCommandBuffer, static_cast<VkFrontFace>(frontFace));
}

void RHIVkCommandBuffer::SetPrimitiveTopology(EPrimitiveTopology topology)
{
    ::vkCmdSetPrimitiveTopology(m_VkCommandBuffer, static_cast<VkPrimitiveTopology>(topology));
}

void RHIVkCommandBuffer::SetDepthTestEnable(bool enable)
{
    ::vkCmdSetDepthTestEnable(m_VkCommandBuffer, static_cast<VkBool32>(enable));
}

void RHIVkCommandBuffer::SetDepthWriteEnable(bool enable)
{
    ::vkCmdSetDepthWriteEnable(m_VkCommandBuffer, static_cast<VkBool32>(enable));
}

void RHIVkCommandBuffer::SetDepthCompareOp(ECompareOp depthCompareOp)
{
    ::vkCmdSetDepthCompareOp(m_VkCommandBuffer, static_cast<VkCompareOp>(depthCompareOp));
}

void RHIVkCommandBuffer::SetStencilTestEnable(bool enable)
{
    ::vkCmdSetStencilTestEnable(m_VkCommandBuffer, static_cast<VkBool32>(enable));
}


void RHIVkCommandBuffer::SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp, ECompareOp compareOp)
{
    ::vkCmdSetStencilOp(m_VkCommandBuffer, static_cast<VkStencilFaceFlags>(faceMask), 
        static_cast<VkStencilOp>(failOp), static_cast<VkStencilOp>(passOp), 
        static_cast<VkStencilOp>(depthFailOp), static_cast<VkCompareOp>(compareOp));
}

void RHIVkCommandBuffer::BindPipeline(RHIPipelineHandle pipelineHandle)
{
    UInt32 frameIndex = GetCurrentFrameIndex();
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* p = vkDevice->GetPipelinePool()->Get(pipelineHandle);
    if (!p || !p->pipeline) return;

    RHIPipeline* pipeline = p->pipeline;
    m_CurrentPipeline = pipeline;

    auto* vkPipeline = static_cast<RHIVkGPUPipeline*>(pipeline);
    if (vkPipeline->GetVkPipeline(frameIndex) == VK_NULL_HANDLE)
    {
        if (pipeline->GetBindPoint() == PIPELINE_BIND_POINT_GRAPHICS)
        {
            vkPipeline->AllocGraphicPipeline(frameIndex, nullptr);
        }
        else if (pipeline->GetBindPoint() == PIPELINE_BIND_POINT_COMPUTE)
        {
            vkPipeline->AllocComputePipeline(frameIndex);
        }
        else if (pipeline->GetBindPoint() == PIPELINE_BIND_POINT_RAY_TRACING_KHR)
        {
            vkPipeline->AllocRayTracingPipeline(frameIndex);
        }
    }

    ::vkCmdBindPipeline(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
        static_cast<VkPipeline>(vkPipeline->GetVkPipeline(frameIndex)));

    // Bind Global Bindless Descriptor Set (Set 3)
    auto* bindlessManager = vkDevice->GetBindlessManager();
    if (bindlessManager)
    {
        VkDescriptorSet bindlessSet = bindlessManager->GetDescriptorSet();
        auto* vkPipeline = static_cast<RHIVkGPUPipeline*>(pipeline);
        ::vkCmdBindDescriptorSets(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
            vkPipeline->GetPipelineLayout(frameIndex),
            3, 1, &bindlessSet, 0, nullptr);
    }
}

void RHIVkCommandBuffer::BindDescriptorSets(EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets)
{
    UInt32 frameIndex = GetCurrentFrameIndex();
    if (m_CurrentPipeline == nullptr)
    {
        LOG_FATAL("[RHIVkCommandBuffer::BindDescriptorSets] pipeline is null, should binding pipeline first.");
        return;
    }

    RHIVkGPUPipeline* pipeline = static_cast<RHIVkGPUPipeline*>(m_CurrentPipeline);

    m_VkDescriptorSets.clear();
    m_VkDescriptorSets.reserve(descriptorsets.size());
    for (UInt32 i = 0; i < descriptorsets.size(); ++i)
    {
        m_VkDescriptorSets.emplace_back(static_cast<VkDescriptorSet>(descriptorsets[i]->GetHandle()));
    }
    ::vkCmdBindDescriptorSets(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(bindPoint),
        pipeline->GetPipelineLayout(frameIndex),
        firstSet, static_cast<uint32_t>(m_VkDescriptorSets.size()),
        m_VkDescriptorSets.data(),
        dynamicOffsetCount, pDynamicOffsets);
}

void RHIVkCommandBuffer::PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags)
{
    UInt32 frameIndex = GetCurrentFrameIndex();
    if (m_CurrentPipeline == nullptr)
    {
        LOG_FATAL("[RHIVkCommandBuffer::PushConstants] pipeline is null, should binding pipeline first.");
        return;
    }

    RHIVkGPUPipeline* pipeline = static_cast<RHIVkGPUPipeline*>(m_CurrentPipeline);
    ::vkCmdPushConstants(m_VkCommandBuffer, pipeline->GetPipelineLayout(frameIndex),
        static_cast<VkShaderStageFlags>(stageFlags), offset, size, data);
}

void RHIVkCommandBuffer::CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst,
            EImageLayout dstImageLayout, Containers::Vector<RHIBufferImageCopy>&& regions)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* srcBuf = vkDevice->GetBufferPool()->Get(srcBuffer);
    auto* dstImg = vkDevice->GetImagePool()->Get(dst);

    if (!srcBuf || !dstImg) return;

    m_VkBufferImageCopies.clear();
    m_VkBufferImageCopies.reserve(regions.size());
    for (UInt32 i = 0; i < regions.size(); ++i)
    {
        auto regionInfo = regions[i];
        m_VkBufferImageCopies.emplace_back(BufferImageCopyRegion(regionInfo.bufferOffset,
        regionInfo.bufferRowLength,
        regionInfo.bufferImageHeight,
        regionInfo.imageSubresource,
        regionInfo.offsetX, regionInfo.offsetY, regionInfo.offsetZ,
        regionInfo.width, regionInfo.height, regionInfo.depth));
    }
    
    ::vkCmdCopyBufferToImage(m_VkCommandBuffer,
        srcBuf->buffer, dstImg->image,
        static_cast<VkImageLayout>(dstImageLayout), static_cast<uint32_t>(m_VkBufferImageCopies.size()), m_VkBufferImageCopies.data()
        );
    
    CaptureResource(srcBuffer);
    CaptureResource(dst);
}

void RHIVkCommandBuffer::TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* img = vkDevice->GetImagePool()->Get(image);
    if (!img) return;

    TransitionImageLayout(image, static_cast<EImageLayout>(img->currentLayout), targetLayout);
}

void RHIVkCommandBuffer::TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout)
{
    if (oldLayout == targetLayout) return;

    RHIImageMemoryBarrier barrier{};
    barrier.image = image;
    barrier.oldLayout = oldLayout;
    barrier.newLayout = targetLayout;
    barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
    barrier.subresourceRange = { IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1 }; // Default to color, 1 level, 1 layer

    // Automatic inference of stages and access masks
    barrier.srcStageMask = PIPELINE_STAGE_ALL_COMMANDS_BIT;
    barrier.dstStageMask = PIPELINE_STAGE_ALL_COMMANDS_BIT;
    barrier.srcAccess = static_cast<EAccessFlag>(ACCESS_MEMORY_READ_BIT | ACCESS_MEMORY_WRITE_BIT);
    barrier.dstAccess = static_cast<EAccessFlag>(ACCESS_MEMORY_READ_BIT | ACCESS_MEMORY_WRITE_BIT);

    // Common transition refinements
    if (oldLayout == IMAGE_LAYOUT_UNDEFINED)
    {
        barrier.srcStageMask = PIPELINE_STAGE_TOP_OF_PIPE_BIT;
        barrier.srcAccess = ACCESS_NONE;
    }

    if (targetLayout == IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL)
    {
        barrier.dstStageMask = PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
        barrier.dstAccess = ACCESS_COLOR_ATTACHMENT_WRITE_BIT;
    }
    else if (targetLayout == IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL)
    {
        barrier.dstStageMask = static_cast<EPipelineStageFlag>(PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT | PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT);
        barrier.dstAccess = ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT;
        barrier.subresourceRange.aspectMask = IMAGE_ASPECT_DEPTH_BIT;
    }
    else if (targetLayout == IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL)
    {
        barrier.dstStageMask = PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
        barrier.dstAccess = ACCESS_SHADER_READ_BIT;
    }
    else if (targetLayout == IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL)
    {
        barrier.dstStageMask = PIPELINE_STAGE_TRANSFER_BIT;
        barrier.dstAccess = ACCESS_TRANSFER_WRITE_BIT;
    }
    else if (targetLayout == IMAGE_LAYOUT_PRESENT_SRC_KHR)
    {
        barrier.dstStageMask = PIPELINE_STAGE_BOTTOM_OF_PIPE_BIT;
        barrier.dstAccess = ACCESS_NONE;
    }

    PipelineBarrier(barrier.srcStageMask, barrier.dstStageMask, 0, &barrier, 1);
}

void RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount,
    const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount,
    const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount)
{
    m_VkMemoryBarriers.clear();
    m_VkMemoryBarriers.reserve(memoryBarrierCount);
    m_VkBufferMemoryBarriers.clear();
    m_VkBufferMemoryBarriers.reserve(bufferMemoryBarrierCount);
    m_VkImageMemoryBarriers.clear();
    m_VkImageMemoryBarriers.reserve(imageMemoryBarrierCount);

    for (UInt32 i = 0; i < memoryBarrierCount; ++i)
    {
        const auto& barrier = pMemoryBarriers[i];
        m_VkMemoryBarriers.emplace_back(MemoryBarrier2(
            MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE ? barrier.srcStageMask : srcStage),
            MapAccessFlags2(barrier.srcAccessMask),
            MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE ? barrier.dstStageMask : dstStage),
            MapAccessFlags2(barrier.dstAccessMask)));
    }
    
    for (UInt32 i = 0; i < bufferMemoryBarrierCount; ++i)
    {
        const auto& barrier = pBufferMemoryBarriers[i];
        
        // Resolve Buffer Handle
        auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
        auto* buf = vkDevice->GetBufferPool()->Get(barrier.buffer);
        if (!buf) continue;

        m_VkBufferMemoryBarriers.emplace_back(BufferMemoryBarrier2(
            MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE ? barrier.srcStageMask : srcStage),
            MapAccessFlags2(barrier.srcAccessMask),
            MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE ? barrier.dstStageMask : dstStage),
            MapAccessFlags2(barrier.dstAccessMask),
            barrier.srcQueueFamilyIndex, barrier.dstQueueFamilyIndex,
            buf->buffer, 0, VK_WHOLE_SIZE));
            
         CaptureResource(barrier.buffer);
    }

    for (UInt32 i = 0; i < imageMemoryBarrierCount; ++i)
    {
        const auto& barrier = pImageMemoryBarriers[i];
        
        // Resolve Image Handle
        auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
        auto* img = vkDevice->GetImagePool()->Get(barrier.image);
        if (!img) continue;

#ifdef RHI_VALIDATION
        if (barrier.oldLayout != IMAGE_LAYOUT_UNDEFINED && img->currentLayout != static_cast<VkImageLayout>(barrier.oldLayout))
        {
            LOG_WARNF("[RHIVkCommandBuffer::PipelineBarrier]: Layout mismatch for image! Tracked: {0}, Provided OldLayout: {1}", 
                (int)img->currentLayout, (int)barrier.oldLayout);
        }
#endif

        m_VkImageMemoryBarriers.emplace_back(ImageMemoryBarrier2(
            MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE ? barrier.srcStageMask : srcStage),
            MapAccessFlags2(barrier.srcAccess),
            MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE ? barrier.dstStageMask : dstStage),
            MapAccessFlags2(barrier.dstAccess),
            barrier.srcQueueFamilyIndex, barrier.dstQueueFamilyIndex,
            barrier.oldLayout, barrier.newLayout, img->image,
            barrier.subresourceRange));

        // Update tracked layout
        img->currentLayout = static_cast<VkImageLayout>(barrier.newLayout);

        CaptureResource(barrier.image);
    }
    
    VkDependencyInfoKHR dependencyInfo = DependencyInfo(
        static_cast<uint32_t>(m_VkMemoryBarriers.size()), m_VkMemoryBarriers.data(),
        static_cast<uint32_t>(m_VkBufferMemoryBarriers.size()), m_VkBufferMemoryBarriers.data(),
        static_cast<uint32_t>(m_VkImageMemoryBarriers.size()), m_VkImageMemoryBarriers.data(),
        static_cast<VkDependencyFlags>(dependency));

    // Use extension function
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (vkDevice->vkCmdPipelineBarrier2KHR)
    {
        vkDevice->vkCmdPipelineBarrier2KHR(m_VkCommandBuffer, &dependencyInfo);
    }
}

void RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount)
{
    PipelineBarrier(srcStage, dstStage, dependency, pMemoryBarriers, memoryBarrierCount, nullptr, 0, nullptr, 0);
}

void RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount)
{
    PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, pImageMemoryBarriers, imageMemoryBarrierCount, nullptr, 0);
}

void RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount)
{
    PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, nullptr, 0, pBufferMemoryBarriers, bufferMemoryBarrierCount);
}



void RHIVkCommandBuffer::Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding)
{
    if (m_VertexBuffers.size() > 0)
    {
        ::vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }
    ::vkCmdDraw(m_VkCommandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
}

void RHIVkCommandBuffer::DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding)
{
    if (m_VertexBuffers.size() > 0)
    {
        ::vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }
    
    if (m_IndexBuffer.has_value())
    {
        ::vkCmdBindIndexBuffer(m_VkCommandBuffer, m_IndexBuffer.value(), m_IndexOffset.value(), static_cast<
                                 VkIndexType>(m_IndexType.value()));
    }

    ::vkCmdDrawIndexed(m_VkCommandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
}

void RHIVkCommandBuffer::DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
{
    if (m_VertexBuffers.size() > 0)
    {
        ::vkCmdBindVertexBuffers(m_VkCommandBuffer, 0, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }

    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(buffer);
    if (!buf) return;

    ::vkCmdDrawIndirect(m_VkCommandBuffer, buf->buffer, offset, drawCount, stride);
    CaptureResource(buffer);
}

void RHIVkCommandBuffer::DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
{
    if (m_VertexBuffers.size() > 0)
    {
        ::vkCmdBindVertexBuffers(m_VkCommandBuffer, 0, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }

    if (m_IndexBuffer.has_value())
    {
        ::vkCmdBindIndexBuffer(m_VkCommandBuffer, m_IndexBuffer.value(), m_IndexOffset.value(), static_cast<VkIndexType>(m_IndexType.value()));
    }

    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(buffer);
    if (!buf) return;

    ::vkCmdDrawIndexedIndirect(m_VkCommandBuffer, buf->buffer, offset, drawCount, stride);
    CaptureResource(buffer);
}

void RHIVkCommandBuffer::Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
{
    ::vkCmdDispatch(m_VkCommandBuffer, groupCountX, groupCountY, groupCountZ);
}

void RHIVkCommandBuffer::DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (vkDevice->vkCmdDrawMeshTasksEXT)
    {
        vkDevice->vkCmdDrawMeshTasksEXT(m_VkCommandBuffer, groupCountX, groupCountY, groupCountZ);
    }
    else
    {
        LOG_ERROR("[RHIVkCommandBuffer::DrawMeshTasks]: vkCmdDrawMeshTasksEXT not found!");
    }
}

void RHIVkCommandBuffer::BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(buffer);
    if (!buf) return;

    m_VertexBuffers.emplace_back(buf->buffer);
    m_VertexBindingOffsets.emplace_back(offset);
    CaptureResource(buffer);
}

// Removed legacy WaitSemaphore/SignalSemaphore/Getters

void RHIVkCommandBuffer::CopyBuffer(RHIBufferHandle src, UInt64 srcOffset,
                                                       RHIBufferHandle dst, UInt64 dstOffset, UInt64 size)
{
    ASSERT(GetState() == ECommandBufferState::Recording);
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* srcBuf = vkDevice->GetBufferPool()->Get(src);
    auto* dstBuf = vkDevice->GetBufferPool()->Get(dst);

    if (!srcBuf || !dstBuf) return;

    VkBufferCopy copyRegion {};
    copyRegion.srcOffset = srcOffset;
    copyRegion.dstOffset = dstOffset;
    copyRegion.size = size;
    ::vkCmdCopyBuffer(m_VkCommandBuffer, srcBuf->buffer, dstBuf->buffer, 1, &copyRegion);

    CaptureResource(src);
    CaptureResource(dst);
}

void RHIVkCommandBuffer::BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type)
{ 
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(indexBuffer);
    if (!buf) return;

    m_IndexBuffer = buf->buffer;
    m_IndexOffset = offset;
    m_IndexType = type;
    CaptureResource(indexBuffer);
}


void RHIVkCommandBuffer::GenerateMipmaps(RHIImageHandle image) {
  if (!image.IsValid()) return;
  auto *vkDevice = static_cast<RHIVkDevice *>(GetDevice());
  auto *img = vkDevice->GetImagePool()->Get(image);
  if (!img) return;

  uint32_t mipLevels = img->mipLevels;
  uint32_t width = img->width;
  uint32_t height = img->height;

  for (uint32_t i = 1; i < mipLevels; i++) {
    // 1. Transition previous level (i-1) from TRANSFER_DST to TRANSFER_SRC
    {
      RHIImageMemoryBarrier barrier{};
      barrier.srcAccess = ACCESS_TRANSFER_WRITE_BIT;
      barrier.dstAccess = ACCESS_TRANSFER_READ_BIT;
      barrier.oldLayout = IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
      barrier.newLayout = IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
      barrier.image = image;
      barrier.subresourceRange = {IMAGE_ASPECT_COLOR_BIT, i - 1, 1, 0, 1};
      barrier.srcStageMask = PIPELINE_STAGE_TRANSFER_BIT;
      barrier.dstStageMask = PIPELINE_STAGE_TRANSFER_BIT;
      #ifdef RHI_VALIDATION
      img->currentLayout = static_cast<VkImageLayout>(barrier.oldLayout);
      #endif
      PipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_TRANSFER_BIT, 0, &barrier, 1);
    }

    // 2. Transition current level (i) from UNDEFINED to TRANSFER_DST
    {
        RHIImageMemoryBarrier barrier{};
        barrier.srcAccess = ACCESS_NONE;
        barrier.dstAccess = ACCESS_TRANSFER_WRITE_BIT;
        barrier.oldLayout = IMAGE_LAYOUT_UNDEFINED;
        barrier.newLayout = IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
        barrier.image = image;
        barrier.subresourceRange = {IMAGE_ASPECT_COLOR_BIT, i, 1, 0, 1};
        barrier.srcStageMask = PIPELINE_STAGE_TOP_OF_PIPE_BIT;
        barrier.dstStageMask = PIPELINE_STAGE_TRANSFER_BIT;
        PipelineBarrier(PIPELINE_STAGE_TOP_OF_PIPE_BIT, PIPELINE_STAGE_TRANSFER_BIT, 0, &barrier, 1);
    }

    // 3. Blit from previous level to current level
    VkImageBlit blit{};
    blit.srcOffsets[0] = {0, 0, 0};
    blit.srcOffsets[1] = {static_cast<int32_t>(width), static_cast<int32_t>(height), 1};
    blit.srcSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    blit.srcSubresource.mipLevel = i - 1;
    blit.srcSubresource.baseArrayLayer = 0;
    blit.srcSubresource.layerCount = 1;
    blit.dstOffsets[0] = {0, 0, 0};
    blit.dstOffsets[1] = {static_cast<int32_t>(width > 1 ? width / 2 : 1),
                          static_cast<int32_t>(height > 1 ? height / 2 : 1), 1};
    blit.dstSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    blit.dstSubresource.mipLevel = i;
    blit.dstSubresource.baseArrayLayer = 0;
    blit.dstSubresource.layerCount = 1;

    ::vkCmdBlitImage(m_VkCommandBuffer, img->image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                   img->image, VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &blit, VK_FILTER_LINEAR);

    // 4. Transition previous level (i-1) to SHADER_READ_ONLY_OPTIMAL
    {
      RHIImageMemoryBarrier barrier{};
      barrier.srcAccess = ACCESS_TRANSFER_READ_BIT;
      barrier.dstAccess = ACCESS_SHADER_READ_BIT;
      barrier.oldLayout = IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL;
      barrier.newLayout = IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
      barrier.image = image;
      barrier.subresourceRange = {IMAGE_ASPECT_COLOR_BIT, i - 1, 1, 0, 1};
      barrier.srcStageMask = PIPELINE_STAGE_TRANSFER_BIT;
      barrier.dstStageMask = PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
      #ifdef RHI_VALIDATION
      img->currentLayout = static_cast<VkImageLayout>(barrier.oldLayout);
      #endif
      PipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &barrier, 1);
    }

    if (width > 1) width /= 2;
    if (height > 1) height /= 2;
  }

  // 5. Final transition for the last mip level (mipLevels - 1) to SHADER_READ_ONLY_OPTIMAL
  {
      RHIImageMemoryBarrier barrier{};
      barrier.srcAccess = ACCESS_TRANSFER_WRITE_BIT;
      barrier.dstAccess = ACCESS_SHADER_READ_BIT;
      barrier.oldLayout = IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL;
      barrier.newLayout = IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL;
      barrier.image = image;
      barrier.subresourceRange = {IMAGE_ASPECT_COLOR_BIT, mipLevels - 1, 1, 0, 1};
      barrier.srcStageMask = PIPELINE_STAGE_TRANSFER_BIT;
      barrier.dstStageMask = PIPELINE_STAGE_FRAGMENT_SHADER_BIT;
      #ifdef RHI_VALIDATION
      img->currentLayout = static_cast<VkImageLayout>(barrier.oldLayout);
      #endif
      PipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &barrier, 1);
  }
  CaptureResource(image);
}

void RHIVkCommandBuffer::BeginDebugLabel(const char* label, const Float32 color[4])
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (vkDevice->vkCmdBeginDebugUtilsLabelEXT)
    {
        VkDebugUtilsLabelEXT labelInfo{};
        labelInfo.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT;
        labelInfo.pLabelName = label;
        if (color)
        {
            memcpy(labelInfo.color, color, sizeof(float) * 4);
        }
        vkDevice->vkCmdBeginDebugUtilsLabelEXT(m_VkCommandBuffer, &labelInfo);
    }
}

void RHIVkCommandBuffer::EndDebugLabel()
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (vkDevice->vkCmdEndDebugUtilsLabelEXT)
    {
        vkDevice->vkCmdEndDebugUtilsLabelEXT(m_VkCommandBuffer);
    }
}

void RHIVkCommandBuffer::InsertDebugMarker(const char* label, const Float32 color[4])
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (vkDevice->vkCmdInsertDebugUtilsLabelEXT)
    {
        VkDebugUtilsLabelEXT labelInfo{};
        labelInfo.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT;
        labelInfo.pLabelName = label;
        if (color)
        {
            memcpy(labelInfo.color, color, sizeof(float) * 4);
        }
        vkDevice->vkCmdInsertDebugUtilsLabelEXT(m_VkCommandBuffer, &labelInfo);
    }
}

VkFence RHIVkCommandBuffer::GetSubmissionFence() const
{
    // Fence ownership is separated from command buffer. Queue/device owns synchronization.
    return VK_NULL_HANDLE;
}

void RHIVkCommandBuffer::ResetInternal()
{
    if (GetState() == ECommandBufferState::Initial) return;

    // m_WaitSemaphores.clear();
    // m_SignalSemaphores.clear();
    // m_WaitStages.clear();
    m_VkBeginInfo = {};
    m_TrackedDescriptorPools.clear();
    
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* registry = vkDevice->GetResourceRegistry();
    if (registry)
    {
        for (auto h : m_TrackedResourceHandles)
        {
            registry->Release(h, RHIQueueType::Graphics, 0);
        }
    }
    m_TrackedResourceHandles.clear();

    m_VertexBuffers.clear();
    m_VertexBindingOffsets.clear();
    m_IndexBuffer.reset();
    m_IndexOffset.reset();
    m_VkMemoryBarriers.clear();
    m_VkBufferMemoryBarriers.clear();
    m_VkImageMemoryBarriers.clear();
    m_VkColorAttachments.clear();
    m_VkDescriptorSets.clear();
    m_VkBufferImageCopies.clear();

    m_CurrentPipeline = nullptr;
    
    ::vkResetCommandBuffer(m_VkCommandBuffer, 0);
    SetState(ECommandBufferState::Initial);
}

} // namespace ArisenEngine::RHI

void ArisenEngine::RHI::RHIVkCommandBuffer::BuildAccelerationStructures(UInt32 infoCount, const RHIAccelerationStructureBuildGeometryInfo* pInfos, const RHIAccelerationStructureBuildRangeInfo* const* ppBuildRangeInfos)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (!vkDevice->vkCmdBuildAccelerationStructuresKHR) return;

    Containers::Vector<VkAccelerationStructureBuildGeometryInfoKHR> vkInfos;
    vkInfos.reserve(infoCount);
    
    // We need to keep the geometry arrays alive during the call
    Containers::Vector<Containers::Vector<VkAccelerationStructureGeometryKHR>> vkGeometriesPerInfo;
    vkGeometriesPerInfo.reserve(infoCount);

    for (UInt32 i = 0; i < infoCount; ++i)
    {
        const auto& rhiInfo = pInfos[i];
        
        VkAccelerationStructureBuildGeometryInfoKHR vkInfo{};
        vkInfo.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR;
        vkInfo.type = (VkAccelerationStructureTypeKHR)rhiInfo.type;
        vkInfo.flags = (VkBuildAccelerationStructureFlagsKHR)rhiInfo.flags;
        vkInfo.mode = VK_BUILD_ACCELERATION_STRUCTURE_MODE_BUILD_KHR;
        
        auto* dstAS = vkDevice->m_AccelerationStructurePool->Get(rhiInfo.dstAccelerationStructure);
        if (dstAS) vkInfo.dstAccelerationStructure = dstAS->accelerationStructure;

        auto* srcAS = vkDevice->m_AccelerationStructurePool->Get(rhiInfo.srcAccelerationStructure);
        if (srcAS) vkInfo.srcAccelerationStructure = srcAS->accelerationStructure;

        auto* scratchBuf = vkDevice->m_BufferPool->Get(rhiInfo.scratchData);
        if (scratchBuf) vkInfo.scratchData.deviceAddress = vkDevice->m_MemoryAllocator->GetDeviceAddress(scratchBuf->buffer);

        vkInfo.geometryCount = rhiInfo.geometryCount;
        
        Containers::Vector<VkAccelerationStructureGeometryKHR> vkGeometries;
        vkGeometries.reserve(rhiInfo.geometryCount);
        for (UInt32 j = 0; j < rhiInfo.geometryCount; ++j)
        {
            const auto& rhiGeom = rhiInfo.pGeometries[j];
            VkAccelerationStructureGeometryKHR vkGeom{};
            vkGeom.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR;
            vkGeom.geometryType = (VkGeometryTypeKHR)rhiGeom.type;
            vkGeom.flags = (VkGeometryFlagsKHR)rhiGeom.flags;
            
            if (rhiGeom.type == ERHIAccelerationStructureGeometryType::Triangles)
            {
                vkGeom.geometry.triangles.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR;
                vkGeom.geometry.triangles.vertexFormat = (VkFormat)rhiGeom.triangles.vertexFormat;
                vkGeom.geometry.triangles.vertexData.deviceAddress = rhiGeom.triangles.vertexData;
                vkGeom.geometry.triangles.vertexStride = rhiGeom.triangles.vertexStride;
                vkGeom.geometry.triangles.maxVertex = rhiGeom.triangles.maxVertex;
                vkGeom.geometry.triangles.indexType = (VkIndexType)rhiGeom.triangles.indexType;
                vkGeom.geometry.triangles.indexData.deviceAddress = rhiGeom.triangles.indexData;
                vkGeom.geometry.triangles.transformData.deviceAddress = rhiGeom.triangles.transformData;
            }
            else if (rhiGeom.type == ERHIAccelerationStructureGeometryType::AABBs)
            {
                vkGeom.geometry.aabbs.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_AABBS_DATA_KHR;
                vkGeom.geometry.aabbs.data.deviceAddress = rhiGeom.aabbs.data;
                vkGeom.geometry.aabbs.stride = rhiGeom.aabbs.stride;
            }
            else if (rhiGeom.type == ERHIAccelerationStructureGeometryType::Instances)
            {
                vkGeom.geometry.instances.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR;
                vkGeom.geometry.instances.arrayOfPointers = rhiGeom.instances.arrayOfPointers ? VK_TRUE : VK_FALSE;
                vkGeom.geometry.instances.data.deviceAddress = rhiGeom.instances.data;
            }
            
            vkGeometries.emplace_back(vkGeom);
        }
        
        vkGeometriesPerInfo.emplace_back(std::move(vkGeometries));
        vkInfo.pGeometries = vkGeometriesPerInfo.back().data();
        vkInfos.emplace_back(vkInfo);

        CaptureResource(rhiInfo.scratchData);
        // Track the AS handles? We don't have a CaptureResource for AS yet, but we should.
        if (dstAS) m_TrackedResourceHandles.push_back(dstAS->registryHandle);
        if (srcAS) m_TrackedResourceHandles.push_back(srcAS->registryHandle);
    }

    Containers::Vector<const VkAccelerationStructureBuildRangeInfoKHR*> vkRangeInfoPtrs;
    vkRangeInfoPtrs.reserve(infoCount);
    for (UInt32 i = 0; i < infoCount; ++i)
    {
        vkRangeInfoPtrs.push_back(reinterpret_cast<const VkAccelerationStructureBuildRangeInfoKHR*>(ppBuildRangeInfos[i]));
    }

    vkDevice->vkCmdBuildAccelerationStructuresKHR(m_VkCommandBuffer, infoCount, vkInfos.data(), vkRangeInfoPtrs.data());
}

void ArisenEngine::RHI::RHIVkCommandBuffer::TraceRays(const RHITraceRaysDescriptor& desc)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    if (!vkDevice->vkCmdTraceRaysKHR) return;

    VkStridedDeviceAddressRegionKHR raygenRegion{};
    raygenRegion.deviceAddress = desc.raygenShaderRecord.deviceAddress;
    raygenRegion.stride = desc.raygenShaderRecord.stride;
    raygenRegion.size = desc.raygenShaderRecord.size;

    VkStridedDeviceAddressRegionKHR missRegion{};
    missRegion.deviceAddress = desc.missShaderTable.deviceAddress;
    missRegion.stride = desc.missShaderTable.stride;
    missRegion.size = desc.missShaderTable.size;

    VkStridedDeviceAddressRegionKHR hitRegion{};
    hitRegion.deviceAddress = desc.hitShaderTable.deviceAddress;
    hitRegion.stride = desc.hitShaderTable.stride;
    hitRegion.size = desc.hitShaderTable.size;

    VkStridedDeviceAddressRegionKHR callableRegion{};
    callableRegion.deviceAddress = desc.callableShaderTable.deviceAddress;
    callableRegion.stride = desc.callableShaderTable.stride;
    callableRegion.size = desc.callableShaderTable.size;

    vkDevice->vkCmdTraceRaysKHR(m_VkCommandBuffer, &raygenRegion, &missRegion, &hitRegion, &callableRegion, desc.width, desc.height, desc.depth);
}
