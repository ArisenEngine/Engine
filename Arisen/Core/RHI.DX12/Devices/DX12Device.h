#pragma once


#include "../Common.h"
#include "RHI/Core/RHIDevice.h"

namespace ArisenEngine::RHI
{
	 class DX12Device final : public RHIDevice, public IRHIBackend
	{
	public:
		DX12Device(RHIInstance* instance, RHISurface* surface) : RHIDevice(instance, surface) {}
		void* GetHandle() const override { return nullptr; }
		void DeviceWaitIdle() const override {}
		void GraphicQueueWaitIdle() const override {}
		RHIFactory* GetFactory() const override { return nullptr; }
		RHISyncPrimitive* GetSync() const override { return nullptr; }
		RayTracingExtension* GetRayTracing() const override { return nullptr; }
		RHIPipelineCache* GetPipelineCache() const override { return nullptr; }
		RHIDescriptorPool* GetDescriptorPool() const override { return nullptr; }
		RHIDescriptorPoolHandle GetDescriptorPoolHandle() const override { return RHIDescriptorPoolHandle::Invalid(); }
		RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer, const struct RHISubmitDescriptor* descriptor = nullptr) override { (void)commandBuffer; (void)descriptor; return 0; }
		UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override { (void)typeFilter; (void)properties; return 0; }
		void SetResolution(UInt32 width, UInt32 height) override { (void)width; (void)height; }
		void SetObjectName(ERHIObjectType type, UInt64 handle, const char* name) override { (void)type; (void)handle; (void)name; }
		UInt32 GetMaxFramesInFlight() const override { return 1; }
		
		void* GetGraphicsQueue() override { return nullptr; }
		void* GetComputeQueue() override { return nullptr; }
		void* GetPresentQueue() override { return nullptr; }
		RHICommandBuffer* GetCommandBuffer(RHICommandBufferHandle handle) override { (void)handle; return nullptr; }
		const RHIResourceStats& GetResourceStats() const override { static RHIResourceStats stats; return stats; }

		// Stubs for missing pure virtuals
		class RHIMemoryAllocator* GetMemoryAllocator() const override { return nullptr; }
		
		// IRHIBackend implementation
		bool AllocBuffer(RHIBufferHandle handle, RHIBufferDescriptor&& desc) override { (void)handle; (void)desc; return false; }
		bool AllocBufferDeviceMemory(RHIBufferHandle handle) override { (void)handle; return false; }
		void ReleaseBuffer(RHIBufferHandle handle) override { (void)handle; }
		bool AllocImage(RHIImageHandle handle, RHIImageDescriptor&& desc) override { (void)handle; (void)desc; return false; }
		bool AllocImageDeviceMemory(RHIImageHandle handle) override { (void)handle; return false; }
		void ReleaseImage(RHIImageHandle handle) override { (void)handle; }
		bool AllocMemoryPool(RHIMemoryPoolHandle handle, UInt64 size, UInt32 usageBits) override { (void)handle; (void)size; (void)usageBits; return false; }
		void ReleaseMemoryPool(RHIMemoryPoolHandle handle) override { (void)handle; }
		bool AllocBufferAliased(RHIBufferHandle handle, RHIBufferDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) override { (void)handle; (void)desc; (void)pool; (void)offset; return false; }
		bool AllocImageAliased(RHIImageHandle handle, RHIImageDescriptor&& desc, RHIMemoryPoolHandle pool, UInt64 offset) override { (void)handle; (void)desc; (void)pool; (void)offset; return false; }
		bool AllocImageView(RHIImageViewHandle handle, RHIImageHandle imageHandle, RHIImageViewDesc&& desc) override { (void)handle; (void)imageHandle; (void)desc; return false; }
		void ReleaseImageView(RHIImageViewHandle handle) override { (void)handle; }
		void ReleaseSampler(RHISamplerHandle handle) override { (void)handle; }
		void ReleaseSemaphore(RHISemaphoreHandle handle) override { (void)handle; }
		void ReleaseFence(RHIFenceHandle handle) override { (void)handle; }
		void ReleaseRenderPass(RHIRenderPassHandle handle) override { (void)handle; }
		void ReleaseFrameBuffer(RHIFrameBufferHandle handle) override { (void)handle; }
		void ReleasePipeline(RHIPipelineHandle handle) override { (void)handle; }
		void ReleaseAccelerationStructure(RHIAccelerationStructureHandle handle) override { (void)handle; }
		bool AllocAccelerationStructure(RHIAccelerationStructureHandle handle, ERHIAccelerationStructureType type, UInt64 size, RHIBufferHandle buffer, UInt64 offset) override { (void)handle; (void)type; (void)size; (void)buffer; (void)offset; return false; }
		bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle) override { (void)handle; (void)frameIndex; (void)viewHandle; (void)renderPassHandle; return false; }

		// Descriptor Heap & Bindless Table
		RHIDescriptorHeap* CreateDescriptorHeap(EDescriptorHeapType type, UInt32 descriptorCount) override { (void)type; (void)descriptorCount; return nullptr; }
		RHIBindlessDescriptorTable* CreateBindlessDescriptorTable(RHIDescriptorHeap* heap) override { (void)heap; return nullptr; }
	};

}

extern "C" RHI_DX12_DLL ArisenEngine::RHI::RHIDevice * CreateDevice();
