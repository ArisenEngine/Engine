#include "RHIVkImageHandle.h"

#include "../Memory/RHIVkImageView.h"
#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkImageHandle::RHIVkImageHandle(VkDevice device):
ImageHandle(), m_VKDevice(device)
{
    
}

ArisenEngine::RHI::RHIVkImageHandle::RHIVkImageHandle(VkDevice device, VkImage image, ImageViewDesc desc):
ImageHandle(), m_VKDevice(device), m_VkImage(image)
{
    m_MemoryView = new RHIVkImageView(desc, device, image);
}

ArisenEngine::RHI::RHIVkImageHandle::~RHIVkImageHandle() noexcept
{
    LOG_DEBUG("[RHIVkImageHandle::~RHIVkImageHandle]: ~RHIVkImageHandle");
}
