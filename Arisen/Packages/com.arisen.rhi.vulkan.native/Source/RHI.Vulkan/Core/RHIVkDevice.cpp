#include "Core/RHIVkDevice.h"

#include "Resources/RHIVkAccelerationStructure.h"
#include "RHI/Resources/RHIAccelerationStructure.h"
#include "Core/RHIVkFactory.h"
#include "Logger/Logger.h"
#include "Windowing/RenderWindowAPI.h"
#include "Services/RHIVkTransferManager.h"
#include "Utils/RHIVkDeferredDeletion.h"
#include "Queues/RHIVkQueue.h"
#include "Handles/RHIVkResourcePools.h"
#include "Allocation/RHIVkMemoryAllocator.h"
#include "Core/RHIVkInstance.h"
#include "Utils/RHIVkInitializer.h"
#include "Descriptors/RHIVkBindlessManager.h"
#include "RenderPass/RHIVkGPURenderPass.h"
#include "Commands/RHIVkCommandBuffer.h"
#include "Pipeline/RHIVkGPUPipeline.h"
#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "Commands/RHIVkCommandBuffer.h"
#include "../../Core.RHI/RHI/Core/RHIInspector.h"
#include "Descriptors/RHIVkDescriptorHeap.h"
#include "Descriptors/RHIVkBindlessDescriptorTable.h"
#include "Profiler.h"

using namespace ArisenEngine::RHI;

#if ARISEN_RHI__RESOURCE_INSPECTOR
#define RHI_STATS_PTR(x) (&(x))
#else
    #define RHI_STATS_PTR(x) (nullptr)
#endif


