#include "PipelineExports.h"
#include "../../Core/Core.Infra/RHI/Program/GPUPipelineManager.h"

using namespace ArisenEngine;

extern "C" ENGINE_DLL RHI_PipelineManagerHandle RHI_Device_GetPipelineManager(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return nullptr;
    return reinterpret_cast<RHI_PipelineManagerHandle>(dev->GetGPUPipelineManager());
}

extern "C" ENGINE_DLL RHI_PSOHandle RHI_PipelineManager_CreatePSO(RHI_PipelineManagerHandle pm)
{
    auto* mgr = reinterpret_cast<RHI::GPUPipelineManager*>(pm);
    if (mgr == nullptr) return nullptr;
    auto up = mgr->GetPipelineState();
    return reinterpret_cast<RHI_PSOHandle>(up.release());
}

extern "C" ENGINE_DLL void RHI_PSO_AddProgram(RHI_PSOHandle pso, unsigned int programId)
{
    auto* s = reinterpret_cast<RHI::GPUPipelineStateObject*>(pso);
    if (s == nullptr) return;
    s->AddProgram(programId);
}

extern "C" ENGINE_DLL void RHI_PSO_ClearPrograms(RHI_PSOHandle pso)
{
    auto* s = reinterpret_cast<RHI::GPUPipelineStateObject*>(pso);
    if (s == nullptr) return;
    s->ClearAllPrograms();
}

extern "C" ENGINE_DLL RHI_PipelineHandle RHI_PipelineManager_GetGraphicsPipeline(RHI_PipelineManagerHandle pm, RHI_PSOHandle pso)
{
    auto* mgr = reinterpret_cast<RHI::GPUPipelineManager*>(pm);
    auto* s = reinterpret_cast<RHI::GPUPipelineStateObject*>(pso);
    if (mgr == nullptr || s == nullptr) return nullptr;
    return reinterpret_cast<RHI_PipelineHandle>(mgr->GetGraphicsPipeline(s));
}

extern "C" ENGINE_DLL RHI_RenderPassHandle RHI_Device_GetRenderPass(RHI_DeviceHandle device)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    if (dev == nullptr) return nullptr;
    auto sp = dev->GetRenderPass();
    return reinterpret_cast<RHI_RenderPassHandle>(sp.get());
}

extern "C" ENGINE_DLL void RHI_Device_ReleaseRenderPass(RHI_DeviceHandle device, RHI_RenderPassHandle rp)
{
    auto* dev = reinterpret_cast<RHI::Device*>(device);
    auto* r = reinterpret_cast<RHI::GPURenderPass*>(rp);
    if (dev == nullptr || r == nullptr) return;
    std::shared_ptr<RHI::GPURenderPass> sp(r, [](RHI::GPURenderPass*){});
    dev->ReleaseRenderPass(sp);
}


