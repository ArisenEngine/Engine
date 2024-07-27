#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../Common.h"
#include <optional>

#include "../CommandBuffer/RHIVkCommandBufferPool.h"
#include "../Program/RHIVkGPUPipeline.h"
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
        void DestroyGPUProgram(u32 programId) override;
        bool AttachProgramByteCode(u32 programId, GPUProgramDesc&& desc) override;

        u32 CreateCommandBufferPool() override;
        RHICommandBufferPool*  GetCommandBufferPool(u32 id) override;
        
    private:

        friend class RHIVkInstance;
        
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        RHIVkGPUPipeline* m_GPUPipeline;
        Containers::Map<u32, std::unique_ptr<RHIVkGPUProgram>> m_GPUPrograms;
        Containers::Map<u32, std::unique_ptr<RHIVkCommandBufferPool>> m_CommandBufferPools;

    };
}
