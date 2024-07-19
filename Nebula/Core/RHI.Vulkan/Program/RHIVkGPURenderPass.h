#pragma once
#include <vulkan/vulkan_core.h>

#include "Common/CommandHeaders.h"
#include "RHI/Program/GPURenderPass.h"

namespace NebulaEngine::RHI
{
    class RHIVkGPURenderPass final : public GPURenderPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPURenderPass)
        RHIVkGPURenderPass(VkDevice device);
        ~RHIVkGPURenderPass() noexcept override;

    private:
        VkRenderPass m_VkRenderPass;
        VkDevice m_VkDevice;
    };
}
