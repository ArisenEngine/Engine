#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../Common.h"
#include <optional>

#include "../Program/RHIVkGPUPipeline.h"
#include "../Program/RHIVkGPUProgram.h"

#include "Logger/Logger.h"

namespace NebulaEngine::RHI
{
    class RHIVkDevice final: public Device
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkDevice)
        ~RHIVkDevice() noexcept override;
        void* GetHandle() const override { return m_VkDevice; }
        RHIVkDevice(Instance* instance, VkQueue graphicQueue, VkQueue presentQueue, VkDevice device);

        void DeviceWaitIdle() const override;
        u32 CreateGPUProgram() override;
        void DestroyGPUProgram(u32 programId) override;
        bool AttachProgramByteCode(u32 programId, GPUProgramDesc&& desc) override;
    private:

        friend class RHIVkInstance;
        
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        RHIVkGPUPipeline* m_GPUPipeline;
        Containers::Map<u32, std::unique_ptr<RHIVkGPUProgram>> m_GPUPrograms;
    };
}
