#pragma once
#include "Presentation/RHIVkFrameBuffer.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Sync/RHIBufferMemoryBarrier.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Sync/RHIMemoryBarrier.h"
#include <vulkan/vulkan_core.h>
#include <thread>


namespace ArisenEngine::RHI {
class RHIVkCommandBufferPool;
class RHIVkDevice;
class RHIDescriptorPool;
class RHIVkCommandBuffer final : public RHICommandBuffer {
public:
  NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBuffer)
  ~RHIVkCommandBuffer() noexcept override;
  RHIVkCommandBuffer(RHIVkDevice *device, RHIVkCommandBufferPool *pool);


  void BeginRenderPass(UInt32 frameIndex, RenderPassBeginDesc &&desc) override;
  void EndRenderPass() override;
  void Begin(UInt32 frameIndex) override;
  void Begin(UInt32 frameIndex, UInt32 commandBufferUsage) override;
  void End() override;

  void BeginRendering(const RHIRenderingInfo &info) override;
  void EndRendering() override;

  void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height,
                   Float32 minDepth, Float32 maxDepth) override;
  void SetViewport(Float32 x, Float32 y, Float32 width,
                   Float32 height) override;
  void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width,
                  UInt32 height) override;

  void BindPipeline(UInt32 frameIndex, RHIPipelineHandle pipeline) override;

  void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex,
            UInt32 firstInstance, UInt32 firstBinding) override;
  void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex,
                   UInt32 vertexOffset, UInt32 firstInstance,
                   UInt32 firstBinding) override;
  void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) override;
  void BindVertexBuffers(RHIBufferHandle buffers, UInt64 offset) override;
  void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset,
                       EIndexType type) override;

  void WaitSemaphore(RHISemaphoreHandle semaphore,
                     EPipelineStageFlag stage) override;
  void SignalSemaphore(RHISemaphoreHandle semaphore) override;
  void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset,
                  RHIBufferHandle dst, UInt64 dstOffset,
                  UInt64 size) override;

  void BindDescriptorSets(
      UInt32 frameIndex, EPipelineBindPoint bindPoint, UInt32 firstSet,
      Containers::Vector<std::shared_ptr<RHIDescriptorSet>> &descriptorsets,
      UInt32 dynamicOffsetCount, const UInt32 *pDynamicOffsets) override;
  void TrackDescriptorPoolUse(RHIDescriptorPool *pool, UInt32 poolId) override;

  void
  CopyBufferToImage(RHIBufferHandle srcBuffer, RHIImageHandle dst,
                    EImageLayout dstImageLayout,
                    Containers::Vector<RHIBufferImageCopy> &&regions) override;
  void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage,
                       UInt32 dependency,
                       const RHIMemoryBarrier *pMemoryBarriers,
                       UInt32 memoryBarrierCount,
                       const RHIImageMemoryBarrier *pImageMemoryBarriers,
                       UInt32 imageMemoryBarrierCount,
                       const RHIBufferMemoryBarrier *pBufferMemoryBarriers,
                       UInt32 bufferMemoryBarrierCount) override;
  void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage,
                       UInt32 dependency,
                       const RHIMemoryBarrier *pMemoryBarriers,
                       UInt32 memoryBarrierCount) override;
  void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage,
                       UInt32 dependency,
                       const RHIImageMemoryBarrier *pImageMemoryBarriers,
                       UInt32 imageMemoryBarrierCount) override;
  void PipelineBarrier(EPipelineStageFlag srcStage, EPipelineStageFlag dstStage,
                       UInt32 dependency,
                       const RHIBufferMemoryBarrier *pBufferMemoryBarriers,
                       UInt32 bufferMemoryBarrierCount) override;

  void GenerateMipmaps(RHIImageHandle image) override;

    private:
        friend class RHIVkQueue;
        // Vulkan only
        const VkSemaphore *GetWaitSemaphores() const;
        UInt32 GetWaitSemaphoresCount() const;
        const VkSemaphore *GetSignalSemaphores() const;
        UInt32 GetSignalSemaphoresCount() const;
        const VkPipelineStageFlags *GetWaitStageMask() const;
        VkFence GetSubmissionFence() const;

protected:
  void ResetInternal() override;

private:
  VkCommandBuffer m_VkCommandBuffer;
  VkCommandPool m_VkCommandPool;
  VkDevice m_VkDevice;
  Containers::Vector<VkBuffer> m_VertexBuffers;
  Containers::Vector<UInt64> m_VertexBindingOffsets;
  std::optional<VkBuffer> m_IndexBuffer;
  std::optional<UInt64> m_IndexOffset;
  std::optional<EIndexType> m_IndexType;

  Containers::Vector<VkSemaphore> m_WaitSemaphores;
  Containers::Vector<VkSemaphore> m_SignalSemaphores;
  Containers::Vector<VkPipelineStageFlags> m_WaitStages;

  VkCommandBufferBeginInfo m_VkBeginInfo{};
  // Fence ownership is separated from command buffer (owned by
  // queue/device/pool).

  Containers::Vector<VkMemoryBarrier2KHR> m_VkMemoryBarriers{};
  Containers::Vector<VkBufferMemoryBarrier2KHR> m_VkBufferMemoryBarriers{};
  Containers::Vector<VkImageMemoryBarrier2KHR> m_VkImageMemoryBarriers{};
  Containers::Vector<VkRenderingAttachmentInfoKHR> m_VkColorAttachments{};
  VkRenderingAttachmentInfoKHR m_VkDepthAttachment{};
  VkRenderingAttachmentInfoKHR m_VkStencilAttachment{};

  // Cached vectors for other commands
  Containers::Vector<VkDescriptorSet> m_VkDescriptorSets{};
  Containers::Vector<VkBufferImageCopy> m_VkBufferImageCopies{};

  RHIPipeline *m_CurrentPipeline{nullptr};

  struct TrackedPoolUse {
    RHIDescriptorPool *pool{nullptr};
    UInt32 poolId{0};
  };
  Containers::Vector<TrackedPoolUse> m_TrackedDescriptorPools;
  Containers::Vector<RHIResourceHandle> m_TrackedResourceHandles;

  std::thread::id m_OwnerThreadId;

  friend class RHIVkCommandBufferPool;
  friend class RHIVkQueue;
private:
  void CaptureResource(RHIBufferHandle buffer);
  void CaptureResource(RHIImageHandle image);

  const Containers::Vector<RHIResourceHandle>& GetTrackedResourceHandles() const {
    return m_TrackedResourceHandles;
  }
  void ClearTrackedResourceHandles() { m_TrackedResourceHandles.clear(); }

  const Containers::Vector<TrackedPoolUse>& GetTrackedDescriptorPools() const {
    return m_TrackedDescriptorPools;
  }
  void ClearTrackedDescriptorPools() { m_TrackedDescriptorPools.clear(); }
    };
} // namespace ArisenEngine::RHI




