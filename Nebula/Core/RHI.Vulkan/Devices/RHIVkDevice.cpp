#include "RHIVkDevice.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device)
: Device(instance, surface), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device)
{
    m_GPUPipelineManager = new RHIVkGPUPipelineManager(this);
}

void NebulaEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
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
    m_CommandBufferPools.insert({id, std::make_unique<RHIVkCommandBufferPool>(this)});
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
        renderPass = std::make_shared<RHIVkGPURenderPass>(m_VkDevice);
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
        frameBuffer = std::make_shared<RHIVkFrameBuffer>(m_VkDevice);
    }

    return frameBuffer;
}

void NebulaEngine::RHI::RHIVkDevice::ReleaseFrameBuffer(std::shared_ptr<FrameBuffer> frameBuffer)
{
    m_FrameBuffers.emplace_back(frameBuffer);
}

void NebulaEngine::RHI::RHIVkDevice::Submit(RHICommandBuffer* commandBuffer)
{
    ASSERT(commandBuffer->ReadyForSubmit());
    
    VkSubmitInfo submitInfo{};
    submitInfo.sType = VK_STRUCTURE_TYPE_SUBMIT_INFO;

    VkSemaphore waitSemaphores[] = { static_cast<VkSemaphore>(
            m_Surface->GetSwapChain()->GetImageAvailableSemaphore()->GetHandle())
    };
    VkPipelineStageFlags waitStages[] = {VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT};
    submitInfo.waitSemaphoreCount = 1;
    submitInfo.pWaitSemaphores = waitSemaphores;
    submitInfo.pWaitDstStageMask = waitStages;

    submitInfo.commandBufferCount = 1;
    submitInfo.pCommandBuffers = (static_cast<VkCommandBuffer*>(commandBuffer->GetHandlerPointer()));

    VkSemaphore signalSemaphores[] = { static_cast<VkSemaphore>(
            m_Surface->GetSwapChain()->GetRenderFinishSemaphore()->GetHandle()) };
    submitInfo.signalSemaphoreCount = 1;
    submitInfo.pSignalSemaphores = signalSemaphores;

    if (vkQueueSubmit(m_VkGraphicQueue, 1, &submitInfo, static_cast<VkFence>(
                          commandBuffer->GetOwner()->GetFence()->GetHandle())) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkDevice::Submit]: failed to submit draw command buffer!");
    }
}

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    DeviceWaitIdle();
    delete m_GPUPipelineManager;
    m_FrameBuffers.clear();
    m_RenderPasses.clear();
    m_GPUPrograms.clear();
    m_CommandBufferPools.clear();
    vkDestroyDevice(m_VkDevice, nullptr);
    LOG_DEBUG("## Destroy Vulkan Device ##")
    m_Instance = nullptr;
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: ~RHIVkDevice");
}


