#include "RHI/Allocation/RHIMemoryAllocator.h"
#include "RHI/Commands/RHICommandBuffer.h"
#include "RHI/Commands/RHICommandBufferPool.h"
#include "RHI/Core/RHIDevice.h"
#include "RHI/Core/RHIInstance.h"
#include "RHI/Descriptors/RHIBindlessDescriptorTable.h"
#include "RHI/Descriptors/RHIDescriptorHeap.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "RHI/Descriptors/RHIDescriptorSet.h"
#include "RHI/Pipeline/RHIPipeline.h"
#include "RHI/Pipeline/RHIPipelineCache.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/Pipeline/RHIShaderProgram.h"
#include "RHI/Presentation/RHISurface.h"
#include "RHI/Queues/RHIQueue.h"
#include "RHI/RenderPass/RHIFrameBuffer.h"
#include "RHI/RenderPass/RHIRenderPass.h"
#include "RHI/Samplers/RHISampler.h"
#include "RHI/Sync/RHIFence.h"
#include "RHI/Sync/RHISemaphore.h"

namespace ArisenEngine::RHI
{
    // RHIMemoryAllocator
    RHIMemoryAllocator::RHIMemoryAllocator() = default;
    RHIMemoryAllocator::~RHIMemoryAllocator() noexcept = default;


    // RHICommandBufferPool
    RHICommandBufferPool::RHICommandBufferPool(RHIDevice* device, UInt32 maxFramesInFlight, RHIQueueType queueType) :
        m_Device(device), m_MaxFramesInFlight(maxFramesInFlight), m_QueueType(queueType)
    {
    }

    RHICommandBufferPool::~RHICommandBufferPool() = default;

    // RHIPipeline
    RHIPipeline::RHIPipeline(UInt32 maxFramesInFlight) : m_MaxFramesInFlight(maxFramesInFlight)
    {
    }

    // RHIPipelineCache
    RHIPipelineCache::RHIPipelineCache(UInt32 maxFramesInFlight) : m_MaxFramesInFlight(maxFramesInFlight)
    {
    }

    // RHIPipelineState
    RHIPipelineState::RHIPipelineState() = default;
    RHIPipelineState::~RHIPipelineState() noexcept = default;

    // RHIShaderProgram
    RHIShaderProgram::RHIShaderProgram() = default;
    RHIShaderProgram::~RHIShaderProgram() noexcept = default;

    // RHISurface
    RHISurface::RHISurface(UInt32&& id, RHIInstance* instance) : m_RenderWindowId(id), m_Instance(instance)
    {
    }

    RHISurface::~RHISurface() noexcept
    {
        m_RenderWindowId = InvalidID;
        m_Instance = nullptr;
    }

    // RHISampler
    RHISampler::RHISampler(RHIDevice* device) : m_Device(device)
    {
    }

    RHISampler::~RHISampler() noexcept = default;

    // RHIFence
    RHIFence::RHIFence() = default;
    RHIFence::~RHIFence() noexcept = default;

    // RHISemaphore
    RHISemaphore::RHISemaphore() = default;
    RHISemaphore::~RHISemaphore() noexcept = default;

    // RHIRenderPass
    RHIRenderPass::RHIRenderPass(UInt32 maxFramesInFlight) : m_MaxFramesInFlight(maxFramesInFlight)
    {
    }

    // RHIFrameBuffer
    RHIFrameBuffer::RHIFrameBuffer(UInt32 maxFramesInFlight) : m_RenderArea(), m_MaxFramesInFlight(maxFramesInFlight)
    {
    }

    const RHIInputAssemblyState& RHIPipelineState::GetInputAssemblyState() const { return m_InputAssemblyState; }
    const RHIRasterizationState& RHIPipelineState::GetRasterizationState() const { return m_RasterizationState; }
    const RHIMultisampleState& RHIPipelineState::GetMultisampleState() const { return m_MultisampleState; }
    const RHIDepthStencilState& RHIPipelineState::GetDepthStencilState() const { return m_DepthStencilState; }
    void RHIPipelineState::SetDynamicStateMask(UInt64 mask) { m_DynamicStateMask = mask; }
    UInt64 RHIPipelineState::GetDynamicStateMask() const { return m_DynamicStateMask; }

    const char* RHIShaderProgram::GetEntry() const { return m_Entry.c_str(); }
    const EShaderStage RHIShaderProgram::GetShaderState() const { return m_Stage; }
    const String& RHIShaderProgram::GetName() const { return m_Name; }

    // RHIDescriptorPool
    RHIDescriptorPool::RHIDescriptorPool() = default;

    // RHIDescriptorSet
    RHIDescriptorSet::RHIDescriptorSet(RHIDescriptorPool* descriptorPool, UInt32 layoutIndex) :
        m_DescriptorPool(descriptorPool), m_LayoutIndex(layoutIndex)
    {
    }

    RHIDescriptorSet::~RHIDescriptorSet() noexcept = default;

    // RHIQueue
    RHIQueue::~RHIQueue() = default;

    RHIDevice* RHICommandBufferPool::GetDevice() const { return m_Device; }
}
