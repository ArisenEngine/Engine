#pragma once
#include "Base/FoundationMinimal.h"
#include "RHICommon.h"
#include "RHI/Enums/Memory/EMemoryPropertyFlagBits.h"
#include "RHI/Descriptors/RHIDescriptorPool.h"
#include "RHI/Definitions/DeviceLimits.h"
#include "RHI/Queues/RHIQueue.h"
#include "RHI/Handles/RHIHandle.h"
#include "RHI/Definitions/CoreRHICommon.h"
#include "RHI/Descriptors/RHIResourceDescriptors.h"
#include "RHI/Core/RHIInspector.h"
#include "RHI/Descriptors/RHIDescriptorHeap.h"
#include "RHI/Descriptors/RHIBindlessDescriptorTable.h"

namespace ArisenEngine::RHI
{
    class RHIPipelineCache;
    class RHIInstance;
    class RHISurface;
    class RHICommandBufferPool;
    class RHIRenderPass;
    class RHIFrameBuffer;
    class RHIFence;
    class RHIFactory;
    class RHIMemoryAllocator;

    enum class ERHIAccelerationStructureType;
    struct RHIAccelerationStructureBuildGeometryInfo;
    struct RHIAccelerationStructureBuildSizesInfo;

    /**
     * @brief Internal interface for RHI backends.
     * Contains methods that should not be exposed directly to the user.
     * 
     * TODO(CppSharp): IRHIBackend 不需要导出到 C#。确保 CppSharp 配置中跳过此类。
     * 它是纯内部后端接口，上层管线不应直接使用。
     */
    class IRHIBackend
    {
    public:
        virtual ~IRHIBackend() = default;

        // Internal Allocation / Release (Moved from RHIDevice)
        // Internal Allocation / Release (Moved from RHIDevice)
        virtual bool AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc) = 0;
        virtual bool AllocBufferDeviceMemory(RHIBufferHandle handle) = 0;
        virtual void ReleaseBuffer(RHIBufferHandle handle) = 0;

