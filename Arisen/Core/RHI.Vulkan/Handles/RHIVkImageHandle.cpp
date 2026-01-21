#include "RHIVkImageHandle.h"
#include "../VkInitializer.h"
#include "../Memory/RHIVkImageView.h"
#include "Logger/Logger.h"
#include "RHI/Devices/RHIDevice.h"
#include "../Devices/RHIVkDevice.h"
#include "../Memory/RHIVkDeviceMemory.h"
#include "RHI/Utils/RHIResourceRegistry.h"

namespace ArisenEngine::RHI {
    struct DeferredVkBuffer {
        VkDevice device;
        VkBuffer buffer;
        ~DeferredVkBuffer() {
            if (device != VK_NULL_HANDLE && buffer != VK_NULL_HANDLE) {
                LOG_DEBUG("## vkDestroyBuffer called ##");
                vkDestroyBuffer(device, buffer, nullptr);
            }
        }
    };

    struct DeferredVkImage {
        VkDevice device;
        VkImage image;
        ~DeferredVkImage() {
            if (device != VK_NULL_HANDLE && image != VK_NULL_HANDLE) {
                vkDestroyImage(device, image, nullptr);
            }
        }
    };
}

ArisenEngine::RHI::RHIVkImageHandle::RHIVkImageHandle(RHIDevice* device):
ImageHandle(), m_Device(device), m_VKDevice(static_cast<VkDevice>(device->GetHandle()))
{
    
}

ArisenEngine::RHI::RHIVkImageHandle::RHIVkImageHandle(RHIDevice* device, VkImage image, ImageViewDesc desc):
ImageHandle(), m_Device(device), m_VKDevice(static_cast<VkDevice>(device->GetHandle())), m_VkImage(image)
{
    m_MemoryView = new RHIVkImageView(desc, m_VKDevice, image);
    m_NeedDestroy = false;
}

ArisenEngine::RHI::RHIVkImageHandle::~RHIVkImageHandle() noexcept
{
    LOG_DEBUG("[RHIVkImageHandle::~RHIVkImageHandle]: ~RHIVkImageHandle");
    if (m_NeedDestroy)
    {
        FreeHandle();
    }
}

void ArisenEngine::RHI::RHIVkImageHandle::AllocHandle(ImageDescriptor&& desc)
{
    FreeHandle();
    m_NeedDestroy = true;

    VkImageCreateInfo imageInfo = ImageCreateInfo(desc.imageType,
    desc.width, desc.height, desc.depth, desc.mipLevels, desc.arrayLayers,
    desc.format, desc.tiling, desc.imageLayout, desc.usage,
    desc.sampleCount, desc.sharingMode,
    desc.queueFamilyIndexCount,
    desc.pQueueFamilyIndices);

    if (vkCreateImage(m_VKDevice, &imageInfo, nullptr, &m_VkImage) != VK_SUCCESS)
    {
        LOG_FATAL_AND_THROW("[RHIVkImageHandle::AllocHandle] failed to create image!");
    }

    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
    auto* registry = vkDevice->GetResourceRegistry();

    auto* deferred = new DeferredVkImage{
        static_cast<VkDevice>(vkDevice->GetHandle()),
        m_VkImage
    };

    m_RHIHandle = registry->Create(MakeDeferredDeleteItem(deferred));
}

void ArisenEngine::RHI::RHIVkImageHandle::FreeHandle()
{
    if (m_VkImage == VK_NULL_HANDLE)
    {
        return;
    }
    
    auto* vkDevice = static_cast<RHIVkDevice*>(m_Device);
    auto* registry = vkDevice->GetResourceRegistry();

    registry->Release(m_RHIHandle, RHIQueueType::Graphics, vkDevice->GetCompletedSubmitId());

    m_VkImage = VK_NULL_HANDLE;
    m_RHIHandle = RHIResourceHandle::Invalid();
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkImageHandle::AddImageView(ImageViewDesc&& desc)
{
    if (m_MemoryView != nullptr)
    {
        delete m_MemoryView;
    }
    
    m_MemoryView = new RHIVkImageView(desc, m_VKDevice, m_VkImage);

    return 0;
}

bool ArisenEngine::RHI::RHIVkImageHandle::AllocDeviceMemory(UInt32 memoryPropertiesBits)
{
    ASSERT(m_VkImage != VK_NULL_HANDLE);
    
    if (m_DeviceMemory != nullptr)
    {
        m_DeviceMemory->FreeDeviceMemory();
    }
    else
    {
        m_DeviceMemory = new RHIVkDeviceMemory(m_Device, m_VkImage);
    }
    
    return m_DeviceMemory->AllocDeviceMemory(memoryPropertiesBits);
}
