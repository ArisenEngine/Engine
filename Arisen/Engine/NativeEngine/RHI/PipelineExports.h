#pragma once
#include "EngineCommon.h"
#include "../../Core/Core.Infra/RHI/Program/GPURenderPass.h"
#include "../../Core/Core.Infra/RHI/Program/GPUPipeline.h"
#include "../../Core/Core.Infra/RHI/Program/GPUPipelineStateObject.h"

typedef void* RHI_DeviceHandle;
typedef void* RHI_PipelineManagerHandle;
typedef void* RHI_PipelineHandle;
typedef void* RHI_RenderPassHandle;
typedef void* RHI_PSOHandle;

extern "C" ENGINE_DLL RHI_PipelineManagerHandle RHI_Device_GetPipelineManager(RHI_DeviceHandle device);
extern "C" ENGINE_DLL RHI_PSOHandle RHI_PipelineManager_CreatePSO(RHI_PipelineManagerHandle pm);
extern "C" ENGINE_DLL void RHI_PSO_AddProgram(RHI_PSOHandle pso, unsigned int programId);
extern "C" ENGINE_DLL void RHI_PSO_ClearPrograms(RHI_PSOHandle pso);
extern "C" ENGINE_DLL RHI_PipelineHandle RHI_PipelineManager_GetGraphicsPipeline(RHI_PipelineManagerHandle pm, RHI_PSOHandle pso);

extern "C" ENGINE_DLL RHI_RenderPassHandle RHI_Device_GetRenderPass(RHI_DeviceHandle device);
extern "C" ENGINE_DLL void RHI_Device_ReleaseRenderPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp);