ArisenEngine::RHI::RHIVkDevice::RHIVkDevice(RHIInstance* instance, RHISurface* surface, VkQueue graphicQueue,
                                            VkQueue presentQueue, VkQueue computeQueue, VkQueue transferQueue,
                                            VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties,
                                            UInt32 graphicsFamilyIndex, UInt32 computeFamilyIndex, UInt32 transferFamilyIndex,
                                            UInt32 presentFamilyIndex)
    : RHIDevice(instance, surface), m_VkGraphicQueue(graphicQueue), m_VkPresentQueue(presentQueue),
      m_VkComputeQueue(computeQueue), m_VkTransferQueue(transferQueue), m_VkDevice(device),
      m_GraphicsFamilyIndex(graphicsFamilyIndex), m_ComputeFamilyIndex(computeFamilyIndex),
      m_TransferFamilyIndex(transferFamilyIndex), m_PresentFamilyIndex(presentFamilyIndex),
      m_VkPhysicalDeviceMemoryProperties(memoryProperties)
{
    std::cout << "[DEBUG] RHIVkDevice::RHIVkDevice START" << std::endl;
    m_GPUPipelineManager = new RHIVkGPUPipelineManager(this, m_Instance->GetMaxFramesInFlight());
    m_DescriptorPool = new RHIVkDescriptorPool(this);

    // Cache function pointers for Sync 2.0 and Dynamic Rendering
    vkCmdPipelineBarrier2KHR = (PFN_vkCmdPipelineBarrier2KHR)
        vkGetDeviceProcAddr(m_VkDevice, "vkCmdPipelineBarrier2KHR");
    vkCmdBeginRenderingKHR = (PFN_vkCmdBeginRenderingKHR)vkGetDeviceProcAddr(m_VkDevice, "vkCmdBeginRenderingKHR");
    vkCmdEndRenderingKHR = (PFN_vkCmdEndRenderingKHR)vkGetDeviceProcAddr(m_VkDevice, "vkCmdEndRenderingKHR");
    vkCmdDrawMeshTasksEXT = (PFN_vkCmdDrawMeshTasksEXT)vkGetDeviceProcAddr(m_VkDevice, "vkCmdDrawMeshTasksEXT");

    // Debug Utils
    vkSetDebugUtilsObjectNameEXT = (PFN_vkSetDebugUtilsObjectNameEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkSetDebugUtilsObjectNameEXT");
    vkCmdBeginDebugUtilsLabelEXT = (PFN_vkCmdBeginDebugUtilsLabelEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdBeginDebugUtilsLabelEXT");
    vkCmdEndDebugUtilsLabelEXT = (PFN_vkCmdEndDebugUtilsLabelEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdEndDebugUtilsLabelEXT");
    vkCmdInsertDebugUtilsLabelEXT = (PFN_vkCmdInsertDebugUtilsLabelEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdInsertDebugUtilsLabelEXT");

    // RT Function Pointers
    vkCreateAccelerationStructureKHR = (PFN_vkCreateAccelerationStructureKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkCreateAccelerationStructureKHR");
    vkDestroyAccelerationStructureKHR = (PFN_vkDestroyAccelerationStructureKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkDestroyAccelerationStructureKHR");
    vkGetAccelerationStructureBuildSizesKHR = (PFN_vkGetAccelerationStructureBuildSizesKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkGetAccelerationStructureBuildSizesKHR");
    vkGetAccelerationStructureDeviceAddressKHR = (PFN_vkGetAccelerationStructureDeviceAddressKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkGetAccelerationStructureDeviceAddressKHR");
    vkGetBufferDeviceAddressKHR = (PFN_vkGetBufferDeviceAddressKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkGetBufferDeviceAddressKHR");
    if (vkGetBufferDeviceAddressKHR == nullptr)
    {
        vkGetBufferDeviceAddressKHR = (PFN_vkGetBufferDeviceAddressKHR)vkGetDeviceProcAddr(
            m_VkDevice, "vkGetBufferDeviceAddress");
    }
    vkCmdBuildAccelerationStructuresKHR = (PFN_vkCmdBuildAccelerationStructuresKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdBuildAccelerationStructuresKHR");
    vkCmdTraceRaysKHR = (PFN_vkCmdTraceRaysKHR)vkGetDeviceProcAddr(m_VkDevice, "vkCmdTraceRaysKHR");
    vkCreateRayTracingPipelinesKHR = (PFN_vkCreateRayTracingPipelinesKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkCreateRayTracingPipelinesKHR");
    vkGetRayTracingShaderGroupHandlesKHR = (PFN_vkGetRayTracingShaderGroupHandlesKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkGetRayTracingShaderGroupHandlesKHR");

    // Descriptor Buffers
    if (m_Capabilities.supportDescriptorBuffer)
    {
        vkGetDescriptorSetLayoutSizeEXT = (PFN_vkGetDescriptorSetLayoutSizeEXT)vkGetDeviceProcAddr(m_VkDevice, "vkGetDescriptorSetLayoutSizeEXT");
        vkGetDescriptorSetLayoutBindingOffsetEXT = (PFN_vkGetDescriptorSetLayoutBindingOffsetEXT)vkGetDeviceProcAddr(m_VkDevice, "vkGetDescriptorSetLayoutBindingOffsetEXT");
        vkGetDescriptorEXT = (PFN_vkGetDescriptorEXT)vkGetDeviceProcAddr(m_VkDevice, "vkGetDescriptorEXT");
        vkCmdBindDescriptorBuffersEXT = (PFN_vkCmdBindDescriptorBuffersEXT)vkGetDeviceProcAddr(m_VkDevice, "vkCmdBindDescriptorBuffersEXT");
        vkCmdSetDescriptorBufferOffsetsEXT = (PFN_vkCmdSetDescriptorBufferOffsetsEXT)vkGetDeviceProcAddr(m_VkDevice, "vkCmdSetDescriptorBufferOffsetsEXT");
    }

    // VRS
    // VRS
    vkCmdSetFragmentShadingRateKHR = (PFN_vkCmdSetFragmentShadingRateKHR)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdSetFragmentShadingRateKHR");

    // Dynamic State
    vkCmdSetCullModeEXT = (PFN_vkCmdSetCullModeEXT)vkGetDeviceProcAddr(m_VkDevice, "vkCmdSetCullModeEXT");
    vkCmdSetFrontFaceEXT = (PFN_vkCmdSetFrontFaceEXT)vkGetDeviceProcAddr(m_VkDevice, "vkCmdSetFrontFaceEXT");
    vkCmdSetPrimitiveTopologyEXT = (PFN_vkCmdSetPrimitiveTopologyEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdSetPrimitiveTopologyEXT");
    vkCmdSetDepthTestEnableEXT = (PFN_vkCmdSetDepthTestEnableEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdSetDepthTestEnableEXT");
    vkCmdSetDepthWriteEnableEXT = (PFN_vkCmdSetDepthWriteEnableEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdSetDepthWriteEnableEXT");
    vkCmdSetDepthCompareOpEXT = (PFN_vkCmdSetDepthCompareOpEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdSetDepthCompareOpEXT");
    vkCmdSetStencilTestEnableEXT = (PFN_vkCmdSetStencilTestEnableEXT)vkGetDeviceProcAddr(
        m_VkDevice, "vkCmdSetStencilTestEnableEXT");
    vkCmdSetStencilOpEXT = (PFN_vkCmdSetStencilOpEXT)vkGetDeviceProcAddr(m_VkDevice, "vkCmdSetStencilOpEXT");

    auto* vkInstance = static_cast<RHIVkInstance*>(m_Instance);
    m_MemoryAllocator = new RHIVkMemoryAllocator(this, vkInstance->GetVkInstance(), vkInstance->GetPhysicalDevice(),
                                                 m_VkDevice, VK_API_VERSION_1_2,
                                                 RHI_STATS_PTR(m_Stats.totalVideoMemoryAllocated));


    m_BindlessManager = new RHIVkBindlessManager(this);
    m_BindlessManager->Initialize();

    m_Factory = new RHIVkFactory(this);
    m_DeferredDeletion = std::make_unique<RHIVkDeferredDeletion>(m_Instance->GetMaxFramesInFlight());
    m_ResourceRegistry = std::make_unique<RHIResourceRegistry>(m_DeferredDeletion.get());

    m_BufferPool = std::make_unique<RHIResourcePool<RHIBufferHandle, RHIVkBufferPoolItem>>(
        RHI_STATS_PTR(m_Stats.bufferCount));
    m_ImagePool = std::make_unique<RHIResourcePool<RHIImageHandle, RHIVkImagePoolItem>>(
        RHI_STATS_PTR(m_Stats.imageCount));
    m_ImageViewPool = std::make_unique<RHIResourcePool<RHIImageViewHandle, RHIVkImageViewPoolItem>>(
        RHI_STATS_PTR(m_Stats.imageViewCount));
    m_SamplerPool = std::make_unique<RHIResourcePool<RHISamplerHandle, RHIVkSamplerPoolItem>>(
        RHI_STATS_PTR(m_Stats.samplerCount));
    m_RenderPassPool = std::make_unique<RHIResourcePool<RHIRenderPassHandle, RHIVkRenderPassPoolItem>>(
        RHI_STATS_PTR(m_Stats.renderPassCount));
    m_FrameBufferPool = std::make_unique<RHIResourcePool<RHIFrameBufferHandle, RHIVkFrameBufferPoolItem>>(
        RHI_STATS_PTR(m_Stats.frameBufferCount));
    m_SemaphorePool = std::make_unique<RHIResourcePool<RHISemaphoreHandle, RHIVkSemaphorePoolItem>>(
        RHI_STATS_PTR(m_Stats.synchronizationCount));
    m_PipelinePool = std::make_unique<RHIResourcePool<RHIPipelineHandle, RHIVkPipelinePoolItem>>(
        RHI_STATS_PTR(m_Stats.pipelineCount));
    m_GPUProgramPool = std::make_unique<RHIResourcePool<RHIShaderProgramHandle, RHIVkGPUProgramPoolItem>>(
        RHI_STATS_PTR(m_Stats.shaderProgramCount));
    m_CommandBufferPoolPool = std::make_unique<RHIResourcePool<
        RHICommandBufferPoolHandle, RHIVkCommandBufferPoolItem>>();
    m_CommandBufferPool = std::make_unique<RHIResourcePool<RHICommandBufferHandle, RHIVkCommandBufferItem>>(
        RHI_STATS_PTR(m_Stats.commandBufferCount));
    m_AccelerationStructurePool = std::make_unique<RHIResourcePool<
        RHIAccelerationStructureHandle, RHIVkAccelerationStructurePoolItem>>();
    m_DescriptorPoolPool = std::make_unique<RHIResourcePool<RHIDescriptorPoolHandle, RHIVkDescriptorPoolPoolItem>>();

    // Register default descriptor pool
    m_DescriptorPoolHandle = m_DescriptorPoolPool->Allocate([&](auto* item)
    {
        item->pool = m_DescriptorPool;
        item->name = "DefaultDescriptorPool";
    });
    m_MemoryPoolPool = std::make_unique<RHIResourcePool<RHIMemoryPoolHandle, RHIVkMemoryPoolPoolItem>>();
    m_GraphicsQueue = std::make_unique<RHIVkQueue>(this, m_VkDevice, m_VkGraphicQueue, RHIQueueType::Graphics,
                                                   m_DeferredDeletion.get(), m_ResourceRegistry.get());

    if (m_VkComputeQueue != VK_NULL_HANDLE)
    {
        m_ComputeQueue = std::make_unique<RHIVkQueue>(this, m_VkDevice, m_VkComputeQueue, RHIQueueType::Compute,
                                                      m_DeferredDeletion.get(), m_ResourceRegistry.get());
    }

    if (m_VkTransferQueue != VK_NULL_HANDLE)
    {
        m_TransferQueue = std::make_unique<RHIVkQueue>(this, m_VkDevice, m_VkTransferQueue, RHIQueueType::Transfer,
                                                       m_DeferredDeletion.get(), m_ResourceRegistry.get());
    }

    if (m_VkPresentQueue != VK_NULL_HANDLE)
    {
        m_PresentQueue = std::make_unique<RHIVkQueue>(this, m_VkDevice, m_VkPresentQueue, RHIQueueType::Present,
                                                      m_DeferredDeletion.get(), m_ResourceRegistry.get());
    }

    const UInt32 maxFramesInFlight = m_Instance->GetMaxFramesInFlight();
    m_FrameSync = std::make_unique<FrameSyncTracker>(maxFramesInFlight);

    // Initialize TransferManager after queues and allocator are ready
    m_TransferManager = std::make_unique<RHIVkTransferManager>(this);

    std::cout << "[DEBUG] RHIVkDevice::RHIVkDevice END" << std::endl;
}

#undef RHI_STATS_PTR


ArisenEngine::RHI::RHIFactory* ArisenEngine::RHI::RHIVkDevice::GetFactory() const
{
    return m_Factory;
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::GetMaxFramesInFlight() const
{
    return m_Instance->GetMaxFramesInFlight();
}

ArisenEngine::RHI::RHIMemoryAllocator* ArisenEngine::RHI::RHIVkDevice::GetMemoryAllocator() const
{
    return m_MemoryAllocator;
}

void ArisenEngine::RHI::RHIVkDevice::DeviceWaitIdle() const
{
    vkDeviceWaitIdle(m_VkDevice);
}
void ArisenEngine::RHI::RHIVkDevice::GraphicQueueWaitIdle() const
{
    if (m_GraphicsQueue) m_GraphicsQueue->WaitIdle();
}
void ArisenEngine::RHI::RHIVkDevice::ComputeQueueWaitIdle() const
{
    if (m_ComputeQueue) m_ComputeQueue->WaitIdle();
}
void ArisenEngine::RHI::RHIVkDevice::TransferQueueWaitIdle() const
{
    if (m_TransferQueue) m_TransferQueue->WaitIdle();
}
void ArisenEngine::RHI::RHIVkDevice::PresentQueueWaitIdle() const
{
    if (m_PresentQueue) m_PresentQueue->WaitIdle();
}
void ArisenEngine::RHI::RHIVkDevice::QueueWaitIdle(RHIQueueType type) const
{
    switch (type)
    {
    case RHIQueueType::Graphics:
        GraphicQueueWaitIdle();
        break;
    case RHIQueueType::Compute:
        ComputeQueueWaitIdle();
        break;
    case RHIQueueType::Transfer:
        TransferQueueWaitIdle();
        break;
    case RHIQueueType::Present:
        PresentQueueWaitIdle();
        break;
    default:
        DeviceWaitIdle();
        break;
    }
}


void ArisenEngine::RHI::RHIVkDevice::DeferredDelete(const RHIDeletionDependencies& deps,
                                                   RHIDeferredDeleteItem item)
{
    EnqueueDeferredDestroy(deps, item);
}

void ArisenEngine::RHI::RHIVkDevice::EnqueueDeferredDestroy(const RHIDeletionDependencies& deps,
                                                           RHIDeferredDeleteItem item)
{
    if (m_DeferredDeletion)
    {
        m_DeferredDeletion->Enqueue(deps, item);
    }
}

namespace
{
    struct DeferredCallItem
    {
        std::function<void()> fn;
    };

    static void DeferredCallDeleter(void* p)
    {
        auto* item = static_cast<DeferredCallItem*>(p);
        if (item && item->fn) item->fn();
        delete item;
    }
}

void ArisenEngine::RHI::RHIVkDevice::EnqueueDeferredDestroy(const RHIDeletionDependencies& deps,
                                                           std::function<void()>&& fn)
{
    auto* item = new DeferredCallItem{std::move(fn)};
    EnqueueDeferredDestroy(deps, RHIDeferredDeleteItem{item, &DeferredCallDeleter});
}



RHIQueue* ArisenEngine::RHI::RHIVkDevice::GetQueue(RHIQueueType type)
{
    if (type == RHIQueueType::Graphics)
    {
        return m_GraphicsQueue.get();
    }
    else if (type == RHIQueueType::Compute)
    {
        return m_ComputeQueue.get();
    }
    else if (type == RHIQueueType::Transfer)
    {
        return m_TransferQueue.get();
    }
    else if (type == RHIQueueType::Present)
    {
        return m_PresentQueue.get();
    }
    return nullptr;
}

RHIQueue* ArisenEngine::RHI::RHIVkDevice::GetQueueByFamilyIndex(UInt32 familyIndex)
{
    if (familyIndex == m_GraphicsFamilyIndex) return m_GraphicsQueue.get();
    if (familyIndex == m_ComputeFamilyIndex) return m_ComputeQueue.get();
    if (familyIndex == m_TransferFamilyIndex) return m_TransferQueue.get();
    if (familyIndex == m_PresentFamilyIndex) return m_PresentQueue.get();
    return nullptr;
}
RHICommandBufferPool* ArisenEngine::RHI::RHIVkDevice::GetCommandBufferPool(RHICommandBufferPoolHandle handle)
{
    auto* item = m_CommandBufferPoolPool->Get(handle);
    return item ? item->pool : nullptr;
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::FindMemoryType(UInt32 typeFilter, UInt32 properties)
{
    for (uint32_t i = 0; i < m_VkPhysicalDeviceMemoryProperties.memoryTypeCount; ++i)
    {
        if ((typeFilter & (1 << i)) && (m_VkPhysicalDeviceMemoryProperties.memoryTypes[i].propertyFlags & properties) ==
            properties)
        {
            return i;
        }
    }

    LOG_FATAL("[RHIVkDevice::FindMemoryType]: failed to find suitable memory type!");
    return -1;
}

void ArisenEngine::RHI::RHIVkDevice::SetResolution(UInt32 width, UInt32 height)
{
    m_Instance->UpdateSurfaceCapabilities(m_Surface);
    m_Surface->GetSwapChain()->SetResolution(width, height);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::RegisterBindlessResource(RHIImageViewHandle image)
{
    return m_BindlessManager->RegisterImage(image);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::RegisterBindlessResource(RHIBufferHandle buffer)
{
    return m_BindlessManager->RegisterBuffer(buffer);
}

ArisenEngine::UInt32 ArisenEngine::RHI::RHIVkDevice::RegisterBindlessResource(RHISamplerHandle sampler)
{
    return m_BindlessManager->RegisterSampler(sampler);
}

void ArisenEngine::RHI::RHIVkDevice::SetObjectName(ERHIObjectType type, UInt64 handle, const char* name)
{
    if (vkSetDebugUtilsObjectNameEXT == nullptr) return;

    VkDebugUtilsObjectNameInfoEXT nameInfo{};
    nameInfo.sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_OBJECT_NAME_INFO_EXT;
    nameInfo.pObjectName = name;
    nameInfo.objectHandle = handle;

    switch (type)
    {
    case ERHIObjectType::Buffer:
        {
            auto h = *reinterpret_cast<RHIBufferHandle*>(&handle);
            auto* item = m_BufferPool->Get(h);
            if (item)
            {
                nameInfo.objectHandle = (UInt64)item->buffer;
                nameInfo.objectType = VK_OBJECT_TYPE_BUFFER;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::Image:
        {
            auto h = *reinterpret_cast<RHIImageHandle*>(&handle);
            auto* item = m_ImagePool->Get(h);
            if (item)
            {
                nameInfo.objectHandle = (UInt64)item->image;
                nameInfo.objectType = VK_OBJECT_TYPE_IMAGE;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::ImageView:
        {
            auto h = *reinterpret_cast<RHIImageViewHandle*>(&handle);
            auto* item = m_ImageViewPool->Get(h);
            if (item)
            {
                nameInfo.objectHandle = (UInt64)item->view;
                nameInfo.objectType = VK_OBJECT_TYPE_IMAGE_VIEW;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::Sampler:
        {
            auto h = *reinterpret_cast<RHISamplerHandle*>(&handle);
            auto* item = m_SamplerPool->Get(h);
            if (item)
            {
                nameInfo.objectHandle = (UInt64)item->sampler;
                nameInfo.objectType = VK_OBJECT_TYPE_SAMPLER;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::RenderPass:
        {
            auto h = *reinterpret_cast<RHIRenderPassHandle*>(&handle);
            auto* item = m_RenderPassPool->Get(h);
            if (item)
            {
                nameInfo.objectHandle = (UInt64)item->renderPass;
                nameInfo.objectType = VK_OBJECT_TYPE_RENDER_PASS;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::FrameBuffer:
        {
            auto h = *reinterpret_cast<RHIFrameBufferHandle*>(&handle);
            auto* item = m_FrameBufferPool->Get(h);
            if (item)
            {
                nameInfo.objectHandle = (UInt64)item->framebuffer;
                nameInfo.objectType = VK_OBJECT_TYPE_FRAMEBUFFER;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::Semaphore: nameInfo.objectType = VK_OBJECT_TYPE_SEMAPHORE;
        break;
    case ERHIObjectType::Fence: nameInfo.objectType = VK_OBJECT_TYPE_FENCE;
        break;
    case ERHIObjectType::GPUPipeline:
        {
            auto h = *reinterpret_cast<RHIPipelineHandle*>(&handle);
            auto* item = m_PipelinePool->Get(h);
            if (item)
            {
                // In RHIVkGPUPipeline, there are multiple pipelines per frame, but we can name the base one or others
                // For simplicity, we just use the first available or provided handle
                nameInfo.objectHandle = handle; // Fallback if handle is already a raw handle
                nameInfo.objectType = VK_OBJECT_TYPE_PIPELINE;
                item->name = name;
            }
        }
        break;
    case ERHIObjectType::GPUProgram: nameInfo.objectType = VK_OBJECT_TYPE_SHADER_MODULE;
        break;
    case ERHIObjectType::CommandBuffer:
        {
            auto* c = reinterpret_cast<RHIVkCommandBuffer*>(handle);
            if (c)
            {
                nameInfo.objectHandle = (UInt64)reinterpret_cast<uintptr_t>(c->GetHandle());
                nameInfo.objectType = VK_OBJECT_TYPE_COMMAND_BUFFER;
            }
        }
        break;
    case ERHIObjectType::CommandBufferPool: nameInfo.objectType = VK_OBJECT_TYPE_COMMAND_POOL;
        break;
    case ERHIObjectType::DescriptorPool: nameInfo.objectType = VK_OBJECT_TYPE_DESCRIPTOR_POOL;
        break;
    case ERHIObjectType::DescriptorSet: nameInfo.objectType = VK_OBJECT_TYPE_DESCRIPTOR_SET;
        break;
    default: nameInfo.objectType = VK_OBJECT_TYPE_UNKNOWN;
        break;
    }

    if (nameInfo.objectType != VK_OBJECT_TYPE_UNKNOWN)
    {
        vkSetDebugUtilsObjectNameEXT(m_VkDevice, &nameInfo);
    }
}

// --- Handle-based Buffer Operations ---

bool ArisenEngine::RHI::RHIVkDevice::AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc)
{
    ARISEN_PROFILE_ZONE("Vk::AllocBuffer");
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer) return false;

    auto bufferInfo = BufferCreateInfo(
        desc.createFlagBits,
        desc.size,
        desc.usage,
        desc.sharingMode,
        desc.queueFamilyIndexCount,
        (const uint32_t*)desc.pQueueFamilyIndices);

    buffer->size = desc.size;
    buffer->range = desc.size;

    if (vkCreateBuffer(m_VkDevice, &bufferInfo, nullptr, &buffer->buffer) != VK_SUCCESS)
    {
        std::cout << "AllocBuffer FAILED to create buffer! Size: " << desc.size << " Usage: " << (int)desc.usage <<
            std::endl;
        LOG_ERRORF("[RHIVkDevice::AllocBuffer]: failed to create buffer! Size: {0}, Usage: {1}, Sharing: {2}",
                   (UInt64)desc.size, (UInt32)desc.usage, (UInt32)desc.sharingMode);
        return false;
    }

    // Register for deferred deletion using a shared state object
    buffer->state = new RHIVkBufferState();
    buffer->state->device = m_VkDevice;
    buffer->state->buffer = buffer->buffer;
    buffer->state->allocator = m_MemoryAllocator->GetVmaAllocator();
    buffer->memoryUsage = desc.memoryUsage;
    buffer->usage = desc.usage;
    buffer->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(buffer->state));

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocBufferDeviceMemory(RHIBufferHandle handle)
{
    ARISEN_PROFILE_ZONE("Vk::AllocBufferMemory");
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->buffer == VK_NULL_HANDLE || !buffer->state) return false;

    // NOTE: We use explicit VMA_MEMORY_USAGE_* flags (like GPU_ONLY) instead of VMA_MEMORY_USAGE_AUTO*
    // because VMA_MEMORY_USAGE_AUTO* requires additional alignment/usage information when used with 
    // low-level allocation functions like vmaAllocateMemoryForBuffer, which can trigger assertions 
    // if not provided correctly. Explicit flags are safer for manual allocations.
    VmaMemoryUsage usage = VMA_MEMORY_USAGE_GPU_ONLY;
    // Map ERHIMemoryUsage to VMA usage
    switch (buffer->memoryUsage)
    {
    case ERHIMemoryUsage::GpuOnly:
        usage = VMA_MEMORY_USAGE_GPU_ONLY;
        break;
    case ERHIMemoryUsage::Upload:
        usage = VMA_MEMORY_USAGE_CPU_TO_GPU;
        break;
    case ERHIMemoryUsage::Readback:
        usage = VMA_MEMORY_USAGE_GPU_TO_CPU;
        break;
    case ERHIMemoryUsage::Transient:
        usage = VMA_MEMORY_USAGE_CPU_TO_GPU;
        break;
    }

    VmaAllocation newAlloc = VK_NULL_HANDLE;

    if (!m_MemoryAllocator->AllocateBufferMemory(buffer->buffer, usage, &newAlloc))
    {
        std::cout << "AllocBufferDeviceMemory FAILED for buffer " << handle.index << " Usage: " << (int)buffer->
            memoryUsage << std::endl;
        LOG_ERRORF("[RHIVkDevice::AllocBufferDeviceMemory]: Failed to allocate memory for buffer {0}. Usage: {1}",
                   (UInt64)handle.index, (int)buffer->memoryUsage);
        return false;
    }

    // If there was an old allocation, queue it for individual deletion
    if (buffer->state->allocation != VK_NULL_HANDLE)
    {
        auto deps = m_ResourceRegistry->GetTickets(buffer->registryHandle);
        EnqueueDeferredDestroy(deps,
                               [allocator = buffer->state->allocator, oldAlloc = buffer->state->allocation]()
                               {
                                   if (allocator != VK_NULL_HANDLE && oldAlloc != VK_NULL_HANDLE)
                                   {
                                       vmaFreeMemory(allocator, oldAlloc);
                                   }
                               });
    }

    buffer->state->allocation = newAlloc;
    buffer->allocation = newAlloc; // Sync cache

    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeBufferInternal(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer) return;

    if (buffer->buffer != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(buffer->registryHandle);

        buffer->buffer = VK_NULL_HANDLE;
        buffer->allocation = VK_NULL_HANDLE;
        buffer->state = nullptr;
        buffer->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseBuffer(RHIBufferHandle handle)
{
    FreeBufferInternal(handle);
    if (!m_BufferPool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseBuffer]: Failed to deallocate handle (invalid or stale)!");
    }
}

// Optimized synchronization: uses TransferManager for batched, ring-buffer-backed staging.
void ArisenEngine::RHI::RHIVkDevice::BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size,
                                                      UInt64 offset)
{
    ARISEN_PROFILE_ZONE("Vk::BufferMemoryCopy");
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->allocation == VK_NULL_HANDLE) return;

    // Check if the memory is host visible
    VmaAllocationInfo allocInfo;
    vmaGetAllocationInfo(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation, &allocInfo);
    VkMemoryPropertyFlags memFlags = m_VkPhysicalDeviceMemoryProperties.memoryTypes[allocInfo.memoryType].propertyFlags;

    if (memFlags & VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT)
    {
        // Direct mapping possible
        void* mappedData;
        if (vmaMapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation, &mappedData) == VK_SUCCESS)
        {
            memcpy((uint8_t*)mappedData + offset, src, size);

            // Flush for non-coherent memory to ensure GPU visibility
            if (!(memFlags & VK_MEMORY_PROPERTY_HOST_COHERENT_BIT))
            {
                vmaFlushAllocation(m_MemoryAllocator->GetVmaAllocator(),
                                   buffer->allocation, offset, size);
            }

            vmaUnmapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation);
        }
    }
    else
    {
        // Device-local: use TransferManager (ring-buffer staging + batched submit)
        m_TransferManager->EnqueueBufferCopy(buffer->buffer, src, size, offset, m_GraphicsFamilyIndex);
        RHIGpuTicket ticket = m_TransferManager->Flush();
        if (ticket > 0)
        {
            m_TransferManager->WaitForTicket(ticket);
        }
    }
}

RHIGpuTicket ArisenEngine::RHI::RHIVkDevice::BufferMemoryCopyAsync(RHIBufferHandle handle, const void* src,
                                                                    UInt64 size, UInt64 offset)
{
    ARISEN_PROFILE_ZONE("Vk::BufferMemoryCopyAsync");
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->allocation == VK_NULL_HANDLE) return 0;

    // Check if the memory is host visible
    VmaAllocationInfo allocInfo;
    vmaGetAllocationInfo(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation, &allocInfo);
    VkMemoryPropertyFlags memFlags = m_VkPhysicalDeviceMemoryProperties.memoryTypes[allocInfo.memoryType].propertyFlags;

    if (memFlags & VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT)
    {
        // Direct mapping — inherently synchronous, no ticket needed
        void* mappedData;
        if (vmaMapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation, &mappedData) == VK_SUCCESS)
        {
            memcpy((uint8_t*)mappedData + offset, src, size);

            if (!(memFlags & VK_MEMORY_PROPERTY_HOST_COHERENT_BIT))
            {
                vmaFlushAllocation(m_MemoryAllocator->GetVmaAllocator(),
                                   buffer->allocation, offset, size);
            }

            vmaUnmapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation);
        }
        return 0; // No GPU work needed
    }
    else
    {
        // Device-local: enqueue via TransferManager and flush (non-blocking return)
        // Issue ownership transfer to the immediate Graphics Queue for usage
        m_TransferManager->EnqueueBufferCopy(buffer->buffer, src, size, offset, m_GraphicsFamilyIndex);
        return m_TransferManager->Flush();
    }
}

RHIGpuTicket ArisenEngine::RHI::RHIVkDevice::FlushTransfers()
{
    return m_TransferManager->Flush();
}

void ArisenEngine::RHI::RHIVkDevice::UpdateTransfers()
{
    m_TransferManager->Update();
}

void* ArisenEngine::RHI::RHIVkDevice::MapBuffer(RHIBufferHandle handle)
{
    ARISEN_PROFILE_ZONE("Vk::MapBuffer");
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->allocation == VK_NULL_HANDLE) return nullptr;

    void* mappedData = nullptr;
    if (vmaMapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation, &mappedData) == VK_SUCCESS)
    {
        return mappedData;
    }
    return nullptr;
}

void ArisenEngine::RHI::RHIVkDevice::UnmapBuffer(RHIBufferHandle handle)
{
    ARISEN_PROFILE_ZONE("Vk::UnmapBuffer");
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer || buffer->allocation == VK_NULL_HANDLE) return;

    vmaUnmapMemory(m_MemoryAllocator->GetVmaAllocator(), buffer->allocation);
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferSize(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    return buffer ? buffer->size : 0ULL;
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferOffset(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    return buffer ? buffer->offset : 0ULL;
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferRange(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    return buffer ? buffer->range : 0ULL;
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetBufferDeviceAddress(RHIBufferHandle handle)
{
    auto* buffer = m_BufferPool->Get(handle);
    if (!buffer)
    {
        LOG_ERRORF("[RHIVkDevice::GetBufferDeviceAddress]: Invalid buffer handle {0}", (UInt64)handle.index);
        return 0ULL;
    }
    if (buffer->buffer == VK_NULL_HANDLE)
    {
        LOG_ERRORF("[RHIVkDevice::GetBufferDeviceAddress]: Buffer {0} has VK_NULL_HANDLE", (UInt64)handle.index);
        return 0ULL;
    }

    if (!vkGetBufferDeviceAddressKHR) return 0ULL;

    VkBufferDeviceAddressInfoKHR addressInfo{};
    addressInfo.sType = VK_STRUCTURE_TYPE_BUFFER_DEVICE_ADDRESS_INFO_KHR;
    addressInfo.buffer = buffer->buffer;
    UInt64 addr = vkGetBufferDeviceAddressKHR(m_VkDevice, &addressInfo);
    if (addr == 0)
    {
        LOG_ERRORF("[RHIVkDevice::GetBufferDeviceAddress]: Returned 0 for buffer {0}", (UInt64)handle.index);
    }
    return addr;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc)
{
    ARISEN_PROFILE_ZONE("Vk::AllocImage");
    auto* image = m_ImagePool->Get(handle);
    if (!image) return false;

    auto imageInfo = ImageCreateInfo(
        desc.imageType,
        desc.width, desc.height, desc.depth,
        desc.mipLevels, desc.arrayLayers,
        desc.format, desc.tiling,
        desc.imageLayout, desc.usage,
        desc.sampleCount, desc.sharingMode,
        desc.queueFamilyIndexCount,
        (const uint32_t*)desc.pQueueFamilyIndices);

    if (vkCreateImage(m_VkDevice, &imageInfo, nullptr, &image->image) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocImage]: failed to create image!");
        return false;
    }

    image->width = desc.width;
    image->height = desc.height;
    image->mipLevels = desc.mipLevels;
    image->currentLayout = static_cast<VkImageLayout>(desc.imageLayout);
    image->needDestroy = true;

    // Register for deferred deletion using a shared state object
    image->state = new RHIVkImageState();
    image->state->device = m_VkDevice;
    image->state->image = image->image;
    image->state->allocator = m_MemoryAllocator->GetVmaAllocator();
    image->memoryUsage = desc.memoryUsage;

    image->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(image->state));

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageDeviceMemory(RHIImageHandle handle)
{
    ARISEN_PROFILE_ZONE("Vk::AllocImageMemory");
    auto* image = m_ImagePool->Get(handle);
    if (!image || image->image == VK_NULL_HANDLE || !image->state) return false;

    // NOTE: Use explicit VMA_MEMORY_USAGE_* flags to avoid assertions in manual allocation paths.
    VmaMemoryUsage usage = VMA_MEMORY_USAGE_GPU_ONLY;
    // Map ERHIMemoryUsage to VMA usage
    switch (image->memoryUsage)
    {
    case ERHIMemoryUsage::GpuOnly:
        usage = VMA_MEMORY_USAGE_GPU_ONLY;
        break;
    case ERHIMemoryUsage::Upload:
        usage = VMA_MEMORY_USAGE_CPU_TO_GPU;
        break;
    case ERHIMemoryUsage::Readback:
        usage = VMA_MEMORY_USAGE_GPU_TO_CPU;
        break;
    case ERHIMemoryUsage::Transient:
        usage = VMA_MEMORY_USAGE_CPU_TO_GPU;
        break;
    default:
        LOG_WARN("[RHIVkDevice::AllocImageDeviceMemory]: Unknown memory usage type, defaulting to GPU_ONLY");
        break;
    }

    VmaAllocation newAlloc = VK_NULL_HANDLE;
    if (!m_MemoryAllocator->AllocateImageMemory(image->image, usage, &newAlloc))
    {
        return false;
    }

    // If there was an old allocation, queue it for individual deletion
    if (image->state->allocation != VK_NULL_HANDLE)
    {
        auto deps = m_ResourceRegistry->GetTickets(image->registryHandle);
        EnqueueDeferredDestroy(deps,
                               [allocator = image->state->allocator, oldAlloc = image->state->allocation]()
                               {
                                   if (allocator != VK_NULL_HANDLE && oldAlloc != VK_NULL_HANDLE)
                                   {
                                       vmaFreeMemory(allocator, oldAlloc);
                                   }
                               });
    }

    image->state->allocation = newAlloc;
    image->allocation = newAlloc; // Sync cache
    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeImageInternal(RHIImageHandle handle)
{
    auto* image = m_ImagePool->Get(handle);
    if (!image) return;

    if (image->image != VK_NULL_HANDLE && image->needDestroy)
    {
        m_ResourceRegistry->Release(image->registryHandle);

        image->image = VK_NULL_HANDLE;
        image->allocation = VK_NULL_HANDLE;
        image->state = nullptr;
        image->registryHandle = RHIResourceHandle::Invalid();
        image->needDestroy = false;
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseImage(RHIImageHandle handle)
{
    FreeImageInternal(handle);
    if (!m_ImagePool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseImage]: Failed to deallocate handle (invalid or stale)!");
    }
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle,
                                                    RHIImageViewDesc&& desc)
{
    ARISEN_PROFILE_ZONE("Vk::AllocImageView");
    auto* viewItem = m_ImageViewPool->Get(handle);
    auto* imageItem = m_ImagePool->Get(imageHandle);
    if (!viewItem || !imageItem || imageItem->image == VK_NULL_HANDLE) return false;

    auto viewInfo = ImageViewCreateInfo(
        imageItem->image, desc.viewType, desc.format, desc.aspectMask,
        desc.baseMipLevel, desc.levelCount, desc.baseArrayLayer, desc.layerCount);

    if (vkCreateImageView(m_VkDevice, &viewInfo, nullptr, &viewItem->view) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocImageView]: failed to create image view!");
        return false;
    }

    viewItem->format = desc.format;
    viewItem->imageHandle = imageHandle;
    viewItem->width = desc.width > 0 ? desc.width : imageItem->width;
    viewItem->height = desc.height > 0 ? desc.height : imageItem->height;

    // Register for deferred deletion
    struct DeferredVkImageView
    {
        VkDevice device;
        VkImageView view;

        ~DeferredVkImageView()
        {
            if (device != VK_NULL_HANDLE && view != VK_NULL_HANDLE)
            {
                vkDestroyImageView(device, view, nullptr);
            }
        }
    };
    auto* deferred = new DeferredVkImageView{m_VkDevice, viewItem->view};
    viewItem->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}

void ArisenEngine::RHI::RHIVkDevice::FreeImageViewInternal(RHIImageViewHandle handle)
{
    auto* viewItem = m_ImageViewPool->Get(handle);
    if (!viewItem) return;

    if (viewItem->view != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(viewItem->registryHandle);
        viewItem->view = VK_NULL_HANDLE;
        viewItem->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseImageView(RHIImageViewHandle handle)
{
    FreeImageViewInternal(handle);
    if (!m_ImageViewPool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseImageView]: Failed to deallocate handle (invalid or stale)!");
    }
}

ArisenEngine::RHI::RHIImageViewHandle ArisenEngine::RHI::RHIVkDevice::FindImageViewForImage(RHIImageHandle imageHandle)
{
    return m_ImageViewPool->FindHandle([imageHandle](const RHIVkImageViewPoolItem& item)
    {
        return item.imageHandle == imageHandle;
    });
}

void ArisenEngine::RHI::RHIVkDevice::FreeSamplerInternal(RHISamplerHandle handle)
{
    auto* sampler = m_SamplerPool->Get(handle);
    if (sampler && sampler->sampler != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(sampler->registryHandle);
        sampler->sampler = VK_NULL_HANDLE;
        sampler->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseSampler(RHISamplerHandle handle)
{
    FreeSamplerInternal(handle);
    if (!m_SamplerPool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseSampler]: Failed to deallocate handle (invalid or stale)!");
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeSemaphoreInternal(RHISemaphoreHandle handle)
{
    auto* sem = m_SemaphorePool->Get(handle);
    if (sem && sem->semaphore != VK_NULL_HANDLE)
    {
        m_ResourceRegistry->Release(sem->registryHandle);
        sem->semaphore = VK_NULL_HANDLE;
        sem->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseSemaphore(
    RHISemaphoreHandle handle)
{
    FreeSemaphoreInternal(handle);
    if (!m_SemaphorePool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseSemaphore]: Failed to deallocate handle (invalid or stale)!");
    }
}



void ArisenEngine::RHI::RHIVkDevice::FreeRenderPassInternal(RHIRenderPassHandle handle)
{
    auto* rp = m_RenderPassPool->Get(handle);
    if (rp && rp->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(rp->registryHandle);
        rp->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseRenderPass(
    RHIRenderPassHandle handle)
{
    FreeRenderPassInternal(handle);
    if (!m_RenderPassPool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseRenderPass]: Failed to deallocate handle (invalid or stale)!");
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeFrameBufferInternal(RHIFrameBufferHandle handle)
{
    auto* fb = m_FrameBufferPool->Get(handle);
    if (fb && fb->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(fb->registryHandle);
        fb->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseFrameBuffer(
    RHIFrameBufferHandle handle)
{
    FreeFrameBufferInternal(handle);
    if (!m_FrameBufferPool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseFrameBuffer]: Failed to deallocate handle (invalid or stale)!");
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreePipelineInternal(RHIPipelineHandle handle)
{
    auto* p = m_PipelinePool->Get(handle);
    if (p && p->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(p->registryHandle);
        p->registryHandle = RHIResourceHandle::Invalid();
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleasePipeline(RHIPipelineHandle handle)
{
    FreePipelineInternal(handle);
    if (!m_PipelinePool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleasePipeline]: Failed to deallocate handle (invalid or stale)!");
    }
}

ArisenEngine::RHI::RHIVkDevice::~RHIVkDevice() noexcept
{
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Start destruction");
    // 1. Wait for GPU to be idle
    DeviceWaitIdle();

    // 2. Drain FrameSync to ensure all submitted work is tracked as completed
    // 2. Drain FrameSync to ensure all submitted work is tracked as completed
    // (Note: In a multi-queue world, we might want to drain all available queues or rely on DeviceWaitIdle)
    if (m_FrameSync)
    {
        // For shutdown, the GPU is idle, so we just need to satisfy the tracker
        m_FrameSync->OnSubmit(0, 0); 
    }

    // 3. Destroy managers that might rely on the device still being alive.
    // This may explicitly release some resources.
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Deleting managers");
    if (m_GPUPipelineManager)
    {
        delete m_GPUPipelineManager;
        m_GPUPipelineManager = nullptr;
    }
    if (m_BindlessManager)
    {
        delete m_BindlessManager;
        m_BindlessManager = nullptr;
    }

    // Explicitly destroy TransferManager before MemoryAllocator is destroyed,
    // so staging buffer VMA allocations are freed while the allocator is still valid.
    if (m_TransferManager)
    {
        m_TransferManager.reset();
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_TransferManager reset");
    }

    // Wait for queues to be idle before destroying anything
    if (m_GraphicsQueue)
        m_GraphicsQueue->WaitIdle();
    if (m_ComputeQueue)
        m_ComputeQueue->WaitIdle();
    if (m_TransferQueue)
        m_TransferQueue->WaitIdle();
    if (m_PresentQueue)
        m_PresentQueue->WaitIdle();

    m_FrameSync.reset();

    // Destroy transfer manager before queues (as it depends on queues and command pools)
    m_TransferManager.reset();

    // Destroy queues before factory and resource registry, because queue destructors
    // call m_Factory->ReleaseSemaphore(...) and need the registry/allocator alive.
    m_GraphicsQueue.reset();
    m_ComputeQueue.reset();
    m_TransferQueue.reset();
    m_PresentQueue.reset();
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: TransferManager, Sync and Queue objects reset");

    // 4. Shut down the Resource Registry to enqueue all remaining resources for deferred destruction.
    if (m_ResourceRegistry)
    {
        m_ResourceRegistry->Shutdown();
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Resource Registry shut down, remaining resources enqueued");
    }

    // 5. Flush all deferred deletions now that we know the GPU is idle and all tickets are completed.
    if (m_DeferredDeletion)
    {
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Flushing deferred deletions");
        constexpr RHIGpuTicket kAll = ~static_cast<RHIGpuTicket>(0);

        m_DeferredDeletion->Flush(RHIQueueType::Graphics, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Compute, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Transfer, kAll);
        m_DeferredDeletion->Flush(RHIQueueType::Present, kAll);
    }

    // 6. Now safe to destroy DescriptorPool (after deferred callbacks have completed)
    if (m_DescriptorPool)
    {
        delete m_DescriptorPool;
        m_DescriptorPool = nullptr;
    }

    // 7. Now safe to destroy the registry object and memory allocator
    m_ResourceRegistry.reset();
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Resource Registry reset");

    // IMPORTANT: Memory allocator must be deleted AFTER all resources that might use it are flushed.
    if (m_MemoryAllocator)
    {
        delete m_MemoryAllocator;
        m_MemoryAllocator = nullptr;
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_MemoryAllocator deleted");
    }

    if (m_Factory)
    {
        delete m_Factory;
        m_Factory = nullptr;
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: m_Factory deleted");
    }

    // 8. Finally destroy the Vulkan device
    if (m_VkDevice != VK_NULL_HANDLE)
    {
        vkDestroyDevice(m_VkDevice, nullptr);
        m_VkDevice = VK_NULL_HANDLE;
        LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: vkDestroyDevice called");
    }

    m_Instance = nullptr;
    LOG_DEBUG("[RHIVkDevice::~RHIVkDevice]: Finished destruction");
}

bool ArisenEngine::RHI::RHIVkDevice::AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex,
                                                      RHIImageViewHandle viewHandle,
                                                      RHIRenderPassHandle renderPassHandle)
{
    ARISEN_PROFILE_ZONE("Vk::AllocFrameBuffer");
    auto* fbItem = m_FrameBufferPool->Get(handle);
    auto* viewItem = m_ImageViewPool->Get(viewHandle);
    auto* rpItem = m_RenderPassPool->Get(renderPassHandle);

    if (!fbItem || !viewItem || !rpItem) return false;

    auto* rpObj = static_cast<RHIVkGPURenderPass*>(rpItem->renderPassObj);
    if (!rpObj) return false;

    VkImageView attachments[] = {viewItem->view};

    VkFramebufferCreateInfo framebufferInfo{};
    framebufferInfo.sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO;
    framebufferInfo.renderPass = static_cast<VkRenderPass>(rpObj->GetHandle(frameIndex));
    framebufferInfo.attachmentCount = 1;
    framebufferInfo.pAttachments = attachments;
    framebufferInfo.width = viewItem->width;
    framebufferInfo.height = viewItem->height;
    framebufferInfo.layers = 1;

    if (vkCreateFramebuffer(m_VkDevice, &framebufferInfo, nullptr, &fbItem->framebuffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocFrameBuffer]: failed to create RHIFrameBuffer!");
        return false;
    }

    fbItem->width = viewItem->width;
    fbItem->height = viewItem->height;

    // Register for deferred deletion
    struct DeferredVkFramebuffer
    {
        VkDevice device;
        VkFramebuffer RHIFrameBuffer;

        ~DeferredVkFramebuffer()
        {
            if (device != VK_NULL_HANDLE && RHIFrameBuffer != VK_NULL_HANDLE)
            {
                vkDestroyFramebuffer(device, RHIFrameBuffer, nullptr);
            }
        }
    };
    auto* deferred = new DeferredVkFramebuffer{m_VkDevice, fbItem->framebuffer};
    fbItem->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(deferred));

    return true;
}



void ArisenEngine::RHI::RHIVkDevice::ReleaseGPUProgram(RHIShaderProgramHandle handle)
{
    auto* item = m_GPUProgramPool->Get(handle);
    if (item)
    {
        if (item->registryHandle.IsValid())
        m_ResourceRegistry->Release(item->registryHandle);

        if (!m_GPUProgramPool->Deallocate(handle))
        {
            LOG_WARN("[RHIVkDevice::ReleaseGPUProgram]: Failed to deallocate handle (invalid or stale)!");
        }
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseCommandBufferPool(RHICommandBufferPoolHandle handle)
{
    auto* item = m_CommandBufferPoolPool->Get(handle);
    if (item)
    {
        if (item->registryHandle.IsValid())
        m_ResourceRegistry->Release(item->registryHandle);

        if (!m_CommandBufferPoolPool->Deallocate(handle))
        {
            LOG_WARN("[RHIVkDevice::ReleaseCommandBufferPool]: Failed to deallocate handle (invalid or stale)!");
        }
    }
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseCommandBuffer(RHICommandBufferHandle handle)
{
    auto* item = m_CommandBufferPool->Get(handle);
    if (item)
    {
        if (item->registryHandle.IsValid())
        m_ResourceRegistry->Release(item->registryHandle);

        if (!m_CommandBufferPool->Deallocate(handle))
        {
            LOG_WARN("[RHIVkDevice::ReleaseCommandBuffer]: Failed to deallocate handle (invalid or stale)!");
        }
    }
}

void ArisenEngine::RHI::RHIVkDevice::GetAccelerationStructureBuildSizes(
    const RHIAccelerationStructureBuildGeometryInfo& buildInfo, const UInt32* pMaxPrimitiveCounts,
    RHIAccelerationStructureBuildSizesInfo* pSizeInfo)
{
    if (!vkGetAccelerationStructureBuildSizesKHR) return;

    // Convert RHI info to Vulkan info
    // This is a simplified version, ideally we need a full converter
    VkAccelerationStructureBuildGeometryInfoKHR vkBuildInfo{};
    vkBuildInfo.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_GEOMETRY_INFO_KHR;
    vkBuildInfo.type = (VkAccelerationStructureTypeKHR)buildInfo.type;
    vkBuildInfo.flags = (VkBuildAccelerationStructureFlagsKHR)buildInfo.flags;
    vkBuildInfo.geometryCount = buildInfo.geometryCount;

    // Convert geometries if necessary
    Containers::Vector<VkAccelerationStructureGeometryKHR> vkGeometries;
    if (buildInfo.pGeometries && buildInfo.geometryCount > 0)
    {
        vkGeometries.reserve(buildInfo.geometryCount);
        for (UInt32 i = 0; i < buildInfo.geometryCount; ++i)
        {
            const auto& rhiGeom = buildInfo.pGeometries[i];
            VkAccelerationStructureGeometryKHR vkGeom{};
            vkGeom.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_KHR;
            vkGeom.geometryType = (VkGeometryTypeKHR)rhiGeom.type;
            vkGeom.flags = (VkGeometryFlagsKHR)rhiGeom.flags;

            if (rhiGeom.type == ERHIAccelerationStructureGeometryType::Triangles)
            {
                vkGeom.geometry.triangles.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_TRIANGLES_DATA_KHR;
                vkGeom.geometry.triangles.vertexFormat = (VkFormat)rhiGeom.triangles.vertexFormat;
                vkGeom.geometry.triangles.vertexData.deviceAddress = rhiGeom.triangles.vertexData;
                vkGeom.geometry.triangles.vertexStride = rhiGeom.triangles.vertexStride;
                vkGeom.geometry.triangles.maxVertex = rhiGeom.triangles.maxVertex;
                vkGeom.geometry.triangles.indexType = (VkIndexType)rhiGeom.triangles.indexType;
                vkGeom.geometry.triangles.indexData.deviceAddress = rhiGeom.triangles.indexData;
                vkGeom.geometry.triangles.transformData.deviceAddress = rhiGeom.triangles.transformData;
            }
            else if (rhiGeom.type == ERHIAccelerationStructureGeometryType::AABBs)
            {
                vkGeom.geometry.aabbs.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_AABBS_DATA_KHR;
                vkGeom.geometry.aabbs.data.deviceAddress = rhiGeom.aabbs.data;
                vkGeom.geometry.aabbs.stride = rhiGeom.aabbs.stride;
            }
            else if (rhiGeom.type == ERHIAccelerationStructureGeometryType::Instances)
            {
                vkGeom.geometry.instances.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_GEOMETRY_INSTANCES_DATA_KHR;
                vkGeom.geometry.instances.arrayOfPointers = rhiGeom.instances.arrayOfPointers ? VK_TRUE : VK_FALSE;
                vkGeom.geometry.instances.data.deviceAddress = rhiGeom.instances.data;
            }
            vkGeometries.push_back(vkGeom);
        }
    }

    if (!vkGeometries.empty())
    {
        vkBuildInfo.pGeometries = vkGeometries.data();
    }

    VkAccelerationStructureBuildSizesInfoKHR vkSizeInfo{};
    vkSizeInfo.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_BUILD_SIZES_INFO_KHR;

    vkGetAccelerationStructureBuildSizesKHR(m_VkDevice, VK_ACCELERATION_STRUCTURE_BUILD_TYPE_DEVICE_KHR, &vkBuildInfo,
                                            pMaxPrimitiveCounts, &vkSizeInfo);


    pSizeInfo->accelerationStructureSize = vkSizeInfo.accelerationStructureSize;
    pSizeInfo->updateScratchSize = vkSizeInfo.updateScratchSize;
    pSizeInfo->buildScratchSize = vkSizeInfo.buildScratchSize;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocAccelerationStructure(RHIAccelerationStructureHandle handle,
                                                                ERHIAccelerationStructureType type, UInt64 size,
                                                                RHIBufferHandle buffer, UInt64 offset)
{
    ARISEN_PROFILE_ZONE("Vk::AllocAccelerationStructure");
    if (!vkCreateAccelerationStructureKHR) return false;

    auto* asItem = m_AccelerationStructurePool->Get(handle);
    if (!asItem) return false;

    auto* bufItem = m_BufferPool->Get(buffer);
    if (!bufItem) return false;

    VkAccelerationStructureCreateInfoKHR createInfo{};
    createInfo.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_CREATE_INFO_KHR;
    createInfo.buffer = bufItem->buffer;
    createInfo.offset = offset;
    createInfo.size = size;
    createInfo.type = (VkAccelerationStructureTypeKHR)type;

    VkAccelerationStructureKHR vkAS;
    VkResult result = vkCreateAccelerationStructureKHR(m_VkDevice, &createInfo, nullptr, &vkAS);
    if (result != VK_SUCCESS)
    {
        LOG_ERROR_AND_THROW(
            String::Format(
                "[RHIVkDevice::AllocAccelerationStructure]: failed to create acceleration structure! Result: %d", result
            ));
        m_AccelerationStructurePool->Deallocate(handle);
        return false;
    }

    VkAccelerationStructureDeviceAddressInfoKHR addressInfo{};
    addressInfo.sType = VK_STRUCTURE_TYPE_ACCELERATION_STRUCTURE_DEVICE_ADDRESS_INFO_KHR;
    addressInfo.accelerationStructure = vkAS;

    asItem->accelerationStructure = vkAS;
    asItem->bufferHandle = buffer;
    asItem->size = size;
    asItem->deviceAddress = vkGetAccelerationStructureDeviceAddressKHR(m_VkDevice, &addressInfo);

    std::cout << "[RHIVkDevice::AllocAccelerationStructure] Created AS Handle: " << handle.index << ", VkHandle: " << (
        UInt64)vkAS << ", Address: " << asItem->deviceAddress << std::endl;

    struct DeferredASDeletion
    {
        RHIVkDevice* device;
        RHIAccelerationStructureHandle handle;
        ~DeferredASDeletion() { device->FreeAccelerationStructureInternal(handle); }
    };
    asItem->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(new DeferredASDeletion{this, handle}));

    return true;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle)
{
    auto* item = m_AccelerationStructurePool->Get(handle);
    if (item)
    {
        if (item->registryHandle.IsValid())
        m_ResourceRegistry->Release(item->registryHandle);

        if (!m_AccelerationStructurePool->Deallocate(handle))
        {
            LOG_WARN("[RHIVkDevice::ReleaseAccelerationStructure]: Failed to deallocate handle (invalid or stale)!");
        }
    }
}

ArisenEngine::UInt64 ArisenEngine::RHI::RHIVkDevice::GetAccelerationStructureDeviceAddress(
    RHIAccelerationStructureHandle handle)
{
    auto* item = m_AccelerationStructurePool->Get(handle);
    if (!item)
    {
        LOG_ERRORF("[RHIVkDevice::GetAccelerationStructureDeviceAddress]: Invalid AS handle {0}", (UInt64)handle.index);
        return 0;
    }
    if (item->deviceAddress == 0)
    {
        LOG_ERRORF("[RHIVkDevice::GetAccelerationStructureDeviceAddress]: AS {0} has 0 device address",
                   (UInt64)handle.index);
    }
    return item->deviceAddress;
}

void ArisenEngine::RHI::RHIVkDevice::GetRayTracingShaderGroupHandles(RHIPipelineHandle pipeline, UInt32 firstGroup,
                                                                     UInt32 groupCount, UInt64 size, void* pData)
{
    if (!vkGetRayTracingShaderGroupHandlesKHR) return;

    auto* p = m_PipelinePool->Get(pipeline);
    if (!p || !p->pipeline) return;

    auto* vkPipeline = static_cast<RHIVkGPUPipeline*>(p->pipeline);

    // Ensure pipeline is allocated for frame 0 (typical for SBT creation outside main loop)
    VkPipeline handle = vkPipeline->GetVkPipeline(0);
    if (handle == VK_NULL_HANDLE)
    {
        vkPipeline->AllocRayTracingPipeline(0);
        handle = vkPipeline->GetVkPipeline(0);
    }

    if (handle == VK_NULL_HANDLE)
    {
        LOG_ERROR("[RHIVkDevice::GetRayTracingShaderGroupHandles]: Pipeline allocation failed!");
        return;
    }

    vkGetRayTracingShaderGroupHandlesKHR(m_VkDevice, handle, firstGroup, groupCount, (size_t)size, pData);
}

void ArisenEngine::RHI::RHIVkDevice::FreeAccelerationStructureInternal(RHIAccelerationStructureHandle handle)
{
    auto* item = m_AccelerationStructurePool->Get(handle);
    if (item && item->accelerationStructure != VK_NULL_HANDLE)
    {
        vkDestroyAccelerationStructureKHR(m_VkDevice, item->accelerationStructure, nullptr);
        item->accelerationStructure = VK_NULL_HANDLE;
    }
}

bool ArisenEngine::RHI::RHIVkDevice::AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits)
{
    auto* poolItem = m_MemoryPoolPool->Get(handle);
    if (!poolItem) return false;

    VmaMemoryUsage usage = VMA_MEMORY_USAGE_AUTO;
    if (usageBits & VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT)
    {
        usage = VMA_MEMORY_USAGE_GPU_ONLY;
    }

    VmaAllocation allocation = VK_NULL_HANDLE;
    if (!m_MemoryAllocator->AllocateMemory(size, usage, &allocation))
    {
        return false;
    }

    poolItem->state = new RHIVkMemoryPoolState();
    poolItem->state->allocator = m_MemoryAllocator->GetVmaAllocator();
    poolItem->state->allocation = allocation;
    poolItem->state->size = size;
    poolItem->allocation = allocation;
    poolItem->size = size;

    poolItem->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(poolItem->state));

    return true;
}

void ArisenEngine::RHI::RHIVkDevice::ReleaseMemoryPool(RHIMemoryPoolHandle handle)
{
    FreeMemoryPoolInternal(handle);
    if (!m_MemoryPoolPool->Deallocate(handle))
    {
        LOG_WARN("[RHIVkDevice::ReleaseMemoryPool]: Failed to deallocate handle!");
    }
}

void ArisenEngine::RHI::RHIVkDevice::FreeMemoryPoolInternal(RHIMemoryPoolHandle handle)
{
    auto* poolItem = m_MemoryPoolPool->Get(handle);
    if (poolItem && poolItem->registryHandle.IsValid())
    {
        m_ResourceRegistry->Release(poolItem->registryHandle);
        poolItem->state = nullptr;
        poolItem->allocation = VK_NULL_HANDLE;
        poolItem->registryHandle = RHIResourceHandle::Invalid();
    }
}

bool ArisenEngine::RHI::RHIVkDevice::AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc,
                                                        RHIMemoryPoolHandle pool, UInt64 offset)
{
    ARISEN_PROFILE_ZONE("Vk::AllocBufferAliased");
    auto* buffer = m_BufferPool->Get(handle);
    auto* poolItem = m_MemoryPoolPool->Get(pool);
    if (!buffer || !poolItem || poolItem->allocation == VK_NULL_HANDLE) return false;

    auto bufferInfo = BufferCreateInfo(
        desc.createFlagBits,
        desc.size,
        desc.usage,
        desc.sharingMode,
        desc.queueFamilyIndexCount,
        (const uint32_t*)desc.pQueueFamilyIndices);

    buffer->size = desc.size;
    buffer->range = desc.size;
    buffer->offset = offset;

    if (vkCreateBuffer(m_VkDevice, &bufferInfo, nullptr, &buffer->buffer) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocBufferAliased]: failed to create buffer!");
        return false;
    }

    if (!m_MemoryAllocator->BindBufferMemory(buffer->buffer, poolItem->allocation, offset))
    {
        vkDestroyBuffer(m_VkDevice, buffer->buffer, nullptr);
        buffer->buffer = VK_NULL_HANDLE;
        return false;
    }

    // Register for deferred deletion (buffer only, memory is shared)
    struct AliasedBufferState
    {
        VkDevice device;
        VkBuffer buffer;
        ~AliasedBufferState() { if (device && buffer) vkDestroyBuffer(device, buffer, nullptr); }
    };
    auto* state = new AliasedBufferState{m_VkDevice, buffer->buffer};
    buffer->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(state));
    buffer->state = nullptr; // Important: we don't own the memory allocation here
    buffer->allocation = poolItem->allocation;

    return true;
}

bool ArisenEngine::RHI::RHIVkDevice::AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc,
                                                       RHIMemoryPoolHandle pool, UInt64 offset)
{
    ARISEN_PROFILE_ZONE("Vk::AllocImageAliased");
    auto* image = m_ImagePool->Get(handle);
    auto* poolItem = m_MemoryPoolPool->Get(pool);
    if (!image || !poolItem || poolItem->allocation == VK_NULL_HANDLE) return false;

    auto imageInfo = ImageCreateInfo(
        desc.imageType,
        desc.width, desc.height, desc.depth,
        desc.mipLevels, desc.arrayLayers,
        desc.format, desc.tiling,
        desc.imageLayout, desc.usage,
        desc.sampleCount, desc.sharingMode,
        desc.queueFamilyIndexCount,
        (const uint32_t*)desc.pQueueFamilyIndices);

    if (vkCreateImage(m_VkDevice, &imageInfo, nullptr, &image->image) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkDevice::AllocImageAliased]: failed to create image!");
        return false;
    }

    if (!m_MemoryAllocator->BindImageMemory(image->image, poolItem->allocation, offset))
    {
        vkDestroyImage(m_VkDevice, image->image, nullptr);
        image->image = VK_NULL_HANDLE;
        return false;
    }

    image->width = desc.width;
    image->height = desc.height;
    image->mipLevels = desc.mipLevels;
    image->currentLayout = static_cast<VkImageLayout>(desc.imageLayout);
    image->needDestroy = true;

    // Register for deferred deletion (image only, memory is shared)
    struct AliasedImageState
    {
        VkDevice device;
        VkImage image;
        ~AliasedImageState() { if (device && image) vkDestroyImage(device, image, nullptr); }
    };
    auto* state = new AliasedImageState{m_VkDevice, image->image};
    image->registryHandle = m_ResourceRegistry->Create(MakeDeferredDeleteItem(state));
    image->state = nullptr; // Important: we don't own the memory allocation here
    image->allocation = poolItem->allocation;

    return true;
}


namespace ArisenEngine::RHI
{
    EFormat RHIVkDevice::GetImageViewFormat(RHIImageViewHandle handle)
    {
        auto* pItem = m_ImageViewPool->Get(handle);
        return pItem ? pItem->format : EFormat::FORMAT_UNDEFINED;
    }

    UInt32 RHIVkDevice::GetImageViewWidth(RHIImageViewHandle handle)
    {
        auto* pItem = m_ImageViewPool->Get(handle);
        return pItem ? pItem->width : 0U;
    }

    UInt32 RHIVkDevice::GetImageViewHeight(RHIImageViewHandle handle)
    {
        auto* pItem = m_ImageViewPool->Get(handle);
        return pItem ? pItem->height : 0U;
    }

    void RHIVkDevice::SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size,
                                                          const void* data)
    {
        auto* p = m_GPUProgramPool->Get(handle);
        if (p && p->program)
        {
            p->program->SetSpecializationConstant(constantID, size, data);
        }
    }

    void RHIVkDevice::WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value)
    {
        ARISEN_PROFILE_ZONE("Vk::WaitSemaphoreValue");
        auto* item = m_SemaphorePool->Get(handle);
        if (item && item->semaphore != VK_NULL_HANDLE)
        {
            VkSemaphoreWaitInfo waitInfo{};
            waitInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_WAIT_INFO;
            waitInfo.semaphoreCount = 1;
            waitInfo.pSemaphores = &item->semaphore;
            waitInfo.pValues = &value;
            vkWaitSemaphores(m_VkDevice, &waitInfo, UINT64_MAX);
        }
    }

    void RHIVkDevice::SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value)
    {
        ARISEN_PROFILE_ZONE("Vk::SignalSemaphoreValue");
        auto* item = m_SemaphorePool->Get(handle);
        if (item && item->semaphore != VK_NULL_HANDLE)
        {
            VkSemaphoreSignalInfo signalInfo{};
            signalInfo.sType = VK_STRUCTURE_TYPE_SEMAPHORE_SIGNAL_INFO;
            signalInfo.semaphore = item->semaphore;
            signalInfo.value = value;
            vkSignalSemaphore(m_VkDevice, &signalInfo);
        }
    }

    UInt64 RHIVkDevice::GetSemaphoreValue(RHISemaphoreHandle handle)
    {
        auto* item = m_SemaphorePool->Get(handle);
        if (item && item->semaphore != VK_NULL_HANDLE)
        {
            uint64_t val = 0;
            vkGetSemaphoreCounterValue(m_VkDevice, item->semaphore, &val);
            return val;
        }
        return 0;
    }

    RHIDescriptorHeap* RHIVkDevice::CreateDescriptorHeap(EDescriptorHeapType type, UInt32 descriptorCount)
    {
        return new RHIVkDescriptorHeap(this, type, descriptorCount);
    }

    RHIBindlessDescriptorTable* RHIVkDevice::CreateBindlessDescriptorTable(RHIDescriptorHeap* heap)
    {
        return new RHIVkBindlessDescriptorTable(this, static_cast<RHIVkDescriptorHeap*>(heap));
    }
}
