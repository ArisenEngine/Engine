#pragma once

#include "Base/FoundationMinimal.h"
#include "RHI/Resources/RHIDeferredDeletionQueue.h" // RHIGpuTicket
#include <vulkan/vulkan_core.h>
#include <memory>
#include "RHI/Handles/RHIHandle.h"
#include <vector>
#include <unordered_map>

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
    class RHIVkStagingRingBuffer;
    class RHIVkQueue;

    /**
     * @brief Configuration for the TransferManager subsystem.
     */
    struct RHITransferManagerConfig
    {
        UInt64 ringBufferCapacity = 64 * 1024 * 1024; // Default 64 MB, configurable
    };

    /**
     * @brief Manages asynchronous GPU buffer uploads via a dedicated transfer queue.
     *
     * Batches multiple buffer copies into a single command buffer submission,
     * using a ring-buffer staging allocator and timeline semaphore synchronization.
     *
     * Usage:
     *   EnqueueBufferCopy(dst, src, size, offset);  // batch copies
     *   auto ticket = Flush();                       // submit all pending
     *   WaitForTicket(ticket);                       // block until done (optional)
     *   Update();                                    // poll completion + reclaim staging memory
     *
     * TODO(Transfer-P2): Add queue ownership transfer barriers (release on transfer queue,
     * acquire on graphics queue) when transfer and graphics queue families differ.
     * Required by Vulkan spec, currently works on desktop drivers without them.
     */
    class RHIVkTransferManager
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkTransferManager)

        RHIVkTransferManager(RHIVkDevice* device, const RHITransferManagerConfig& config = {});
        ~RHIVkTransferManager();

        /**
         * @brief Enqueue a buffer copy from CPU memory to a GPU buffer.
         * Does NOT submit to the GPU yet — call Flush() after batching.
         */
        void EnqueueBufferCopy(VkBuffer dstBuffer, const void* srcData,
                               UInt64 size, UInt64 dstOffset, uint32_t dstQueueFamilyIndex = ~0u);

        /**
         * @brief Submit all pending copies as a single command buffer to the transfer queue.
         * @return The RHIGpuTicket for this submission (0 if nothing to flush).
         */
        RHIGpuTicket Flush();

        /**
         * @brief Block the CPU until the specified ticket completes on the GPU.
         */
        void WaitForTicket(RHIGpuTicket ticket);

        /**
         * @brief Poll GPU completion and reclaim staging ring buffer memory.
         * Should be called once per frame.
         */
        void Update();

    private:
        void EnsureCommandBufferReady();

        RHIVkDevice* m_Device;
        RHIVkQueue* m_TransferQueue;

        std::unique_ptr<RHIVkStagingRingBuffer> m_RingBuffer;

        // Persistent command pool for transfer operations
        RHICommandBufferPoolHandle m_TransferCommandPool;
        RHICommandBufferHandle m_CurrentCommandBuffer;
        bool m_CommandBufferRecording{false};

        // Command pools for target queue acquire barriers
        std::unordered_map<uint32_t, RHICommandBufferPoolHandle> m_AcquireCommandPools;

        struct PendingCopy
        {
            VkBuffer srcBuffer;   // ring buffer
            VkBuffer dstBuffer;
            VkBufferCopy region;
            uint32_t dstQueueFamilyIndex;
        };

        std::vector<PendingCopy> m_PendingCopies;
    };
}
