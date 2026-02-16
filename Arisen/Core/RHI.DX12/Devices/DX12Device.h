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
		RHIPipelineCache* GetPipelineCache() const override { return nullptr; }
		RHIDescriptorPool* GetDescriptorPool() const override { return nullptr; }
		RHIGpuTicket Submit(RHICommandBufferHandle commandBuffer, const struct RHISubmitDescriptor* descriptor = nullptr) override { (void)commandBuffer; (void)descriptor; return 0; }
		UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override { (void)typeFilter; (void)properties; return 0; }
		void SetResolution(UInt32 width, UInt32 height) override { (void)width; (void)height; }
		UInt32 GetMaxFramesInFlight() const override { return 1; }
// Stubs for missing pure virtuals
		class RHIMemoryAllocator* GetMemoryAllocator() const override { return nullptr; }
		void ReleaseImageView(RHIImageViewHandle handle) override { (void)handle; }
		RHIImageViewHandle FindImageViewForImage(RHIImageHandle imageHandle) override { (void)imageHandle; return RHIImageViewHandle::Invalid(); }
		void ReleaseSampler(RHISamplerHandle handle) override { (void)handle; }
		void ReleaseSemaphore(RHISemaphoreHandle handle) override { (void)handle; }
		void ReleaseFence(RHIFenceHandle handle) override { (void)handle; }
		void ReleaseRenderPass(RHIRenderPassHandle handle) override { (void)handle; }
		void ReleaseFrameBuffer(RHIFrameBufferHandle handle) override { (void)handle; }
		void ReleasePipeline(RHIPipelineHandle handle) override { (void)handle; }
		bool AllocFrameBuffer(RHIFrameBufferHandle handle, UInt32 frameIndex, RHIImageViewHandle viewHandle, RHIRenderPassHandle renderPassHandle) override { (void)handle; (void)frameIndex; (void)viewHandle; (void)renderPassHandle; return false; }
		void WaitFence(RHIFenceHandle handle) override { (void)handle; }
		void ResetFence(RHIFenceHandle handle) override { (void)handle; }

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
		
		// Buffer utilities in RHIDevice
		void BufferMemoryCopy(RHIBufferHandle handle, const void* src, UInt64 size, UInt64 offset = 0) override { (void)handle; (void)src; (void)size; (void)offset; }
		void* MapBuffer(RHIBufferHandle handle) override { (void)handle; return nullptr; }
		void UnmapBuffer(RHIBufferHandle handle) override { (void)handle; }
		UInt64 GetBufferSize(RHIBufferHandle handle) override { (void)handle; return 0; }
		UInt64 GetBufferOffset(RHIBufferHandle handle) override { (void)handle; return 0; }
		UInt64 GetBufferRange(RHIBufferHandle handle) override { (void)handle; return 0; }
		UInt64 GetBufferDeviceAddress(RHIBufferHandle handle) override { (void)handle; return 0; }

		RHI::EFormat GetImageViewFormat(RHIImageViewHandle handle) override { (void)handle; return RHI::FORMAT_UNDEFINED; }
		UInt32 GetImageViewWidth(RHIImageViewHandle handle) override { (void)handle; return 0; }
		UInt32 GetImageViewHeight(RHIImageViewHandle handle) override { (void)handle; return 0; }
		void SetGPUProgramSpecializationConstant(RHIShaderProgramHandle handle, UInt32 constantID, UInt32 size, const void* data) override { (void)handle; (void)constantID; (void)size; (void)data; }
		void WaitSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) override { (void)handle; (void)value; }
		void SignalSemaphoreValue(RHISemaphoreHandle handle, UInt64 value) override { (void)handle; (void)value; }
		UInt64 GetSemaphoreValue(RHISemaphoreHandle handle) override { (void)handle; return 0; }
	};

}

extern "C" RHI_DX12_DLL ArisenEngine::RHI::RHIDevice * CreateDevice();


