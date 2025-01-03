#include "RHIVkDevice.h"

#include "../Handles/RHIVkBufferHandle.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties)
: Device(instance, surface), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device), m_VkPhysicalDeviceMemoryProperties(memoryProperties)
{
    m_GPUPipelineManager = new RHIVkGPUPipelineManager(this, m_Instance->GetMaxFramesInFlight());
    m_DescriptorPool = new RHIVkDescriptorPool(this, m_Instance->GetMaxFramesInFlight());
}

void NebulaEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
}

void NebulaEngine::RHI::RHIVkDevice::GraphicQueueWaitIdle() const
{
    vkQueueWaitIdle(m_VkGraphicQueue);
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkDevice::CreateGPUProgram()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    u32 id = static_cast<u32>(m_GPUPrograms.size());
    m_GPUPrograms.insert({id, std::make_unique<RHIVkGPUProgram>(m_VkDevice)});
    return id;
}

NebulaEngine::RHI::GPUProgram* NebulaEngine::RHI::RHIVkDevice::GetGPUProgram(u32 programId)
{
    ASSERT(m_GPUPrograms[programId]);
    return m_GPUPrograms[programId].get();
}

void NebulaEngine::RHI::RHIVkDevice::DestroyGPUProgram(u32 programId)
{
    ASSERT(m_GPUPrograms[programId]);
    m_GPUPrograms.erase(programId);
}

bool NebulaEngine::RHI::RHIVkDevice::AttachProgramByteCode(u32 programId, GPUProgramDesc&& desc)
{
    ASSERT(m_GPUPrograms[programId]);
    return m_GPUPrograms[programId]->AttachProgramByteCode(std::move(desc));
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkDevice::CreateCommandBufferPool()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    u32 id = static_cast<u32>(m_CommandBufferPools.size());
    m_CommandBufferPools.insert(
        {
            id,
            std::make_unique<RHIVkCommandBufferPool>(this, m_Instance->GetMaxFramesInFlight())
        });
    return id;
}

NebulaEngine::RHI::RHICommandBufferPool* NebulaEngine::RHI::RHIVkDevice::GetCommandBufferPool(u32 id)
{
    ASSERT(m_CommandBufferPools[id]);
    return m_CommandBufferPools[id].get();
}

std::shared_ptr<NebulaEngine::RHI::GPURenderPass> NebulaEngine::RHI::RHIVkDevice::GetRenderPass()
{
    std::shared_ptr<GPURenderPass> renderPass;
    if (m_RenderPasses.size() > 0)
    {
        renderPass = m_RenderPasses.back();
        m_RenderPasses.pop_back();
    }
    else
    {
        renderPass = std::make_shared<RHIVkGPURenderPass>(m_VkDevice, m_Instance->GetMaxFramesInFlight());
    }

    return renderPass;
}

void NebulaEngine::RHI::RHIVkDevice::ReleaseRenderPass(std::shared_ptr<GPURenderPass> renderPass)
{
    m_RenderPasses.emplace_back(renderPass);
}

std::shared_ptr<NebulaEngine::RHI::FrameBuffer> NebulaEngine::RHI::RHIVkDevice::GetFrameBuffer()
{
    std::shared_ptr<FrameBuffer> frameBuffer;
    if (m_FrameBuffers.size() > 0)
    {
        frameBuffer = m_FrameBuffers.back();
        m_FrameBuffers.pop_back();
    }
    else
    {
        frameBuffer = std::make_shared<RHIVkFrameBuffer>(m_VkDevice, m_Instance->GetMaxFramesInFlight());
    }

    return frameBuffer;
}

void NebulaEngine::RHI::RHIVkDevice::ReleaseFrameBuffer(std::shared_ptr<FrameBuffer> frameBuffer)
{
    m_FrameBuffers.emplace_back(frameBuffer);
}

std::shared_ptr<NebulaEngine::RHI::BufferHandle> NebulaEngine::RHI::RHIVkDevice::GetBufferHandle()
{
    std::shared_ptr<BufferHandle> bufferHandle;
    if (m_BufferHandles.size() > 0)
    {
        bufferHandle = m_BufferHandles.back();
        m_BufferHandles.pop_back();
    }
    else
    {
        bufferHandle = std::make_shared<RHIVkBufferHandle>(this);
    }

    return bufferHandle;
}

void NebulaEngine::RHI::RHIVkDevice::ReleaseBufferHandle(std::shared_ptr<BufferHandle> bufferHandle)
{
   throw;
}


// TODO: move submit info to CommandBuffer

void NebulaEngine::RHI::RHIVkDevice::Submit(RHICommandBuffer* commandBuffer, u32 frameIndex)
{
    ASSERT(commandBuffer->ReadyForSubmit());

    auto rhiVkCommandBuffer = static_cast<RHIVkCommandBuffer*>(commandBuffer);
    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;
    
    if (rhiVkCommandBuffer->GetSignalSemaphoresCount() > 0)
    {
        submitInfo.waitSemaphoreCount = rhiVkCommandBuffer->GetWaitSemaphoresCount();
        submitInfo.pWaitSemaphores = rhiVkCommandBuffer->GetWaitSemaphores();
        submitInfo.pWaitDstStageMask = rhiVkCommandBuffer->GetWaitStageMask();
    }

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = (static_cast<VkCommandBuffer*>(commandBuffer->GetHandlerPointer()));

    // VkSemaphore signalSemaphores[] = { static_cast<VkSemaphore>(
    //         m_Surface->GetSwapChain()->GetRenderFinishSemaphore(frameIndex)->GetHandle()) };
    if (rhiVkCommandBuffer->GetSignalSemaphoresCount() > 0)
    {
        submitInfo.signalSemaphoreCount = rhiVkCommandBuffer->GetSignalSemaphoresCount();
        submitInfo.pSignalSemaphores = rhiVkCommandBuffer->GetSignalSemaphores();
    }

    if (vkQueueSubmit(m_VkGraphicQueue, 1, &submitInfo, rhiVkCommandBuffer->GetSubmissionFence()) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDevice::Submit]: failed to submit draw command buffer!");
    }
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkDevice::FindMemoryType(u32 typeFilter, u32 properties)
{
    for (uint32_t i = 0; i < m_VkPhysicalDeviceMemoryProperties.memoryTypeCount; ++i)
    {
        if ((typeFilter & (1 << i)) && (m_VkPhysicalDeviceMemoryProperties.memoryTypes[i].propertyFlags & properties) == properties)
        {
            return i;
        }
       
    }

    LOG_FATAL("[RHIVkDevice::FindMemoryType]: failed to find suitable memory type!");
    return -1;
}

void NebulaEngine::RHI::RHIVkDevice::SetResolution(u32 width, u32 height)
{
    m_Instance->UpdateSurfaceCapabilities(m_Surface);
    m_Surface->GetSwapChain()->SetResolution(width, height);
}

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    DeviceWaitIdle();
    delete m_GPUPipelineManager;
    delete m_DescriptorPool;
    m_FrameBuffers.clear();
    m_RenderPasses.clear();
    m_GPUPrograms.clear();
    m_CommandBufferPools.clear();
    m_BufferHandles.clear();
    vkDestroyDevice(m_VkDevice, nullptr);
    LOG_DEBUG("## Destroy Vulkan Device ##")
    m_Instance = nullptr;
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: ~RHIVkDevice");
}


