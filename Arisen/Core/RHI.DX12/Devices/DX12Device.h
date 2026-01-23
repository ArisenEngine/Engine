#pragma once


#include "../Common.h"
#include "RHI/Devices/RHIDevice.h"

namespace ArisenEngine::RHI
{
	 class DX12Device final : public RHIDevice
	{
	public:
		DX12Device(RHIInstance* instance, Surface* surface) : RHIDevice(instance, surface) {}
		void* GetHandle() const override { return nullptr; }
		void DeviceWaitIdle() const override {}
		void GraphicQueueWaitIdle() const override {}
		RHIFactory* GetFactory() const override { return nullptr; }
		GPUPipelineManager* GetGPUPipelineManager() const override { return nullptr; }
		DescriptorPool* GetDescriptorPool() const override { return nullptr; }
		RHIGpuTicket Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) override { (void)commandBuffer; (void)frameIndex; return 0; }
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
	};

}

extern "C" RHI_DX12_DLL ArisenEngine::RHI::RHIDevice * CreateDevice();


