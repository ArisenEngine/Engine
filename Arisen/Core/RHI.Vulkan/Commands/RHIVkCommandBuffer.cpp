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


ArisenEngine::RHI::RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
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

ArisenEngine::RHI::RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool)
: RHICommandBuffer(device, pool),
m_OwnerThreadId(std::this_thread::get_id())
{
    m_VkDevice = static_cast<VkDevice>(device->GetHandle());
    m_VkCommandPool = pool->AcquireThreadCommandPool();

    // Alloc Memory
    {
        VkCommandBufferAllocateInfo allocInfo{};
        allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
        allocInfo.commandPool = m_VkCommandPool;
        // todo 
        allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
        allocInfo.commandBufferCount = 1;

        // todo: separate alloc memory and free memory
        if (vkAllocateCommandBuffers(m_VkDevice, &allocInfo, &m_VkCommandBuffer) != VK_SUCCESS)
        {
            LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::RHIVkCommandBuffer]: failed to allocate command buffers!");
        }
    }
    
    SetState(ECommandBufferState::Initial);
}


void ArisenEngine::RHI::RHIVkCommandBuffer::BeginRenderPass(UInt32 frameIndex, RenderPassBeginDesc&& desc)
{
    ASSERT(GetState() == ECommandBufferState::Recording);
    
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
    
    renderPassArea:
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
        renderPassInfo.renderArea.extent = {1280, 720}; // Adjusted fallback to default test resolution
    }
    
    renderPassInfo.clearValueCount = desc.clearValueCount;
    renderPassInfo.pClearValues = reinterpret_cast<const VkClearValue*>(desc.pClearValues);

    vkCmdBeginRenderPass(m_VkCommandBuffer, &renderPassInfo, static_cast<VkSubpassContents>(desc.subpassContents));

    SetState(ECommandBufferState::RecordingPass);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::EndRenderPass()
{
    ASSERT(GetState() == ECommandBufferState::RecordingPass);
    vkCmdEndRenderPass(m_VkCommandBuffer);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BeginRendering(const RHIRenderingInfo& info)
{
    ASSERT(GetState() == ECommandBufferState::Recording);

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

    VkRenderingInfoKHR renderingInfo{};
    renderingInfo.sType = VK_STRUCTURE_TYPE_RENDERING_INFO_KHR;
    renderingInfo.renderArea.offset = { info.RHIRenderArea.x, info.RHIRenderArea.y };
    renderingInfo.renderArea.extent = { info.RHIRenderArea.width, info.RHIRenderArea.height };
    renderingInfo.layerCount = info.layerCount;

    renderingInfo.colorAttachmentCount = static_cast<uint32_t>(m_VkColorAttachments.size());
    renderingInfo.pColorAttachments = m_VkColorAttachments.data();
    
    if (info.pDepthAttachment != nullptr)
    {
        renderingInfo.pDepthAttachment = &m_VkDepthAttachment;
    }

    if (info.pStencilAttachment != nullptr)
    {
        renderingInfo.pStencilAttachment = &m_VkStencilAttachment;
    }

    if (vkDevice->vkCmdBeginRenderingKHR)
    {
        vkDevice->vkCmdBeginRenderingKHR(m_VkCommandBuffer, &renderingInfo);
    }
    else
    {
        LOG_ERROR("[RHIVkCommandBuffer::BeginRendering]: vkCmdBeginRenderingKHR not found!");
    }

    SetState(ECommandBufferState::RecordingPass);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::EndRendering()
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
}


void ArisenEngine::RHI::RHIVkCommandBuffer::TrackDescriptorPoolUse(RHIDescriptorPool* pool, UInt32 poolId)
{
    if (pool == nullptr) return;
    // avoid duplicates
    for (const auto& t : m_TrackedDescriptorPools)
    {
        if (t.pool == pool && t.poolId == poolId) return;
    }
    m_TrackedDescriptorPools.emplace_back(TrackedPoolUse{ pool, poolId });
}

void ArisenEngine::RHI::RHIVkCommandBuffer::CaptureResource(RHIBufferHandle buffer)
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

void ArisenEngine::RHI::RHIVkCommandBuffer::CaptureResource(RHIImageHandle image)
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

void ArisenEngine::RHI::RHIVkCommandBuffer::Begin(UInt32 frameIndex)
{
    Begin(frameIndex, 0);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Begin(UInt32 frameIndex, UInt32 commandBufferUsage)
{
    ASSERT(GetState() == ECommandBufferState::Initial);

    m_VkBeginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
    m_VkBeginInfo.flags = commandBufferUsage;

    if (vkBeginCommandBuffer(m_VkCommandBuffer, &m_VkBeginInfo) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("failed to begin recording command buffer!");
    }

    SetState(ECommandBufferState::Recording);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::End()
{
    ASSERT(m_WaitSemaphores.size() == m_WaitStages.size());
    
    if (vkEndCommandBuffer(m_VkCommandBuffer) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::End]: failed to record command buffer!");
    }
    
    SetState(ECommandBufferState::Executable);
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

void ArisenEngine::RHI::RHIVkCommandBuffer::BindPipeline(UInt32 frameIndex, RHIPipelineHandle pipelineHandle)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* p = vkDevice->GetPipelinePool()->Get(pipelineHandle);
    if (!p || !p->pipeline) return;

    RHIPipeline* pipeline = p->pipeline;
    m_CurrentPipeline = pipeline;
    vkCmdBindPipeline(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
        static_cast<VkPipeline>(pipeline->GetGraphicsPipeline(frameIndex)));

    // Bind Global Bindless Descriptor Set (Set 3)
    auto* bindlessManager = vkDevice->GetBindlessManager();
    if (bindlessManager)
    {
        VkDescriptorSet bindlessSet = bindlessManager->GetDescriptorSet();
        auto* vkPipeline = static_cast<RHIVkGPUPipeline*>(pipeline);
        vkCmdBindDescriptorSets(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
            vkPipeline->GetPipelineLayout(frameIndex),
            3, 1, &bindlessSet, 0, nullptr);
    }
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BindDescriptorSets(UInt32 frameIndex, EPipelineBindPoint bindPoint,
    UInt32 firstSet, Containers::Vector<std::shared_ptr<RHIDescriptorSet>>& descriptorsets, UInt32 dynamicOffsetCount, const UInt32* pDynamicOffsets)
{
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
    vkCmdBindDescriptorSets(m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(bindPoint),
        pipeline->GetPipelineLayout(frameIndex),
        firstSet, static_cast<uint32_t>(m_VkDescriptorSets.size()),
        m_VkDescriptorSets.data(),
        dynamicOffsetCount, pDynamicOffsets);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst,
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
    
    vkCmdCopyBufferToImage(m_VkCommandBuffer,
        srcBuf->buffer, dstImg->image,
        static_cast<VkImageLayout>(dstImageLayout), static_cast<uint32_t>(m_VkBufferImageCopies.size()), m_VkBufferImageCopies.data()
        );
    
    CaptureResource(srcBuffer);
    CaptureResource(dst);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::PipelineBarrier(
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

        m_VkImageMemoryBarriers.emplace_back(ImageMemoryBarrier2(
            MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE ? barrier.srcStageMask : srcStage),
            MapAccessFlags2(barrier.srcAccess),
            MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE ? barrier.dstStageMask : dstStage),
            MapAccessFlags2(barrier.dstAccess),
            barrier.srcQueueFamilyIndex, barrier.dstQueueFamilyIndex,
            barrier.oldLayout, barrier.newLayout, img->image,
            barrier.subresourceRange));

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

void ArisenEngine::RHI::RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount)
{
    PipelineBarrier(srcStage, dstStage, dependency, pMemoryBarriers, memoryBarrierCount, nullptr, 0, nullptr, 0);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount)
{
    PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, pImageMemoryBarriers, imageMemoryBarrierCount, nullptr, 0);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::PipelineBarrier(
    EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
    const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount)
{
    PipelineBarrier(srcStage, dstStage, dependency, nullptr, 0, nullptr, 0, pBufferMemoryBarriers, bufferMemoryBarrierCount);
}



void ArisenEngine::RHI::RHIVkCommandBuffer::Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance, UInt32 firstBinding)
{
    if (m_VertexBuffers.size() > 0)
    {
        vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }
    vkCmdDraw(m_VkCommandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset, UInt32 firstInstance,  UInt32 firstBinding)
{
    if (m_VertexBuffers.size() > 0)
    {
        vkCmdBindVertexBuffers(m_VkCommandBuffer, firstBinding, m_VertexBuffers.size(), m_VertexBuffers.data(), m_VertexBindingOffsets.data());
    }
    
    if (m_IndexBuffer.has_value())
    {
        vkCmdBindIndexBuffer(m_VkCommandBuffer, m_IndexBuffer.value(), m_IndexOffset.value(), static_cast<
                                 VkIndexType>(m_IndexType.value()));
    }

    vkCmdDrawIndexed(m_VkCommandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
{
    vkCmdDispatch(m_VkCommandBuffer, groupCountX, groupCountY, groupCountZ);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
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

void ArisenEngine::RHI::RHIVkCommandBuffer::BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(buffer);
    if (!buf) return;

    m_VertexBuffers.emplace_back(buf->buffer);
    m_VertexBindingOffsets.emplace_back(offset);
    CaptureResource(buffer);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::WaitSemaphore(RHISemaphoreHandle semaphore, EPipelineStageFlag stage)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* sem = vkDevice->GetSemaphorePool()->Get(semaphore);
    if (!sem) return;

    m_WaitSemaphores.emplace_back(sem->semaphore);
    m_WaitStages.emplace_back(static_cast<VkPipelineStageFlags>(stage));
}

const VkSemaphore* ArisenEngine::RHI::RHIVkCommandBuffer::GetWaitSemaphores() const
{
    return m_WaitSemaphores.data();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkCommandBuffer::GetWaitSemaphoresCount() const
{
    return m_WaitSemaphores.size();
}

void ArisenEngine::RHI::RHIVkCommandBuffer::SignalSemaphore(RHISemaphoreHandle semaphore)
{
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* sem = vkDevice->GetSemaphorePool()->Get(semaphore);
    if (!sem) return;

    m_SignalSemaphores.emplace_back(sem->semaphore);
}

const VkSemaphore* ArisenEngine::RHI::RHIVkCommandBuffer::GetSignalSemaphores() const
{
    return m_SignalSemaphores.data();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkCommandBuffer::GetSignalSemaphoresCount() const
{
    return m_SignalSemaphores.size();
}

const VkPipelineStageFlags* ArisenEngine::RHI::RHIVkCommandBuffer::GetWaitStageMask() const
{
    return m_WaitStages.data();
}

void ArisenEngine::RHI::RHIVkCommandBuffer::CopyBuffer(RHIBufferHandle src, UInt64 srcOffset,
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
    vkCmdCopyBuffer(m_VkCommandBuffer, srcBuf->buffer, dstBuf->buffer, 1, &copyRegion);

    CaptureResource(src);
    CaptureResource(dst);
}

void ArisenEngine::RHI::RHIVkCommandBuffer::BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type)
{ 
    auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
    auto* buf = vkDevice->GetBufferPool()->Get(indexBuffer);
    if (!buf) return;

    m_IndexBuffer = buf->buffer;
    m_IndexOffset = offset;
    m_IndexType = type;
    CaptureResource(indexBuffer);
}


void ArisenEngine::RHI::RHIVkCommandBuffer::GenerateMipmaps(RHIImageHandle image) {
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

    vkCmdBlitImage(m_VkCommandBuffer, img->image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
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
      PipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, &barrier, 1);
  }
  CaptureResource(image);
}

VkFence ArisenEngine::RHI::RHIVkCommandBuffer::GetSubmissionFence() const
{
    // Fence ownership is separated from command buffer. Queue/device owns synchronization.
    return VK_NULL_HANDLE;
}

void ArisenEngine::RHI::RHIVkCommandBuffer::ResetInternal()
{
    if (GetState() == ECommandBufferState::Initial) return;

    m_WaitSemaphores.clear();
    m_SignalSemaphores.clear();
    m_WaitStages.clear();
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
    
    vkResetCommandBuffer(m_VkCommandBuffer, 0);
    SetState(ECommandBufferState::Initial);
}