        virtual bool AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc) = 0;
        virtual bool AllocImageDeviceMemory(RHIImageHandle handle) = 0;
        virtual void ReleaseImage(RHIImageHandle handle) = 0;

        virtual bool AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits) = 0;
        virtual void ReleaseMemoryPool(RHIMemoryPoolHandle handle) = 0;

        virtual bool AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) = 0;
        virtual bool AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) = 0;

        virtual bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, RHIImageViewDesc&& desc) = 0;
        virtual void ReleaseImageView(RHIImageViewHandle handle) = 0;

        virtual void ReleaseSampler(RHISamplerHandle handle) = 0;
        virtual void ReleaseSemaphore(RHISemaphoreHandle handle) = 0;
        virtual void ReleaseFence(RHIFenceHandle handle) = 0;
        virtual void ReleaseRenderPass(RHIRenderPassHandle handle) = 0;
        virtual void ReleaseFrameBuffer(RHIFrameBufferHandle handle) = 0;
        virtual void ReleasePipeline(RHIPipelineHandle handle) = 0;
        
        virtual void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) = 0;
        virtual bool AllocAccelerationStructure(RHIAccelerationStructureHandle handle, ERHIAccelerationStructureType type, UInt64 size, RHIBufferHandle buffer, UInt64 offset) = 0;
        virtual bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle) = 0;
    };

    class RHI_DLL RHIDevice
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIDevice)
        virtual ~RHIDevice() noexcept
        {
            m_Instance = nullptr;
            m_Surface = nullptr;
        }

        RHISurface* GetSurface() const { return m_Surface; }
        RHIInstance* GetInstance() const { return m_Instance; }
        virtual UInt32 GetMaxFramesInFlight() const = 0;
        
        // TODO(CppSharp-P0): 消除 void* 返回类型。GetHandle() 应移至 IRHIBackend 或改为不导出。
        // 上层管线不应直接访问 VkDevice/ID3D12Device，所有操作应通过 RHIDevice 的类型安全方法完成。
        virtual void* GetHandle() const = 0;
        virtual void DeviceWaitIdle() const = 0;
        // TODO(CppSharp-P0): GetGraphicsQueue/GetComputeQueue/GetPresentQueue 返回 void* 泄漏了后端队列对象。
        // 方案A: 移至 IRHIBackend（推荐）— 上层通过 RHIQueue* GetQueue(RHIQueueType) 获取。
        // 方案B: 改为 RHIQueueHandle 类型安全句柄。
        virtual void* GetGraphicsQueue() = 0;
        virtual void* GetComputeQueue() = 0;
        virtual void* GetPresentQueue() = 0;
        virtual void GraphicQueueWaitIdle() const = 0;

        virtual RHIFactory* GetFactory() const = 0;

        virtual RHIPipelineCache* GetPipelineCache() const = 0;

        virtual RHIDescriptorPool* GetDescriptorPool() const = 0;

        virtual RHIMemoryAllocator* GetMemoryAllocator() const = 0;

        virtual RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer, const struct RHISubmitDescriptor* descriptor = nullptr) = 0;

        // Descriptor Heap & Bindless Table
        virtual RHIDescriptorHeap* CreateDescriptorHeap(EDescriptorHeapType type, UInt32 descriptorCount) = 0;
        virtual RHIBindlessDescriptorTable* CreateBindlessDescriptorTable(RHIDescriptorHeap* heap) = 0;

        // Handle Resolution
        virtual RHICommandBuffer* GetCommandBuffer(RHICommandBufferHandle handle) = 0;

        // Optional per-frame update hook for GPU completion polling / automatic GC.
        // Default: no-op.
        // virtual void Update() {}  <-- REMOVED per user request (redundant)

        virtual RHIGpuTicket GetCompletedSubmitTicket() const { return 0; }
        virtual void WaitQueueTicket(RHIGpuTicket ticket) { (void)ticket; }

        // Optional: expose backend queues (graphics/compute/transfer/present).
        // Backends may return nullptr for unsupported queues.
        virtual RHIQueue* GetQueue(RHIQueueType type) { (void)type; return nullptr; }
        virtual RHICommandBufferPool* GetCommandBufferPool(RHICommandBufferPoolHandle handle) { (void)handle; return nullptr; }

        // Queue-scoped deferred delete helper. Backends can override to route to their deletion queue.
        // Default is immediate delete (safe for non-GPU objects).
        virtual void DeferredDelete(RHIQueueType queue, RHIGpuTicket ticket, RHIDeferredDeleteItem item)
        {
            (void)queue;
            (void)ticket;
            if (item.deleter && item.ptr) item.deleter(item.ptr);
        }

        // TODO(CppSharp-P1): FindMemoryType 是后端细节，上层不应调用。移至 IRHIBackend。
        // 上层通过 ERHIMemoryUsage 枚举指定内存意图，后端自行选择 memoryType。
        virtual UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) = 0;

        virtual void SetResolution(UInt32 width, UInt32 height) = 0;

        virtual const RHIResourceStats& GetResourceStats() const = 0;

        const RHIDeviceLimits GetDeviceLimits() const

        {
            return m_DeviceLimits;
        }

        // Bindless Resource Support
        // TODO(Interface-P1): RegisterBindlessResource 系列方法应移至 RHIBindlessDescriptorTable 类。
        // RHIDevice 不应直接管理 bindless 索引分配，应由 BindlessTable 统一管理。
        virtual UInt32 RegisterBindlessResource(RHIImageViewHandle image) { (void)image; return 0xFFFFFFFF; }
        virtual UInt32 RegisterBindlessResource(RHIBufferHandle buffer) { (void)buffer; return 0xFFFFFFFF; }
        virtual UInt32 RegisterBindlessResource(RHISamplerHandle sampler) { (void)sampler; return 0xFFFFFFFF; }

        // Debug & Naming
        virtual void SetObjectName(ERHIObjectType type, UInt64 handle, const char* name) { (void)type; (void)handle; (void)name; }

        // Buffer Utilities
        // TODO(Interface-P1): 以下 Buffer 操作方法应提取为独立的 IRHIBufferOps 接口或移入 RHIFactory。
        // RHIDevice 作为 God-class 拥有 50+ 方法，职责过重。BufferMemoryCopy/Map/Unmap 是资源操作，
        // 而非设备级操作。建议 RHIFactory 扩展为 RHIResourceManager，整合 Create/Release/Map/Copy。
        // TODO(CppSharp-P0): MapBuffer 返回 void*，CppSharp 需要用 IntPtr 包装。
        // 考虑改为 bool MapBuffer(handle, void** ppData) 或提供 typed MapBuffer<T>()。
        virtual void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0) = 0;
        virtual void* MapBuffer(RHIBufferHandle handle) = 0;
        virtual void UnmapBuffer(RHIBufferHandle handle) = 0;
        virtual UInt64 GetBufferSize(RHIBufferHandle handle) = 0;
        virtual UInt64 GetBufferOffset(RHIBufferHandle handle) = 0;
        virtual UInt64 GetBufferRange(RHIBufferHandle handle) = 0;
        virtual UInt64 GetBufferDeviceAddress(RHIBufferHandle handle) = 0;

    public:
        // TODO(Interface-P2): FindImageViewForImage 应移至 RHIFactory，它是资源查询而非设备操作。
        virtual RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) = 0;

    public:
        // TODO(Interface-P1): 光线追踪查询方法应提取为 IRHIRayTracingOps 接口。
        // 不是所有硬件都支持 RT，将这些方法留在 RHIDevice 基类中增加了接口面积。
        // 建议: auto* rtOps = device->QueryInterface<IRHIRayTracingOps>(); 按需获取。
        // TODO(CppSharp-P0): GetRayTracingShaderGroupHandles 的 void* pData 参数需要类型化。
        virtual void GetAccelerationStructureBuildSizes(const RHIAccelerationStructureBuildGeometryInfo& buildInfo, const UInt32* pMaxPrimitiveCounts, RHIAccelerationStructureBuildSizesInfo* pSizeInfo) = 0;
        virtual UInt64 GetAccelerationStructureDeviceAddress(RHIAccelerationStructureHandle handle) = 0;
        virtual void GetRayTracingShaderGroupHandles(RHIPipelineHandle pipeline, UInt32 firstGroup, UInt32 groupCount, UInt64 size, void* pData) = 0;

        // TODO(Interface-P2): Fence/Semaphore 操作应考虑移至 RHIQueue 或独立的 IRHISyncOps。
        // 当前 WaitFence/ResetFence/WaitSemaphoreValue 等分散在 RHIDevice 中，语义上属于同步子系统。
        virtual void WaitFence(RHIFenceHandle handle) = 0;
        virtual void ResetFence(RHIFenceHandle handle) = 0;

        // TODO(Interface-P2): ImageView 查询方法应移至 RHIFactory 或新增 RHIImageViewInfo 查询结构。
        // 统一为: RHIImageViewInfo GetImageViewInfo(RHIImageViewHandle) 返回一个POD结构体。
        virtual RHI::EFormat GetImageViewFormat(RHIImageViewHandle handle) = 0;
        virtual UInt32 GetImageViewWidth(RHIImageViewHandle handle) = 0;
        virtual UInt32 GetImageViewHeight(RHIImageViewHandle handle) = 0;

        virtual void SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size, const void* data) = 0;

        virtual void WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) = 0;
        virtual void SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) = 0;
        virtual UInt64 GetSemaphoreValue(RHISemaphoreHandle handle) = 0;

    protected:
        
        RHIInstance* m_Instance;
        RHISurface* m_Surface;
        RHIDeviceLimits m_DeviceLimits;
        RHIDevice(RHIInstance* instance, RHISurface* surface): m_Instance(instance), m_Surface(surface) {}
    private:
        
    };
}

