#pragma once
#include "Presentation/RHIVkFrameBuffer.h"
#include "../../Core.RHI/RHI/Commands/RHICommandBuffer.h"
#include "RHI/Enums/Pipeline/EIndexType.h"
#include "RHI/Enums/Subpass/EDependencyFlag.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Sync/RHIBufferMemoryBarrier.h"
#include "RHI/Sync/RHIImageMemoryBarrier.h"
#include "RHI/Sync/RHIMemoryBarrier.h"
#include <vulkan/vulkan_core.h>
#include "RHI/Enums/Pipeline/ECullMode.h"
#include "RHI/Enums/Pipeline/EFrontFace.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Sampler/ECompareOp.h"
#include "RHI/Resources/RHIAccelerationStructure.h"
#include <thread>


namespace ArisenEngine::RHI {
class RHIVkCommandBufferPool;
class RHIVkDevice;
class RHIDescriptorPool;
class RHIVkCommandBuffer final : public RHICommandBuffer {
public:
  NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBuffer)
  ~RHIVkCommandBuffer() noexcept override;
  RHIVkCommandBuffer(RHIVkDevice *device, RHIVkCommandBufferPool *pool, ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY);
  
  void* GetHandle() const override { return m_VkCommandBuffer; }


  void BeginRenderPass(RenderPassBeginDesc &&desc) override;
  void EndRenderPass() override;
  void Begin() override;
  void Begin(UInt32 frameIndex, UInt32 commandBufferUsage = 0, const RHICommandBufferInheritanceInfo* pInheritanceInfo = nullptr) override;
  void End() override;

  void ExecuteCommands(Containers::Vector<RHICommandBuffer*>&& secondaryBuffers) override;

  void BeginRendering(const RHIRenderingInfo &info) override;
  void EndRendering() override;

  void SetViewport(Float32 x, Float32 y, Float32 width, Float32 height,
                   Float32 minDepth, Float32 maxDepth) override;
  void SetViewport(Float32 x, Float32 y, Float32 width,
                   Float32 height) override;
  void SetScissor(UInt32 offsetX, UInt32 offsetY, UInt32 width,
                  UInt32 height) override;
  void SetLineWidth(Float32 lineWidth) override;
  void SetDepthBias(Float32 depthBiasConstantFactor, Float32 depthBiasClamp, Float32 depthBiasSlopeFactor) override;
  void SetBlendConstants(const Float32 blendConstants[4]) override;
  void SetStencilReference(UInt32 faceMask, UInt32 reference) override;
  void SetCullMode(ECullModeFlagBits cullMode) override;
  void SetFrontFace(EFrontFace frontFace) override;
  void SetPrimitiveTopology(EPrimitiveTopology topology) override;
  void SetDepthTestEnable(bool enable) override;
  void SetDepthWriteEnable(bool enable) override;
  void SetDepthCompareOp(ECompareOp depthCompareOp) override;
  void SetStencilTestEnable(bool enable) override;
  void SetStencilOp(UInt32 faceMask, EStencilOp failOp, EStencilOp passOp, EStencilOp depthFailOp, ECompareOp compareOp) override;

  void BindPipeline(RHIPipelineHandle pipeline) override;

  void Draw(UInt32 vertexCount, UInt32 instanceCount, UInt32 firstVertex,
            UInt32 firstInstance, UInt32 firstBinding) override;
  void DrawIndexed(UInt32 indexCount, UInt32 instanceCount, UInt32 firstIndex,
                   UInt32 vertexOffset, UInt32 firstInstance,
                   UInt32 firstBinding) override;
  void DrawIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) override;
  void DrawIndexedIndirect(RHIBufferHandle buffer, UInt64 offset, UInt32 drawCount, UInt32 stride) override;
  void Dispatch(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) override;
  void DrawMeshTasks(UInt32 groupCountX, UInt32 groupCountY, UInt32 groupCountZ) override;
  void BindVertexBuffers(RHIBufferHandle buffers, UInt64 offset) override;
  void BindIndexBuffer(RHIBufferHandle indexBuffer, UInt64 offset,
                       EIndexType type) override;

  void CopyBuffer(RHIBufferHandle src, UInt64 srcOffset,
                  RHIBufferHandle dst, UInt64 dstOffset,
                  UInt64 size) override;

  void BindDescriptorSets(
      EPipelineBindPoint bindPoint, UInt32 firstSet,
      Containers::Vector<std::shared_ptr<RHIDescriptorSet>> &descriptorsets,
      UInt32 dynamicOffsetCount, const UInt32 *pDynamicOffsets) override;
  
  void PushConstants(UInt32 offset, UInt32 size, const void* data, UInt32 stageFlags) override;

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

  void TransitionImageLayout(RHIImageHandle image, EImageLayout targetLayout) override;
  void TransitionImageLayout(RHIImageHandle image, EImageLayout oldLayout, EImageLayout targetLayout) override;

  void GenerateMipmaps(RHIImageHandle image) override;

  // Ray Tracing
  void BuildAccelerationStructures(UInt32 infoCount, const RHIAccelerationStructureBuildGeometryInfo* pInfos, const RHIAccelerationStructureBuildRangeInfo* const* ppBuildRangeInfos) override;

  // Debug Markers
  void BeginDebugLabel(const char* label, const Float32 color[4]) override;
  void EndDebugLabel() override;
  void InsertDebugMarker(const char* label, const Float32 color[4]) override;

    private:
        friend class RHIVkQueue;
        // Vulkan only
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
  Containers::Vector<VkCommandBuffer> m_VkSecondaryCommandBuffers{};
  Containers::Vector<VkBufferImageCopy> m_VkBufferImageCopies{};

  RHIPipeline *m_CurrentPipeline{nullptr};

  struct TrackedPoolUse {
    RHIDescriptorPool *pool{nullptr};
    UInt32 poolId{0};
  };
  Containers::Vector<TrackedPoolUse> m_TrackedDescriptorPools;
  Containers::Vector<RHIResourceHandle> m_TrackedResourceHandles;

  std::thread::id m_OwnerThreadId;
  size_t m_OwnerThreadIndex;

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




