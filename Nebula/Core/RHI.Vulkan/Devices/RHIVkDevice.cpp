#include "RHIVkDevice.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance* instance, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device)
: Device(instance), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device)
{
    
}

void NebulaEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
}

NebulaEngine::u32 NebulaEngine::RHI::RHIVkDevice::CreateGPUProgram()
{
    ASSERT(m_VkDevice != VK_NULL_HANDLE);
    u32 id = m_GPUPrograms.size();
    m_GPUPrograms.insert({id, std::make_unique<RHIVkGPUProgram>(m_VkDevice)});
    return id;
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

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    DeviceWaitIdle();
    m_GPUPrograms.clear();
    vkDestroyDevice(m_VkDevice, nullptr);
    LOG_DEBUG("## Destroy Vulkan Device ##")
    m_Instance = nullptr;
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: ~RHIVkDevice");
}


