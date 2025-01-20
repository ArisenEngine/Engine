#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPUPipelineManager.h"
#include "../Program/RHIVkGPUProgram.h"
#include "../Program/RHIVkDescriptorPool.h"

namespace NebulaEngine::RHI
{
    class RHIVkCommandBufferPool;
}

namespace NebulaEngine::RHI
{
    class RHIVkDevice final: public Device
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        RHIVkDevice(Instance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device, VkPhysicalDeviceMemoryProperties memoryProperties);

        void DeviceWaitIdle() const override;
        void GraphicQueueWaitIdle() const override;
        UInt32 CreateGPUProgram() override;
        GPUProgram* GetGPUProgram(UInt32 programId) override;
        void DestroyGPUProgram(UInt32 programId) override;
        bool AttachProgramByteCode(UInt32 programId, GPUProgramDesc&& desc) override;

        UInt32 CreateCommandBufferPool() override;
        RHICommandBufferPool* GetCommandBufferPool(UInt32 id) override;

        std::shared_ptr<GPURenderPass> GetRenderPass() override;
        void ReleaseRenderPass(std::shared_ptr<GPURenderPass> renderPass) override;

        std::shared_ptr<FrameBuffer> GetFrameBuffer() override;
        void ReleaseFrameBuffer(std::shared_ptr<FrameBuffer> frameBuffer) override;

        std::shared_ptr<BufferHandle> GetBufferHandle() override;
        void ReleaseBufferHandle(std::shared_ptr<BufferHandle> bufferHandle) override;

        GPUPipelineManager* GetGPUPipelineManager() const override
        {
            return m_GPUPipelineManager;
        }

        DescriptorPool* GetDescriptorPool() const override
        {
            return m_DescriptorPool;
        }

        void Submit(RHICommandBuffer* commandBuffer, UInt32 frameIndex) override;
        UInt32 FindMemoryType(UInt32 typeFilter, UInt32 properties) override;

        void SetResolution(UInt32 width, UInt32 height) override;
    private:

        friend class RHIVkInstance;
        RHIVkGPUPipelineManager* m_GPUPipelineManager;
        RHIVkDescriptorPool* m_DescriptorPool;
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        VkPhysicalDeviceMemoryProperties m_VkPhysicalDeviceMemoryProperties;
        
        Containers::Map<UInt32, std::unique_ptr<RHIVkGPUProgram>> m_GPUPrograms;
        Containers::Map<UInt32, std::unique_ptr<RHIVkCommandBufferPool>> m_CommandBufferPools;
        Containers::Vector<std::shared_ptr<GPURenderPass>> m_RenderPasses;
        Containers::Vector<std::shared_ptr<FrameBuffer>> m_FrameBuffers;
        Containers::Vector<std::shared_ptr<BufferHandle>> m_BufferHandles;

    };
}
