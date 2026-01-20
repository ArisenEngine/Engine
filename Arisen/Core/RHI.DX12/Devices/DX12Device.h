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
		void Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) override { (void)commandBuffer; (void)frameIndex; }
		UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override { (void)typeFilter; (void)properties; return 0; }
		void SetResolution(UInt32 width, UInt32 height) override { (void)width; (void)height; }
		UInt32 GetMaxFramesInFlight() const override { return 1; }
	};

}

extern "C" RHI_DX12_DLL ArisenEngine::RHI::RHIDevice * CreateDevice();


