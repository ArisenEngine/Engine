#pragma once
//#include "Presentation/RHIVkFrameBuffer.h"
#include "../../Core.RHI/RHI/Commands/RHICommandBuffer.h"
#include "../../Core.RHI/RHI/Commands/RHICommandDefs.h"
#include "Definitions/RHIVkCommon.h"
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
#include <optional>

namespace ArisenEngine
{
    namespace RHI
    {
        class RHIVkCommandBufferPool;
        class RHIVkDevice;
        class RHIDescriptorPool;

        class RHI_VULKAN_DLL RHIVkCommandBuffer final : public RHICommandBuffer
        {
        public:
            NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkCommandBuffer)
            ~RHIVkCommandBuffer() noexcept override;
            RHIVkCommandBuffer(RHIVkDevice* device, RHIVkCommandBufferPool* pool,
                               ECommandBufferLevel level = COMMAND_BUFFER_LEVEL_PRIMARY);

            void* GetHandle() const override { return m_VkCommandBuffer; }

            RHIVkDevice* GetVkDevice() const;

            void Compile();
            bool IsCompiled() const { return m_IsCompiled; }

        protected:
            void ResetInternal() override;

        private:
            VkCommandBuffer m_VkCommandBuffer;
            VkCommandPool m_VkCommandPool;
            VkDevice m_VkDevice;
            bool m_IsCompiled{false};
            Containers::Vector<VkBuffer> m_VertexBuffers;
            Containers::Vector<UInt64> m_VertexBindingOffsets;
            std::optional<VkBuffer> m_IndexBuffer;
            std::optional<UInt64> m_IndexOffset;
            std::optional<EIndexType> m_IndexType;

            VkCommandBufferBeginInfo m_VkBeginInfo{};
            // Fence ownership is separated from command buffer (owned by
            // queue/device/pool).

            // Vulkan only
            VkFence GetSubmissionFence() const;

            Containers::Vector<VkMemoryBarrier2> m_VkMemoryBarriers{};
            Containers::Vector<VkBufferMemoryBarrier2> m_VkBufferMemoryBarriers{};
            Containers::Vector<VkImageMemoryBarrier2> m_VkImageMemoryBarriers{};
            Containers::Vector<VkRenderingAttachmentInfo> m_VkColorAttachments{};

            // Cached vectors for other commands
            Containers::Vector<VkDescriptorSet> m_VkDescriptorSets{};
            Containers::Vector<VkDescriptorBufferBindingInfoEXT> m_VkDescriptorBufferBindingInfos{};
            Containers::Vector<VkCommandBuffer> m_VkSecondaryCommandBuffers{};
            Containers::Vector<VkBufferImageCopy> m_VkBufferImageCopies{};

            RHIPipeline* m_CurrentPipeline{nullptr};

            struct TrackedPoolUse
            {
                RHIDescriptorPoolHandle poolHandle;
                UInt32 poolId{0};
            };

            Containers::Vector<TrackedPoolUse> m_TrackedDescriptorPools;
            Containers::Vector<RHIResourceHandle> m_TrackedResourceHandles;

            std::thread::id m_OwnerThreadId;
            size_t m_OwnerThreadIndex;

            friend class RHIVkCommandBufferPool;
            friend class RHIVkQueue;
            friend struct RHIVkExecutor;

        public:
            void CaptureResource(RHIBufferHandle buffer);
            void CaptureResource(RHIImageHandle image);
            void CaptureResource(RHIAccelerationStructureHandle handle);

            const Containers::Vector<RHIResourceHandle>& GetTrackedResourceHandles() const
            {
                return m_TrackedResourceHandles;
            }

            void ClearTrackedResourceHandles() { m_TrackedResourceHandles.clear(); }

            const Containers::Vector<TrackedPoolUse>& GetTrackedDescriptorPools() const
            {
                return m_TrackedDescriptorPools;
            }

            void ClearTrackedDescriptorPools() { m_TrackedDescriptorPools.clear(); }
        };
    }
}
