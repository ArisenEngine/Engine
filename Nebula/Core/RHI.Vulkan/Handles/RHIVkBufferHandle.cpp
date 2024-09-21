#include "RHIVkBufferHandle.h"

NebulaEngine::RHI::RHIVkBufferHandle::RHIVkBufferHandle(VkDevice device)
: m_VKDevice(device)
{
    
}

NebulaEngine::RHI::RHIVkBufferHandle::~RHIVkBufferHandle() noexcept
{
    LOG_DEBUG("[RHIVkBufferHandle::~RHIVkBufferHandle]: ~RHIVkBufferHandle");
    
}

bool NebulaEngine::RHI::RHIVkBufferHandle::AllocBuffer(BufferAllocDesc && desc)
{
    VkBufferCreateInfo bufferInfo{};
    bufferInfo.sType = VK_STRUCTURE_TYPE_BUFFER_CREATE_INFO;
    bufferInfo.flags = desc.createFlagBits;
    bufferInfo.size = desc.size;

    return true;
}
