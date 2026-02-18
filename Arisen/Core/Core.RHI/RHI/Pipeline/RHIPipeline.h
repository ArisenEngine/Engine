#pragma once
#include "RHIPipelineState.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHISubPass;
    class RHI_DLL RHIPipeline
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIPipeline)

        explicit RHIPipeline(UInt32 maxFramesInFlight);
        virtual ~RHIPipeline() noexcept = default;

        // TODO(CppSharp-P0): GetGraphicsPipeline/GetComputePipeline 返回 void*，泄漏 VkPipeline/ID3D12PipelineState。
        // 方案A: 移至 RHIVkPipeline 后端类（推荐）— 上层仅通过 RHIPipelineHandle 引用管线。
        // 方案B: 返回一个 RHINativePipelineHandle POD 结构体封装。
        // 上层录制命令使用 BindPipeline(RHIPipelineHandle)，不应需要原生句柄。
        virtual void* GetGraphicsPipeline(UInt32 frameIndex) = 0;
        virtual void* GetComputePipeline(UInt32 frameIndex) = 0;
        
        virtual void AllocGraphicPipeline(UInt32 frameIndex, RHISubPass* subPass) = 0;
        virtual void AllocComputePipeline(UInt32 frameIndex) = 0;
        virtual void AllocRayTracingPipeline(UInt32 frameIndex) = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;
        
        virtual void BindPipelineStateObject(RHIPipelineState* pso) = 0;
        virtual RHIPipelineState* GetPipelineStateObject() const = 0;
    protected:
        UInt32 m_MaxFramesInFlight;
    };

}
