#include "RHIVkCommandBuffer.h"
#include "RHI/Enums/Buffer/EBufferUsage.h"

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
#include "Descriptors/RHIVkBindlessDescriptorTable.h"
#include "Descriptors/RHIVkDescriptorHeap.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Commands/RHICommandDefs.h"
#include "Presentation/RHIVkFrameBuffer.h"
#include "RenderPass/RHIVkGPURenderPass.h"
#include "RHI/Commands/IRHICommandExecutor.h"
#include "Profiler.h"

namespace ArisenEngine::RHI
{
    using UInt32 = uint32_t;
    using Int32 = int32_t;
    using UInt64 = uint64_t;
    using Float32 = float;

    struct RHIVkExecutor : public IRHICommandExecutor
    {
        RHIVkCommandBuffer* cmd;

        RHIVkExecutor(RHIVkCommandBuffer* c) : cmd(c)
        {
        }

        // Methods will be defined below
        void BeginRenderPass(RenderPassBeginDesc&& desc) override;
        void EndRenderPass() override;
        void BeginRendering(const RHIRenderingInfo& info) override;
        void EndRendering() override;
        void Begin(UInt32 frameIndex, UInt32 commandBufferUsage,
                   const RHICommandBufferInheritanceInfo* pInheritanceInfo) override;
        void End() override;

