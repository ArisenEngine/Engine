#pragma once
#include <vulkan/vulkan_core.h>
#include "RHIVkGPURenderPass.h"
#include "RHI/Program/GPUPipelineManager.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkGPUPipelineManager final : public GPUPipelineManager
    {

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipelineManager);
        RHIVkGPUPipelineManager(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkGPUPipelineManager() noexcept override;

        GPUPipeline* GetGraphicsPipeline(GPUPipelineStateObject* pso) override;
        std::unique_ptr<GPUPipelineStateObject> GetPipelineState() override;
    private:
        
        RHIVkDevice* m_Device;
        Containers::Map<UInt32, std::unique_ptr<GPUPipeline>> m_GPUPipelines;

    };
}
