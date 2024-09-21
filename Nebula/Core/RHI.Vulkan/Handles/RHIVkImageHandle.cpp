#include "RHIVkImageHandle.h"

#include "../Memory/RHIVkImageView.h"
#include "Logger/Logger.h"

NebulaEngine::RHI::RHIVkImageHandle::RHIVkImageHandle(VkDevice device):
ImageHandle(), m_VKDevice(device)
{
    
}

NebulaEngine::RHI::RHIVkImageHandle::RHIVkImageHandle(VkDevice device, VkImage image, ImageViewDesc desc):
ImageHandle(), m_VKDevice(device), m_VkImage(image)
{
    m_MemoryView = new RHIVkImageView(desc, device, image);
}

NebulaEngine::RHI::RHIVkImageHandle::~RHIVkImageHandle() noexcept
{
    LOG_DEBUG("[RHIVkImageHandle::~RHIVkImageHandle]: ~RHIVkImageHandle");
}