        void BindPipeline(RHIPipelineHandle pipeline) override;
        void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance,
                  UInt32 firstBinding) override;
        void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset,
                         UInt32 firstInstance, UInt32 firstBinding) override;
        void DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) override;
        void DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) override;
        void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) override;
        void DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) override;
        void BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset) override;
        void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type) override;
        void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset, RHIBufferHandle dst, UInt64 dstOffset,
                        UInt64 size) override;
        void BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet, RHIDescriptorPoolHandle poolHandle,
                                UInt32 poolId, UInt32 setIndex, bool isSingleSet) override;
        void BindDescriptorBuffers(UInt32 bufferCount, const RHIBufferHandle* pBuffers) override;
        void SetDescriptorBufferOffsets(EPipelineBindPoint bindPoint, RHIPipelineHandle pipeline,
                                        UInt32 firstSet, UInt32 setCount, const UInt32* pIndices,
                                        const UInt64* pOffsets) override;
        void PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags) override;
        void CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst, EImageLayout dstImageLayout,
                               UInt32 regionCount, const RHIBufferImageCopy* pRegions) override;
        void PipelineBarrier(const RHICmdPipelineBarrier& cmd, const RHIMemoryBarrier* pMem,
                             const RHIImageMemoryBarrier* pImg, const RHIBufferMemoryBarrier* pBuf) override;
        void TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout) override;
        void CopyImage(RHIImageHandle src, EImageLayout srcLayout, RHIImageHandle dst, EImageLayout dstLayout,
                       UInt32 regionCount, const RHIImageCopy* pRegions) override;
        void GenerateMipmaps(RHIImageHandle image) override;
        void BuildAccelerationStructures(UInt32 infoCount, const RHIAccelerationStructureBuildGeometryInfo* pInfos,
                                         const RHIAccelerationStructureBuildRangeInfo* const*
                                         ppBuildRangeInfos) override;
        void TraceRays(const RHITraceRaysDescriptor& desc) override;
        void SetFragmentShadingRate(EShadingRate rate, EShadingRateCombiner combinerOp[2]) override;
        void BeginDebugLabel(const char* label, const Float32 color[4]) override;
        void EndDebugLabel() override;
        void InsertDebugMarker(const char* label, const Float32 color[4]) override;
        void TrackDescriptorPoolUse(RHIDescriptorPoolHandle poolHandle, UInt32 poolId) override;

        // Dynamic State
        void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth,
                         Float32 maxDepth) override;
        void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height) override;
        void SetLineWidth(Float32 lineWidth) override;
        void SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp,
                          Float32 depthBiasSlopeFactor) override;
        void SetBlendConstants(const Float32 blendConstants[4]) override;
        void SetStencilReference(UInt32 faceMask, UInt32 reference) override;
        void SetCullMode(ECullModeFlagBits cullMode) override;
        void SetFrontFace(EFrontFace frontFace) override;
        void SetPrimitiveTopology(EPrimitiveTopology topology) override;
        void SetDepthTestEnable(bool enable) override;
        void SetDepthWriteEnable(bool enable) override;
        void SetDepthCompareOp(ECompareOp depthCompareOp) override;
        void SetStencilTestEnable(bool enable) override;
        void SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp,
                          ECompareOp compareOp) override;

        // Internal helper
        void DoPipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
                               const RHIMemoryBarrier* pMem, UInt32 memCount,
                               const RHIImageMemoryBarrier* pImg, UInt32 imgCount,
                               const RHIBufferMemoryBarrier* pBuf, UInt32 bufCount);
    };


    RHIVkCommandBuffer::~RHIVkCommandBuffer() noexcept
    {
        // Ensure resources are released if the buffer is destroyed before submission
        auto* vkDevice = GetVkDevice();
        auto* registry = vkDevice->GetResourceRegistry();
        if (registry)
        {
            for (auto h : m_TrackedResourceHandles)
            {
                registry->Release(h);
            }
        }
    }

    RHIVkDevice* RHIVkCommandBuffer::GetVkDevice() const
    {
        return static_cast<RHIVkDevice*>(GetDevice());
    }

    RHIVkCommandBuffer::RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool, ECommandBufferLevel level)
        : RHICommandBuffer(device, pool, level),
          m_OwnerThreadId(std::this_thread::get_id()),
          m_OwnerThreadIndex(ThreadRegistry::GetThreadIndex())
    {
        static_assert(sizeof(RHIVkCommandBuffer) > 0, "RHIVkCommandBuffer is incomplete!");
        this->m_VkDevice = static_cast<VkDevice>(device->GetHandle());
        this->m_VkCommandPool = pool->GetCurrentThreadSlot().commandPool;

        // Alloc Memory
        {
            VkCommandBufferAllocateInfo allocInfo{};
            allocInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO;
            allocInfo.commandPool = this->m_VkCommandPool;
            allocInfo.level = (level == COMMAND_BUFFER_LEVEL_PRIMARY)
                                  ? VK_COMMAND_BUFFER_LEVEL_PRIMARY
                                  : VK_COMMAND_BUFFER_LEVEL_SECONDARY;
            allocInfo.commandBufferCount = 1;

            // todo: separate alloc memory and free memory
            if (::vkAllocateCommandBuffers(this->m_VkDevice, &allocInfo, &this->m_VkCommandBuffer) != VK_SUCCESS)
            {
                LOG_FATAL_AND_THROW("[RHIVkCommandBuffer::RHIVkCommandBuffer]: failed to allocate command buffers!");
            }
        }

        SetState(ECommandBufferState::Initial);
    }


    void RHIVkExecutor::BeginRenderPass(RenderPassBeginDesc&& desc)
    {
        ARISEN_PROFILE_ZONE("Vk::BeginRenderPass");
        UInt32 frameIndex = cmd->GetCurrentFrameIndex();

        auto* vkDevice = cmd->GetVkDevice();
        auto* rp = vkDevice->GetRenderPassPool()->Get(desc.renderPass);
        auto* fb = vkDevice->GetFrameBufferPool()->Get(desc.frameBuffer);

        if (!rp || !fb)
        {
            LOG_ERROR("[RHIVkCommandBuffer::BeginRenderPass]: invalid renderPass or RHIFrameBuffer handle!");
            return;
        }

        VkRenderPassBeginInfo renderPassInfo{};
        renderPassInfo.sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO;

        // Retrieve backend objects for the specific frame
        auto* rpObj = static_cast<RHIVkGPURenderPass*>(rp->renderPassObj);
        if (rpObj)
        {
            renderPassInfo.renderPass = static_cast<VkRenderPass>(rpObj->GetHandle(frameIndex));
        }
        else
        {
            renderPassInfo.renderPass = rp->renderPass;
        }

        if (fb->frameBufferObj)
        {
            auto* fbObj = static_cast<RHIVkFrameBuffer*>(fb->frameBufferObj);
            renderPassInfo.framebuffer = static_cast<VkFramebuffer>(fbObj->GetHandle(frameIndex));
        }
        else
        {
            renderPassInfo.framebuffer = fb->framebuffer;
        }

        renderPassInfo.renderArea.offset = {0, 0};

        // Use actual RHIFrameBuffer dimensions if available
        renderPassInfo.renderArea.extent = {(UInt32)fb->width, (UInt32)fb->height};
        if (fb->frameBufferObj)
        {
            auto* fbObj = static_cast<RHIVkFrameBuffer*>(fb->frameBufferObj);
            renderPassInfo.renderArea.extent.width = fbObj->GetRenderArea().width;
            renderPassInfo.renderArea.extent.height = fbObj->GetRenderArea().height;
        }

        // Fallback if dimensions are unknown
        if (renderPassInfo.renderArea.extent.width == 0 || renderPassInfo.renderArea.extent.height == 0)
        {
            renderPassInfo.renderArea.extent.width = 1280;
            renderPassInfo.renderArea.extent.height = 720;
        }

        renderPassInfo.clearValueCount = desc.clearValueCount;
        renderPassInfo.pClearValues = reinterpret_cast<const VkClearValue*>(desc.pClearValues);

        ::vkCmdBeginRenderPass(cmd->m_VkCommandBuffer, &renderPassInfo,
                               static_cast<VkSubpassContents>(desc.subpassContents));
    }

    void RHIVkExecutor::EndRenderPass()
    {
        ::vkCmdEndRenderPass(cmd->m_VkCommandBuffer);
    }

    void RHIVkExecutor::BeginRendering(const RHIRenderingInfo& info)
    {
        ARISEN_PROFILE_ZONE("Vk::BeginRendering");
        VkRenderingInfoKHR vkInfo = {};
        vkInfo.sType = VK_STRUCTURE_TYPE_RENDERING_INFO_KHR;
        vkInfo.renderArea.offset = {info.RHIRenderArea.x, info.RHIRenderArea.y};
        vkInfo.renderArea.extent = {info.RHIRenderArea.width, info.RHIRenderArea.height};
        vkInfo.layerCount = info.layerCount;
        vkInfo.colorAttachmentCount = info.colorAttachmentCount;

        cmd->m_VkColorAttachments.clear();
        cmd->m_VkColorAttachments.reserve(info.colorAttachmentCount);

        auto* vkDevice = cmd->GetVkDevice();

        for (UInt32 i = 0; i < info.colorAttachmentCount; ++i)
        {
            const auto& att = info.pColorAttachments[i];

            VkRenderingAttachmentInfoKHR vkAtt{};
            vkAtt.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO_KHR;
            vkAtt.imageLayout = static_cast<VkImageLayout>(att.imageLayout);
            vkAtt.loadOp = static_cast<VkAttachmentLoadOp>(att.loadOp);
            vkAtt.storeOp = static_cast<VkAttachmentStoreOp>(att.storeOp);

            auto* view = vkDevice->GetImageViewPool()->Get(att.imageView);
            if (view)
            {
                vkAtt.imageView = view->view;
            }
            else
            {
                // If view is invalid, we must still push an entry to keep indices aligned.
                // VK_NULL_HANDLE for imageView means the attachment is ignored.
                vkAtt.imageView = VK_NULL_HANDLE;
            }

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

            cmd->m_VkColorAttachments.emplace_back(vkAtt);
        }

        VkRenderingAttachmentInfoKHR depthAttachment{};
        if (info.pDepthAttachment != nullptr)
        {
            const auto& att = *info.pDepthAttachment;
            auto* view = vkDevice->GetImageViewPool()->Get(att.imageView);
            if (view)
            {
                depthAttachment.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO_KHR;
                depthAttachment.imageView = view->view;
                depthAttachment.imageLayout = static_cast<VkImageLayout>(att.imageLayout);
                depthAttachment.loadOp = static_cast<VkAttachmentLoadOp>(att.loadOp);
                depthAttachment.storeOp = static_cast<VkAttachmentStoreOp>(att.storeOp);
                std::memcpy(&depthAttachment.clearValue, &att.clearValue, sizeof(VkClearValue));
                vkInfo.pDepthAttachment = &depthAttachment;
            }
        }

        VkRenderingAttachmentInfoKHR stencilAttachment{};
        if (info.pStencilAttachment != nullptr)
        {
            const auto& att = *info.pStencilAttachment;
            auto* view = vkDevice->GetImageViewPool()->Get(att.imageView);
            if (view)
            {
                stencilAttachment.sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO_KHR;
                stencilAttachment.imageView = view->view;
                stencilAttachment.imageLayout = static_cast<VkImageLayout>(att.imageLayout);
                stencilAttachment.loadOp = static_cast<VkAttachmentLoadOp>(att.loadOp);
                stencilAttachment.storeOp = static_cast<VkAttachmentStoreOp>(att.storeOp);
                std::memcpy(&stencilAttachment.clearValue, &att.clearValue, sizeof(VkClearValue));
                vkInfo.pStencilAttachment = &stencilAttachment;
            }
        }

        vkInfo.pColorAttachments = cmd->m_VkColorAttachments.data();

        if (vkDevice->vkCmdBeginRenderingKHR)
        {
            vkDevice->vkCmdBeginRenderingKHR(cmd->m_VkCommandBuffer, &vkInfo);
        }
        else
        {
            LOG_ERROR("[RHIVkCommandBuffer::BeginRendering]: vkCmdBeginRenderingKHR not found!");
        }
    }

    void RHIVkExecutor::EndRendering()
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdEndRenderingKHR)
        {
            vkDevice->vkCmdEndRenderingKHR(cmd->m_VkCommandBuffer);
        }
        else
        {
            LOG_ERROR("[RHIVkCommandBuffer::EndRendering]: vkCmdEndRenderingKHR not found!");
        }
    }

    void RHIVkExecutor::Begin(UInt32 frameIndex, UInt32 commandBufferUsage,
                              const RHICommandBufferInheritanceInfo* pInheritanceInfo)
    {
        ARISEN_PROFILE_ZONE("Vk::Begin");
        cmd->SetCurrentFrameIndex(frameIndex);

        VkCommandBufferBeginInfo beginInfo{};
        beginInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO;
        beginInfo.flags = commandBufferUsage;

        VkCommandBufferInheritanceInfo inheritanceInfo{};
        if (pInheritanceInfo)
        {
            inheritanceInfo.sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_INHERITANCE_INFO;
            auto* vkDevice = cmd->GetVkDevice();
            auto* rp = vkDevice->GetRenderPassPool()->Get(pInheritanceInfo->renderPass);
            auto* fb = vkDevice->GetFrameBufferPool()->Get(pInheritanceInfo->frameBuffer);

            if (rp) inheritanceInfo.renderPass = rp->renderPass;
            if (fb) inheritanceInfo.framebuffer = fb->framebuffer;
            inheritanceInfo.subpass = pInheritanceInfo->subpass;
            inheritanceInfo.occlusionQueryEnable = pInheritanceInfo->occlusionQueryEnable ? VK_TRUE : VK_FALSE;
            inheritanceInfo.queryFlags = pInheritanceInfo->occlusionQueryFlags;
            inheritanceInfo.pipelineStatistics = pInheritanceInfo->pipelineStatistics;

            beginInfo.pInheritanceInfo = &inheritanceInfo;
        }

        if (::vkBeginCommandBuffer(cmd->m_VkCommandBuffer, &beginInfo) != VK_SUCCESS)
        {
            LOG_FATAL("[RHIVkCommandBuffer::Begin]: failed to begin recording command buffer!");
        }
    }

    void RHIVkExecutor::End()
    {
        ARISEN_PROFILE_ZONE("Vk::End");
        if (::vkEndCommandBuffer(cmd->m_VkCommandBuffer) != VK_SUCCESS)
        {
            LOG_FATAL("[RHIVkCommandBuffer::End]: failed to record command buffer!");
        }
    }


    void RHIVkCommandBuffer::CaptureResource(RHIBufferHandle buffer)
    {
        if (!buffer.IsValid()) return;
        auto* vkDevice = GetVkDevice();
        auto* buf = vkDevice->GetBufferPool()->Get(buffer);
        if (!buf) return;

        auto handle = buf->registryHandle;

        // Performance: skip redundant tracking
        for (const auto& h : this->m_TrackedResourceHandles)
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
        auto* vkDevice = GetVkDevice();
        auto* img = vkDevice->GetImagePool()->Get(image);
        if (!img) return;

        auto handle = img->registryHandle;

        for (const auto& h : this->m_TrackedResourceHandles)
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

    void RHIVkCommandBuffer::CaptureResource(RHIAccelerationStructureHandle handle)
    {
        if (!handle.IsValid()) return;
        auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
        auto* as = vkDevice->m_AccelerationStructurePool->Get(handle);
        if (!as) return;

        auto regHandle = as->registryHandle;

        for (const auto& h : this->m_TrackedResourceHandles)
        {
            if (h.index == regHandle.index && h.generation == regHandle.generation) return;
        }

        if (regHandle.IsValid())
        {
            m_TrackedResourceHandles.emplace_back(regHandle);
            vkDevice->GetResourceRegistry()->Retain(regHandle);
        }
    }


    void RHIVkExecutor::BindPipeline(RHIPipelineHandle pipelineHandle)
    {
        ARISEN_PROFILE_ZONE("Vk::BindPipeline");
        UInt32 frameIndex = cmd->GetCurrentFrameIndex();
        auto* vkDevice = cmd->GetVkDevice();
        auto* p = vkDevice->GetPipelinePool()->Get(pipelineHandle);
        if (!p || !p->pipeline) return;

        RHIPipeline* pipeline = p->pipeline;
        cmd->m_CurrentPipeline = pipeline;

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

        ::vkCmdBindPipeline(cmd->m_VkCommandBuffer, static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
                            static_cast<VkPipeline>(vkPipeline->GetVkPipeline(frameIndex)));

        // Bind Global Bindless Descriptor Set (Set 3)
        VkDescriptorSet bindlessSet = VK_NULL_HANDLE;
        auto* pso = static_cast<RHIVkGPUPipelineStateObject*>(vkPipeline->GetPipelineStateObject());

        if (pso->GetBindlessTable())
        {
            auto* table = static_cast<RHIVkBindlessDescriptorTable*>(pso->GetBindlessTable());
            bindlessSet = static_cast<RHIVkDescriptorHeap*>(table->GetHeap())->GetVkDescriptorSet();
        }
        else
        {
            auto* bindlessManager = vkDevice->GetBindlessManager();
            if (bindlessManager)
            {
                bindlessSet = bindlessManager->GetDescriptorSet();
            }
        }

        if (bindlessSet != VK_NULL_HANDLE)
        {
            VkPipelineLayout layout = vkPipeline->GetPipelineLayout(frameIndex);
            ::vkCmdBindDescriptorSets(cmd->m_VkCommandBuffer,
                                      static_cast<VkPipelineBindPoint>(pipeline->GetBindPoint()),
                                      layout,
                                      3, 1, &bindlessSet, 0, nullptr);
        }
    }

    void RHIVkExecutor::BindDescriptorSets(EPipelineBindPoint bindPoint, UInt32 firstSet,
                                           RHIDescriptorPoolHandle poolHandle, UInt32 poolId, UInt32 setIndex,
                                           bool singleSet)
    {
        ARISEN_PROFILE_ZONE("Vk::BindDescriptorSets");
        auto* vkDevice = cmd->GetVkDevice();
        auto* poolItem = vkDevice->GetDescriptorPoolPool()->Get(poolHandle);
        if (poolItem == nullptr) return;

        RHIDescriptorPool* pool = poolItem->pool;
        if (pool == nullptr) return;


        UInt32 frameIndex = cmd->GetCurrentFrameIndex();
        if (cmd->m_CurrentPipeline == nullptr)
        {
            LOG_FATAL("[RHIVkExecutor::BindDescriptorSets] pipeline is null, should binding pipeline first.");
            return;
        }

        RHIVkGPUPipeline* pipeline = static_cast<RHIVkGPUPipeline*>(cmd->m_CurrentPipeline);
        auto& sets = pool->GetDescriptorSets(poolId);

        cmd->m_VkDescriptorSets.clear();

        if (singleSet)
        {
            if (setIndex >= sets.size()) return;
            cmd->m_VkDescriptorSets.emplace_back(static_cast<VkDescriptorSet>(sets[setIndex]->GetHandle()));
        }
        else
        {
            cmd->m_VkDescriptorSets.reserve(sets.size());
            for (UInt32 i = 0; i < sets.size(); ++i)
            {
                cmd->m_VkDescriptorSets.emplace_back(static_cast<VkDescriptorSet>(sets[i]->GetHandle()));
            }
        }

        if (!cmd->m_VkDescriptorSets.empty())
        {
            ::vkCmdBindDescriptorSets(cmd->m_VkCommandBuffer,
                                      static_cast<VkPipelineBindPoint>(bindPoint),
                                      pipeline->GetPipelineLayout(frameIndex),
                                      firstSet,
                                      static_cast<uint32_t>(cmd->m_VkDescriptorSets.size()),
                                      cmd->m_VkDescriptorSets.data(),
                                      0,
                                      nullptr);
        }

        this->TrackDescriptorPoolUse(poolHandle, poolId);
    }

    // Removing the raw pointer overload since it is deprecated in this path


    void RHIVkExecutor::BindDescriptorBuffers(UInt32 bufferCount, const RHIBufferHandle* pBuffers)
    {
        ARISEN_PROFILE_ZONE("Vk::BindDescriptorBuffers");
        auto* vkDevice = cmd->GetVkDevice();
        if (!vkDevice->GetCapabilities().supportDescriptorBuffer) return;

        cmd->m_VkDescriptorBufferBindingInfos.clear();
        cmd->m_VkDescriptorBufferBindingInfos.reserve(bufferCount);

        for (UInt32 i = 0; i < bufferCount; ++i)
        {
            auto* buf = vkDevice->GetBufferPool()->Get(pBuffers[i]);
            if (buf)
            {
                VkDescriptorBufferBindingInfoEXT info{};
                info.sType = VK_STRUCTURE_TYPE_DESCRIPTOR_BUFFER_BINDING_INFO_EXT;
                info.address = vkDevice->GetFactory()->GetBufferDeviceAddress(pBuffers[i]);
                info.usage = 0;

                // Map RHI usage bits to Vulkan extension bits
                if (buf->usage & (UInt32)BUFFER_USAGE_SAMPLER_DESCRIPTOR_BUFFER_BIT_EXT)
                    info.usage |= VK_BUFFER_USAGE_SAMPLER_DESCRIPTOR_BUFFER_BIT_EXT;
                if (buf->usage & (UInt32)BUFFER_USAGE_RESOURCE_DESCRIPTOR_BUFFER_BIT_EXT)
                    info.usage |= VK_BUFFER_USAGE_RESOURCE_DESCRIPTOR_BUFFER_BIT_EXT;

                cmd->m_VkDescriptorBufferBindingInfos.emplace_back(info);
            }
            cmd->CaptureResource(pBuffers[i]);
        }

        if (!cmd->m_VkDescriptorBufferBindingInfos.empty() && vkDevice->vkCmdBindDescriptorBuffersEXT)
        {
            vkDevice->vkCmdBindDescriptorBuffersEXT(cmd->m_VkCommandBuffer,
                                                    (uint32_t)cmd->m_VkDescriptorBufferBindingInfos.size(),
                                                    cmd->m_VkDescriptorBufferBindingInfos.data());
        }
    }

    void RHIVkExecutor::SetDescriptorBufferOffsets(EPipelineBindPoint bindPoint, RHIPipelineHandle pipelineHandle,
                                                   UInt32 firstSet, UInt32 setCount, const UInt32* pIndices,
                                                   const UInt64* pOffsets)
    {
        ARISEN_PROFILE_ZONE("Vk::SetDescriptorBufferOffsets");
        auto* vkDevice = cmd->GetVkDevice();
        if (!vkDevice->GetCapabilities().supportDescriptorBuffer) return;

        VkPipelineLayout vkLayout = VK_NULL_HANDLE;
        if (pipelineHandle.IsValid())
        {
            auto* p = vkDevice->GetPipelinePool()->Get(pipelineHandle);
            if (p && p->pipeline)
            {
                vkLayout = static_cast<RHIVkGPUPipeline*>(p->pipeline)->GetPipelineLayout(cmd->GetCurrentFrameIndex());
            }
        }
        else if (cmd->m_CurrentPipeline)
        {
            vkLayout = static_cast<RHIVkGPUPipeline*>(cmd->m_CurrentPipeline)->GetPipelineLayout(
                cmd->GetCurrentFrameIndex());
        }

        if (vkLayout != VK_NULL_HANDLE && vkDevice->vkCmdSetDescriptorBufferOffsetsEXT)
        {
            vkDevice->vkCmdSetDescriptorBufferOffsetsEXT(cmd->m_VkCommandBuffer, (VkPipelineBindPoint)bindPoint,
                                                         vkLayout, firstSet, setCount, pIndices, pOffsets);
        }
    }

    void RHIVkExecutor::PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags)
    {
        ARISEN_PROFILE_ZONE("Vk::PushConstants");
        UInt32 frameIndex = cmd->GetCurrentFrameIndex();
        if (cmd->m_CurrentPipeline == nullptr)
        {
            LOG_FATAL("[RHIVkCommandBuffer::PushConstants] pipeline is null, should binding pipeline first.");
            return;
        }

        RHIVkGPUPipeline* pipeline = static_cast<RHIVkGPUPipeline*>(cmd->m_CurrentPipeline);
        ::vkCmdPushConstants(cmd->m_VkCommandBuffer, pipeline->GetPipelineLayout(frameIndex),
                             static_cast<VkShaderStageFlags>(stageFlags), offset, size, data);
    }

    void RHIVkExecutor::CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst,
                                          EImageLayout dstImageLayout, UInt32 regionCount,
                                          const RHIBufferImageCopy* regions)
    {
        ARISEN_PROFILE_ZONE("Vk::CopyBufferToImage");
        auto* vkDevice = cmd->GetVkDevice();
        auto* srcBuf = vkDevice->GetBufferPool()->Get(srcBuffer);
        auto* dstImg = vkDevice->GetImagePool()->Get(dst);

        if (!srcBuf || !dstImg || !regions) return;

        cmd->m_VkBufferImageCopies.clear();
        cmd->m_VkBufferImageCopies.reserve(regionCount);
        for (UInt32 i = 0; i < regionCount; ++i)
        {
            auto regionInfo = regions[i];
            cmd->m_VkBufferImageCopies.emplace_back(BufferImageCopyRegion(regionInfo.bufferOffset,
                                                                          regionInfo.bufferRowLength,
                                                                          regionInfo.bufferImageHeight,
                                                                          regionInfo.imageSubresource,
                                                                          regionInfo.offsetX, regionInfo.offsetY,
                                                                          regionInfo.offsetZ,
                                                                          regionInfo.width, regionInfo.height,
                                                                          regionInfo.depth));
        }

        ::vkCmdCopyBufferToImage(cmd->m_VkCommandBuffer,
                                 srcBuf->buffer, dstImg->image,
                                 static_cast<VkImageLayout>(dstImageLayout),
                                 static_cast<uint32_t>(cmd->m_VkBufferImageCopies.size()),
                                 cmd->m_VkBufferImageCopies.data()
        );

        cmd->CaptureResource(srcBuffer);
        cmd->CaptureResource(dst);
    }


    void RHIVkExecutor::CopyImage(RHIImageHandle src, EImageLayout srcLayout, RHIImageHandle dst,
                                  EImageLayout dstLayout, UInt32 regionCount, const RHIImageCopy* pRegions)
    {
        ARISEN_PROFILE_ZONE("Vk::CopyImage");
        auto* vkDevice = cmd->GetVkDevice();
        auto* srcImg = vkDevice->GetImagePool()->Get(src);
        auto* dstImg = vkDevice->GetImagePool()->Get(dst);

        if (!srcImg || !dstImg || !pRegions) return;

        Containers::Vector<VkImageCopy> vkRegions;
        vkRegions.reserve(regionCount);
        for (UInt32 i = 0; i < regionCount; ++i)
        {
            vkRegions.emplace_back(ImageCopyRegion(
                pRegions[i].srcSubresource, pRegions[i].srcOffset,
                pRegions[i].dstSubresource, pRegions[i].dstOffset,
                pRegions[i].extent));
        }

        ::vkCmdCopyImage(cmd->m_VkCommandBuffer,
                         srcImg->image, static_cast<VkImageLayout>(srcLayout),
                         dstImg->image, static_cast<VkImageLayout>(dstLayout),
                         regionCount, vkRegions.data());

        cmd->CaptureResource(dst);
    }


    void ArisenEngine::RHI::RHIVkExecutor::DoPipelineBarrier(
        EPipelineStageFlag srcStage, EPipelineStageFlag dstStage, UInt32 dependency,
        const RHIMemoryBarrier* pMemoryBarriers, UInt32 memoryBarrierCount,
        const RHIImageMemoryBarrier* pImageMemoryBarriers, UInt32 imageMemoryBarrierCount,
        const RHIBufferMemoryBarrier* pBufferMemoryBarriers, UInt32 bufferMemoryBarrierCount)
    {
        ARISEN_PROFILE_ZONE("Vk::PipelineBarrier");
        cmd->m_VkMemoryBarriers.clear();
        cmd->m_VkMemoryBarriers.reserve(memoryBarrierCount);
        cmd->m_VkBufferMemoryBarriers.clear();
        cmd->m_VkBufferMemoryBarriers.reserve(bufferMemoryBarrierCount);
        cmd->m_VkImageMemoryBarriers.clear();
        cmd->m_VkImageMemoryBarriers.reserve(imageMemoryBarrierCount);

        for (UInt32 i = 0; i < memoryBarrierCount; ++i)
        {
            const auto& barrier = pMemoryBarriers[i];
            cmd->m_VkMemoryBarriers.emplace_back(MemoryBarrier2(
                MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE ? barrier.srcStageMask : srcStage),
                MapAccessFlags2(barrier.srcAccessMask),
                MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE ? barrier.dstStageMask : dstStage),
                MapAccessFlags2(barrier.dstAccessMask)));
        }

        for (UInt32 i = 0; i < bufferMemoryBarrierCount; ++i)
        {
            const auto& barrier = pBufferMemoryBarriers[i];

            // Resolve Buffer Handle
            auto* vkDevice = cmd->GetVkDevice();
            auto* buf = vkDevice->GetBufferPool()->Get(barrier.buffer);
            if (!buf) continue;

            cmd->m_VkBufferMemoryBarriers.emplace_back(BufferMemoryBarrier2(
                MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE ? barrier.srcStageMask : srcStage),
                MapAccessFlags2(barrier.srcAccessMask),
                MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE ? barrier.dstStageMask : dstStage),
                MapAccessFlags2(barrier.dstAccessMask),
                barrier.srcQueueFamilyIndex, barrier.dstQueueFamilyIndex,
                buf->buffer, 0, VK_WHOLE_SIZE));

            cmd->CaptureResource(barrier.buffer);
        }

        for (UInt32 i = 0; i < imageMemoryBarrierCount; ++i)
        {
            const auto& barrier = pImageMemoryBarriers[i];

            // Resolve Image Handle
            auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
            auto* img = vkDevice->GetImagePool()->Get(barrier.image);
            if (!img) continue;

#ifdef RHI_VALIDATION
            if (barrier.oldLayout != IMAGE_LAYOUT_UNDEFINED && img->currentLayout != static_cast<VkImageLayout>(barrier.
                oldLayout))
            {
                LOG_WARNF(
                    "[RHIVkCommandBuffer::PipelineBarrier]: Layout mismatch for image! Tracked: {0}, Provided OldLayout: {1}",
                    (int)img->currentLayout, (int)barrier.oldLayout);
            }
#endif

            VkImageMemoryBarrier2KHR vkBarrier{};
            vkBarrier.sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2_KHR;
            vkBarrier.srcStageMask = MapPipelineStageFlags2(barrier.srcStageMask != PIPELINE_STAGE_NONE
                                                                ? barrier.srcStageMask
                                                                : srcStage);
            vkBarrier.srcAccessMask = MapAccessFlags2(barrier.srcAccess);
            vkBarrier.dstStageMask = MapPipelineStageFlags2(barrier.dstStageMask != PIPELINE_STAGE_NONE
                                                                ? barrier.dstStageMask
                                                                : dstStage);
            vkBarrier.dstAccessMask = MapAccessFlags2(barrier.dstAccess);
            vkBarrier.srcQueueFamilyIndex = barrier.srcQueueFamilyIndex;
            vkBarrier.dstQueueFamilyIndex = barrier.dstQueueFamilyIndex;
            vkBarrier.oldLayout = static_cast<VkImageLayout>(barrier.oldLayout);
            vkBarrier.newLayout = static_cast<VkImageLayout>(barrier.newLayout);
            vkBarrier.image = img->image;
            vkBarrier.subresourceRange.aspectMask = static_cast<VkImageAspectFlags>(barrier.subresourceRange.
                aspectMask);
            vkBarrier.subresourceRange.baseMipLevel = barrier.subresourceRange.baseMipLevel;
            vkBarrier.subresourceRange.levelCount = barrier.subresourceRange.levelCount;
            vkBarrier.subresourceRange.baseArrayLayer = barrier.subresourceRange.baseArrayLayer;
            vkBarrier.subresourceRange.layerCount = barrier.subresourceRange.layerCount;

            cmd->m_VkImageMemoryBarriers.push_back(vkBarrier);

            // Update tracked layout
            img->currentLayout = static_cast<VkImageLayout>(barrier.newLayout);

            cmd->CaptureResource(barrier.image);
        }

        VkDependencyInfoKHR dependencyInfo = DependencyInfo(
            static_cast<uint32_t>(cmd->m_VkMemoryBarriers.size()), cmd->m_VkMemoryBarriers.data(),
            static_cast<uint32_t>(cmd->m_VkBufferMemoryBarriers.size()), cmd->m_VkBufferMemoryBarriers.data(),
            static_cast<uint32_t>(cmd->m_VkImageMemoryBarriers.size()), cmd->m_VkImageMemoryBarriers.data(),
            static_cast<VkDependencyFlags>(dependency));

        // Use extension function
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdPipelineBarrier2KHR)
        {
            vkDevice->vkCmdPipelineBarrier2KHR(cmd->m_VkCommandBuffer, &dependencyInfo);
        }
    }

    void RHIVkExecutor::PipelineBarrier(const RHICmdPipelineBarrier& command, const RHIMemoryBarrier* pMem,
                                        const RHIImageMemoryBarrier* pImg, const RHIBufferMemoryBarrier* pBuf)
    {
        DoPipelineBarrier(command.srcStage, command.dstStage, static_cast<UInt32>(command.dependency),
                          pMem, command.memoryBarrierCount,
                          pImg, command.imageMemoryBarrierCount,
                          pBuf, command.bufferMemoryBarrierCount);
    }

    void RHIVkExecutor::TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout)
    {
        if (oldLayout == targetLayout) return;

        RHIImageMemoryBarrier barrier{};
        barrier.image = image;
        barrier.oldLayout = oldLayout;
        barrier.newLayout = targetLayout;
        barrier.srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
        barrier.dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED;
        barrier.subresourceRange = {IMAGE_ASPECT_COLOR_BIT, 0, 1, 0, 1}; // Default to color, 1 level, 1 layer

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
            barrier.dstStageMask = static_cast<EPipelineStageFlag>(PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT |
                PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT);
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

        DoPipelineBarrier(barrier.srcStageMask, barrier.dstStageMask, 0, nullptr, 0, &barrier, 1, nullptr, 0);
    }


    void RHIVkExecutor::Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex, UInt32 firstInstance,
                             UInt32 firstBinding)
    {
        ARISEN_PROFILE_ZONE("Vk::Draw");
        if (cmd->m_VertexBuffers.size() > 0)
        {
            ::vkCmdBindVertexBuffers(cmd->m_VkCommandBuffer, firstBinding, cmd->m_VertexBuffers.size(),
                                     cmd->m_VertexBuffers.data(), cmd->m_VertexBindingOffsets.data());
        }
        ::vkCmdDraw(cmd->m_VkCommandBuffer, vertexCount, instanceCount, firstVertex, firstInstance);
    }

    void RHIVkExecutor::DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex, UInt32 vertexOffset,
                                    UInt32 firstInstance, UInt32 firstBinding)
    {
        ARISEN_PROFILE_ZONE("Vk::DrawIndexed");
        if (cmd->m_VertexBuffers.size() > 0)
        {
            ::vkCmdBindVertexBuffers(cmd->m_VkCommandBuffer, firstBinding, cmd->m_VertexBuffers.size(),
                                     cmd->m_VertexBuffers.data(), cmd->m_VertexBindingOffsets.data());
        }

        if (cmd->m_IndexBuffer.has_value())
        {
            ::vkCmdBindIndexBuffer(cmd->m_VkCommandBuffer, cmd->m_IndexBuffer.value(), cmd->m_IndexOffset.value(),
                                   static_cast<
                                       VkIndexType>(cmd->m_IndexType.value()));
        }

        ::vkCmdDrawIndexed(cmd->m_VkCommandBuffer, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    void RHIVkExecutor::DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
    {
        ARISEN_PROFILE_ZONE("Vk::DrawIndirect");
        if (cmd->m_VertexBuffers.size() > 0)
        {
            ::vkCmdBindVertexBuffers(cmd->m_VkCommandBuffer, 0, cmd->m_VertexBuffers.size(),
                                     cmd->m_VertexBuffers.data(), cmd->m_VertexBindingOffsets.data());
        }

        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        auto* buf = vkDevice->GetBufferPool()->Get(buffer);
        if (!buf) return;

        ::vkCmdDrawIndirect(cmd->m_VkCommandBuffer, buf->buffer, offset, drawCount, stride);
        cmd->CaptureResource(buffer);
    }

    void RHIVkExecutor::DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride)
    {
        ARISEN_PROFILE_ZONE("Vk::DrawIndexedIndirect");
        if (cmd->m_VertexBuffers.size() > 0)
        {
            ::vkCmdBindVertexBuffers(cmd->m_VkCommandBuffer, 0, cmd->m_VertexBuffers.size(),
                                     cmd->m_VertexBuffers.data(), cmd->m_VertexBindingOffsets.data());
        }

        if (cmd->m_IndexBuffer.has_value())
        {
            ::vkCmdBindIndexBuffer(cmd->m_VkCommandBuffer, cmd->m_IndexBuffer.value(), cmd->m_IndexOffset.value(),
                                   static_cast<VkIndexType>(cmd->m_IndexType.value()));
        }

        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        auto* buf = vkDevice->GetBufferPool()->Get(buffer);
        if (!buf) return;

        ::vkCmdDrawIndexedIndirect(cmd->m_VkCommandBuffer, buf->buffer, offset, drawCount, stride);
        cmd->CaptureResource(buffer);
    }

    void RHIVkExecutor::Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
    {
        ARISEN_PROFILE_ZONE("Vk::Dispatch");
        ::vkCmdDispatch(cmd->m_VkCommandBuffer, groupCountX, groupCountY, groupCountZ);
    }

    void RHIVkExecutor::DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ)
    {
        ARISEN_PROFILE_ZONE("Vk::DrawMeshTasks");
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdDrawMeshTasksEXT)
        {
            vkDevice->vkCmdDrawMeshTasksEXT(cmd->m_VkCommandBuffer, groupCountX, groupCountY, groupCountZ);
        }
        else
        {
            LOG_ERROR("[RHIVkCommandBuffer::DrawMeshTasks]: vkCmdDrawMeshTasksEXT not found!");
        }
    }

    void RHIVkExecutor::BindVertexBuffers(RHIBufferHandle buffer, UInt64 offset)
    {
        ARISEN_PROFILE_ZONE("Vk::BindVertexBuffers");
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        auto* buf = vkDevice->GetBufferPool()->Get(buffer);
        if (!buf) return;

        cmd->m_VertexBuffers.emplace_back(buf->buffer);
        cmd->m_VertexBindingOffsets.emplace_back(offset);
        cmd->CaptureResource(buffer);
    }


    void RHIVkExecutor::CopyBuffer(RHIBufferHandle src, UInt64 srcOffset,
                                   RHIBufferHandle dst, UInt64 dstOffset, UInt64 size)
    {
        ARISEN_PROFILE_ZONE("Vk::CopyBuffer");
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        auto* srcBuf = vkDevice->GetBufferPool()->Get(src);
        auto* dstBuf = vkDevice->GetBufferPool()->Get(dst);

        if (!srcBuf || !dstBuf) return;

        VkBufferCopy copyRegion{};
        copyRegion.srcOffset = srcOffset;
        copyRegion.dstOffset = dstOffset;
        copyRegion.size = size;
        ::vkCmdCopyBuffer(cmd->m_VkCommandBuffer, srcBuf->buffer, dstBuf->buffer, 1, &copyRegion);

        cmd->CaptureResource(src);
        cmd->CaptureResource(dst);
    }

    void RHIVkExecutor::BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset, EIndexType type)
    {
        ARISEN_PROFILE_ZONE("Vk::BindIndexBuffer");
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        auto* buf = vkDevice->GetBufferPool()->Get(indexBuffer);
        if (!buf) return;

        cmd->m_IndexBuffer = buf->buffer;
        cmd->m_IndexOffset = offset;
        cmd->m_IndexType = type;
        cmd->CaptureResource(indexBuffer);
    }


    void RHIVkExecutor::GenerateMipmaps(RHIImageHandle image)
    {
        ARISEN_PROFILE_ZONE("Vk::GenerateMipmaps");
        if (!image.IsValid()) return;
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        auto* img = vkDevice->GetImagePool()->Get(image);
        if (!img) return;

        uint32_t mipLevels = img->mipLevels;
        uint32_t width = img->width;
        uint32_t height = img->height;

        for (uint32_t i = 1; i < mipLevels; i++)
        {
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
                DoPipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_TRANSFER_BIT, 0, nullptr, 0, &barrier, 1,
                                  nullptr, 0);
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
                DoPipelineBarrier(PIPELINE_STAGE_TOP_OF_PIPE_BIT, PIPELINE_STAGE_TRANSFER_BIT, 0, nullptr, 0, &barrier,
                                  1, nullptr, 0);
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
            blit.dstOffsets[1] = {
                static_cast<int32_t>(width > 1 ? width / 2 : 1),
                static_cast<int32_t>(height > 1 ? height / 2 : 1), 1
            };
            blit.dstSubresource.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
            blit.dstSubresource.mipLevel = i;
            blit.dstSubresource.baseArrayLayer = 0;
            blit.dstSubresource.layerCount = 1;

            ::vkCmdBlitImage(cmd->m_VkCommandBuffer, img->image, VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
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
                DoPipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, nullptr, 0,
                                  &barrier, 1, nullptr, 0);
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
            DoPipelineBarrier(PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT, 0, nullptr, 0, &barrier,
                              1, nullptr, 0);
        }
        cmd->CaptureResource(image);
    }

    void RHIVkExecutor::BeginDebugLabel(const char* label, const Float32 color[4])
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdBeginDebugUtilsLabelEXT)
        {
            VkDebugUtilsLabelEXT labelInfo{};
            labelInfo.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT;
            labelInfo.pLabelName = label;
            if (color)
            {
                memcpy(labelInfo.color, color, sizeof(float) * 4);
            }
            vkDevice->vkCmdBeginDebugUtilsLabelEXT(cmd->m_VkCommandBuffer, &labelInfo);
        }
    }

    void RHIVkExecutor::EndDebugLabel()
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdEndDebugUtilsLabelEXT)
        {
            vkDevice->vkCmdEndDebugUtilsLabelEXT(cmd->m_VkCommandBuffer);
        }
    }

    void RHIVkExecutor::InsertDebugMarker(const char* label, const Float32 color[4])
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdInsertDebugUtilsLabelEXT)
        {
            VkDebugUtilsLabelEXT labelInfo{};
            labelInfo.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_LABEL_EXT;
            labelInfo.pLabelName = label;
            if (color)
            {
                memcpy(labelInfo.color, color, sizeof(float) * 4);
            }
            vkDevice->vkCmdInsertDebugUtilsLabelEXT(cmd->m_VkCommandBuffer, &labelInfo);
        }
    }

    VkFence RHIVkCommandBuffer::GetSubmissionFence() const
    {
        // Fence ownership is separated from command buffer. Queue/device owns synchronization.
        return VK_NULL_HANDLE;
    }

    void RHIVkCommandBuffer::ResetInternal()
    {
        m_IsCompiled = false;
        SetCurrentFrameIndex(0);
        m_CommandStream.clear();
        SetLatestSubmitTicket(0);

        // Always clear tracked resources when resetting
        auto* vkDevice = static_cast<RHIVkDevice*>(GetDevice());
        auto* registry = vkDevice->GetResourceRegistry();
        if (registry)
        {
            for (auto h : m_TrackedResourceHandles)
            {
                registry->Release(h);
            }
        }
        m_TrackedResourceHandles.clear();
        m_TrackedDescriptorPools.clear();

        if (GetState() == ECommandBufferState::Initial) return;

        m_VkBeginInfo = {};

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

    void RHIVkExecutor::BuildAccelerationStructures(UInt32 infoCount,
                                                    const RHIAccelerationStructureBuildGeometryInfo* pInfos,
                                                    const RHIAccelerationStructureBuildRangeInfo* const*
                                                    ppBuildRangeInfos)
    {
        ARISEN_PROFILE_ZONE("Vk::BuildAccelerationStructures");
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
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
            if (scratchBuf) vkInfo.scratchData.deviceAddress = vkDevice->m_MemoryAllocator->GetDeviceAddress(
                scratchBuf->buffer);

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
                    vkGeom.geometry.triangles.sType =
                        VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR;
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
                    vkGeom.geometry.instances.sType =
                        VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR;
                    vkGeom.geometry.instances.arrayOfPointers = rhiGeom.instances.arrayOfPointers ? VK_TRUE : VK_FALSE;
                    vkGeom.geometry.instances.data.deviceAddress = rhiGeom.instances.data;
                }

                vkGeometries.emplace_back(vkGeom);
            }

            vkGeometriesPerInfo.emplace_back(std::move(vkGeometries));
            vkInfo.pGeometries = vkGeometriesPerInfo.back().data();
            vkInfos.emplace_back(vkInfo);

            cmd->CaptureResource(rhiInfo.scratchData);
            if (dstAS) cmd->CaptureResource(rhiInfo.dstAccelerationStructure);
            if (srcAS) cmd->CaptureResource(rhiInfo.srcAccelerationStructure);
        }

        Containers::Vector<const VkAccelerationStructureBuildRangeInfoKHR*> vkRangeInfoPtrs;
        vkRangeInfoPtrs.reserve(infoCount);
        for (UInt32 i = 0; i < infoCount; ++i)
        {
            vkRangeInfoPtrs.push_back(
                reinterpret_cast<const VkAccelerationStructureBuildRangeInfoKHR*>(ppBuildRangeInfos[i]));
        }

        vkDevice->vkCmdBuildAccelerationStructuresKHR(cmd->m_VkCommandBuffer, infoCount, vkInfos.data(),
                                                      vkRangeInfoPtrs.data());
        LOG_DEBUGF("Built {0} acceleration structures", infoCount);
    }

    void RHIVkExecutor::TraceRays(const RHITraceRaysDescriptor& desc)
    {
        ARISEN_PROFILE_ZONE("Vk::TraceRays");
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
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

        vkDevice->vkCmdTraceRaysKHR(cmd->m_VkCommandBuffer, &raygenRegion, &missRegion, &hitRegion, &callableRegion,
                                    desc.width, desc.height, desc.depth);
    }

    void RHIVkExecutor::SetFragmentShadingRate(EShadingRate rate, EShadingRateCombiner combinerOp[2])
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (!vkDevice->vkCmdSetFragmentShadingRateKHR) return;

        VkExtent2D shadingRate = {1, 1};
        switch (rate)
        {
        case EShadingRate::Rate1x1: shadingRate = {1, 1};
            break;
        case EShadingRate::Rate1x2: shadingRate = {1, 2};
            break;
        case EShadingRate::Rate2x1: shadingRate = {2, 1};
            break;
        case EShadingRate::Rate2x2: shadingRate = {2, 2};
            break;
        case EShadingRate::Rate2x4: shadingRate = {2, 4};
            break;
        case EShadingRate::Rate4x2: shadingRate = {4, 2};
            break;
        case EShadingRate::Rate4x4: shadingRate = {4, 4};
            break;
        default: shadingRate = {1, 1};
            break;
        }

        VkFragmentShadingRateCombinerOpKHR combiners[2];
        combiners[0] = static_cast<VkFragmentShadingRateCombinerOpKHR>(combinerOp[0]);
        combiners[1] = static_cast<VkFragmentShadingRateCombinerOpKHR>(combinerOp[1]);

        vkDevice->vkCmdSetFragmentShadingRateKHR(cmd->m_VkCommandBuffer, &shadingRate, combiners);
    }

    void RHIVkExecutor::TrackDescriptorPoolUse(RHIDescriptorPoolHandle poolHandle, UInt32 poolId)
    {
        // Simple linear search to avoid duplicates
        for (const auto& p : cmd->m_TrackedDescriptorPools)
        {
            if (p.poolHandle == poolHandle) return;
        }
        cmd->m_TrackedDescriptorPools.push_back({poolHandle, poolId});
    }

    void RHIVkExecutor::SetViewport(Float32 x, Float32 y, Float32 width, Float32 height, Float32 minDepth,
                                    Float32 maxDepth)
    {
        VkViewport viewport{};
        viewport.x = x;
        viewport.y = height < 0 ? y - height : y;
        viewport.width = width;
        viewport.height = height;
        viewport.minDepth = minDepth;
        viewport.maxDepth = maxDepth;
        ::vkCmdSetViewport(cmd->m_VkCommandBuffer, 0, 1, &viewport);
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width, UInt32 height)
    {
        VkRect2D scissor{};
        scissor.offset = {(int32_t)offsetX, (int32_t)offsetY};
        scissor.extent = {width, height};
        ::vkCmdSetScissor(cmd->m_VkCommandBuffer, 0, 1, &scissor);
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetLineWidth(Float32 lineWidth)
    {
        ::vkCmdSetLineWidth(cmd->m_VkCommandBuffer, lineWidth);
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp,
                                                        Float32 depthBiasSlopeFactor)
    {
        ::vkCmdSetDepthBias(cmd->m_VkCommandBuffer, depthBiasConstantFactor, depthBiasClamp, depthBiasSlopeFactor);
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetBlendConstants(const Float32 blendConstants[4])
    {
        ::vkCmdSetBlendConstants(cmd->m_VkCommandBuffer, blendConstants);
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetStencilReference(UInt32 faceMask, UInt32 reference)
    {
        ::vkCmdSetStencilReference(cmd->m_VkCommandBuffer, static_cast<VkStencilFaceFlags>(faceMask), reference);
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetCullMode(ECullModeFlagBits cullMode)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetCullModeEXT)
            vkDevice->vkCmdSetCullModeEXT(cmd->m_VkCommandBuffer, static_cast<VkCullModeFlags>(cullMode));
    }

    void ArisenEngine::RHI::RHIVkExecutor::SetFrontFace(EFrontFace frontFace)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetFrontFaceEXT)
            vkDevice->vkCmdSetFrontFaceEXT(cmd->m_VkCommandBuffer, static_cast<VkFrontFace>(frontFace));
    }

    void RHIVkExecutor::SetPrimitiveTopology(EPrimitiveTopology topology)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetPrimitiveTopologyEXT)
            vkDevice->vkCmdSetPrimitiveTopologyEXT(cmd->m_VkCommandBuffer, static_cast<VkPrimitiveTopology>(topology));
    }

    void RHIVkExecutor::SetDepthTestEnable(bool enable)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetDepthTestEnableEXT)
            vkDevice->vkCmdSetDepthTestEnableEXT(cmd->m_VkCommandBuffer, enable ? VK_TRUE : VK_FALSE);
    }

    void RHIVkExecutor::SetDepthWriteEnable(bool enable)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetDepthWriteEnableEXT)
            vkDevice->vkCmdSetDepthWriteEnableEXT(cmd->m_VkCommandBuffer, enable ? VK_TRUE : VK_FALSE);
    }

    void RHIVkExecutor::SetDepthCompareOp(ECompareOp depthCompareOp)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetDepthCompareOpEXT)
            vkDevice->vkCmdSetDepthCompareOpEXT(cmd->m_VkCommandBuffer, static_cast<VkCompareOp>(depthCompareOp));
    }

    void RHIVkExecutor::SetStencilTestEnable(bool enable)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetStencilTestEnableEXT)
            vkDevice->vkCmdSetStencilTestEnableEXT(cmd->m_VkCommandBuffer, enable ? VK_TRUE : VK_FALSE);
    }

    void RHIVkExecutor::SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp,
                                     ECompareOp compareOp)
    {
        auto* vkDevice = static_cast<RHIVkDevice*>(cmd->GetDevice());
        if (vkDevice->vkCmdSetStencilOpEXT)
            vkDevice->vkCmdSetStencilOpEXT(cmd->m_VkCommandBuffer, static_cast<VkStencilFaceFlags>(faceMask),
                                           static_cast<VkStencilOp>(failOp), static_cast<VkStencilOp>(passOp),
                                           static_cast<VkStencilOp>(depthFailOp), static_cast<VkCompareOp>(compareOp));
    }

    void RHIVkCommandBuffer::Compile()
    {
        // TODO (Phase 4 Optimization): Implement explicit command grouping/sorting here 
        // to improve instruction cache locality and reduce state transitions.
        // This could involve a pre-scan of the command stream to bucket similar commands.
        RHIVkExecutor executor(this);
        Replay(executor);
        m_IsCompiled = true;
    }
} // namespace ArisenEngine::RHI
