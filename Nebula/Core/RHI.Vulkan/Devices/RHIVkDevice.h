#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../Common.h"
#include <optional>

#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPUPipelineManager.h"
#include "../Program/RHIVkGPUProgram.h"

#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{
    class RHIVkCommandBufferPool;
    
    class RHIVkDevice final: public Device
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        RHIVkDevice(Instance* instance, Surface* surface, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device);

        void DeviceWaitIdle() const override;
        u32 CreateGPUProgram() override;
        GPUProgram* GetGPUProgram(u32 programId) override;
        void DestroyGPUProgram(u32 programId) override;
        bool AttachProgramByteCode(u32 programId, GPUProgramDesc&& desc) override;

        u32 CreateCommandBufferPool() override;
        RHICommandBufferPool* GetCommandBufferPool(u32 id) override;

        std::shared_ptr<GPURenderPass> GetRenderPass() override;
        void ReleaseRenderPass(std::shared_ptr<GPURenderPass> renderPass) override;

        std::shared_ptr<FrameBuffer> GetFrameBuffer() override;
        void ReleaseFrameBuffer(std::shared_ptr<FrameBuffer> frameBuffer) override;

        GPUPipelineManager* GetGPUPipelineManager() const override
        {
            return m_GPUPipelineManager;
        }

        void Submit(RHICommandBuffer* commandBuffer, u32 frameIndex) override;
    private:

        friend class RHIVkInstance;
        RHIVkGPUPipelineManager* m_GPUPipelineManager;
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        
        Containers::Map<u32, std::unique_ptr<RHIVkGPUProgram>> m_GPUPrograms;
        Containers::Map<u32, std::unique_ptr<RHIVkCommandBufferPool>> m_CommandBufferPools;
        Containers::Vector<std::shared_ptr<GPURenderPass>> m_RenderPasses;
        Containers::Vector<std::shared_ptr<FrameBuffer>> m_FrameBuffers;

    };
}
