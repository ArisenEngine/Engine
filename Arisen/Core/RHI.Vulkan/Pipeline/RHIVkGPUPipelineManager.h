#pragma once
#include <vulkan/vulkan_core.h>
#include "RenderPass/RHIVkGPURenderPass.h"
#include "RHI/Pipeline/RHIPipelineCache.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkGPUPipelineManager final : public RHIPipelineCache
    {

    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipelineManager);
        RHIVkGPUPipelineManager(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkGPUPipelineManager() noexcept override;

        RHIPipelineHandle GetGraphicsPipeline(RHIPipelineState* pso) override;
        std::unique_ptr<RHIPipelineState> GetPipelineState() override;
    private:
        
        RHIVkDevice* m_Device;
        Containers::Map<UInt32, std::unique_ptr<RHIPipeline>> m_GPUPipelines;
        Containers::Map<UInt32, RHIPipelineHandle> m_PipelineHandles;

    };
}




