#pragma once
#include "Pipeline/RHIVkGPUPipelineManager.h"
#include "RHI/Pipeline/RHIPipeline.h"
#include "RHI/RenderPass/RHISubPass.h"

namespace ArisenEngine::RHI
{
    class RHIPipelineState;

    class RHIVkGPUPipeline final : public RHIPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipeline)
        ~RHIVkGPUPipeline() noexcept override;
        RHIVkGPUPipeline(RHIVkDevice* device, RHIPipelineState* pipelineStateObject, UInt32 maxFramesInFlight);
        void* GetGraphicsPipeline(UInt32 frameIndex) override;
        void* GetComputePipeline(UInt32 frameIndex) override;

        void AllocGraphicPipeline(UInt32 frameIndex, RHISubPass* subPass) override;
        void AllocComputePipeline(UInt32 frameIndex) override;
        void AllocRayTracingPipeline(UInt32 frameIndex) override;

        const EPipelineBindPoint GetBindPoint() const override;
        void BindPipelineStateObject(RHIPipelineState* pso) override;

        RHIPipelineState* GetPipelineStateObject() const override
        {
            return m_PipelineStateObject;
        }

        VkPipelineLayout GetPipelineLayout(UInt32 frameIndex) const;
        VkPipeline GetVkPipeline(UInt32 frameIndex) const;

    private:
        void FreePipelineLayout(UInt32 frameIndex);
        void FreePipeline(UInt32 frameIndex);

        void FreeAllPipelineLayouts();
        void FreeAllPipelines();

        // device
        VkDevice m_VkDevice;
        RHIVkDevice* m_Device;

        // subPass
        RHISubPass* m_SubPass;

        // graphics pipeline
        RHIPipelineState* m_PipelineStateObject;
    };
}
