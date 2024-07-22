#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Devices/Device.h"
#include "../Surfaces/RHIVkSurface.h"
#include "../Common.h"
#include <optional>

#include "../Program/RHIVkGPUPipeline.h"
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
    private:

        friend class RHIVkInstance;
        
        VkQueue m_VkGraphicQueue;
        VkQueue m_VkPresentQueue;
        VkDevice m_VkDevice;
        RHIVkGPUPipeline* m_GPUPipeline;
        
    };
}
