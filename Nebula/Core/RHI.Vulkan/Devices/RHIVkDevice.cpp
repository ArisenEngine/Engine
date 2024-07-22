#include "RHIVkDevice.h"
#include "Logger/Logger.h"
#include "Windows/RenderWindowAPI.h"

NebulaEngine::RHI::RHIVkDevice::RHIVkDevice(Instance* instance, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device)
: Device(instance), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue), m_VkDevice(device)
{
    
}

NebulaEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    vkDestroyDevice(m_VkDevice, nullptr);
    LOG_DEBUG("## Destroy Vulkan Device ##")
    m_Instance = nullptr;
    LOG_INFO("[RHIVkDevice::~RHIVkDevice]: ~RHIVkDevice");
}


