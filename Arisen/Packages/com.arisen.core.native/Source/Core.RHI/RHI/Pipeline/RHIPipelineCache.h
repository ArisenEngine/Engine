#pragma once
#include "RHIPipeline.h"
#include "../Handles/RHIHandle.h"
#include "RHIShaderProgram.h"
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Pipeline/EDynamicState.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHIPipelineState;

    // TODO
    struct SpecializationInfoDesc
    {
    };

    struct PipelineShaderStageDesc
    {
        UInt32 flag;
        EShaderStage stage;
        RHIShaderProgram& program;
        std::optional<SpecializationInfoDesc> specializationInfo;
    };

    class RHI_DLL RHIPipelineCache
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIPipelineCache)
        RHIPipelineCache(UInt32 maxFramesInFlight);
        virtual ~RHIPipelineCache() noexcept = default;
        virtual RHIPipelineHandle GetGraphicsPipeline(RHIPipelineState* pso) = 0;
        virtual RHIPipelineHandle GetComputePipeline(RHIPipelineState* pso) = 0;
        virtual RHIPipelineHandle GetRayTracingPipeline(RHIPipelineState* pso) = 0;

        virtual std::unique_ptr<RHIPipelineState> GetPipelineState() = 0;

    protected:
        UInt32 m_MaxFramesInFlight;
    };
}
