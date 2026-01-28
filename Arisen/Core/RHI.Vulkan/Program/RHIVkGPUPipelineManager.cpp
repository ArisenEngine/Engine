#include "RHIVkGPUPipelineManager.h"
#include "RHIVkGPUPipeline.h"
#include "RHIVkGPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "RHI/Pipeline/GPUPipeline.h"
#include "RHI/Pipeline/GPUPipelineStateObject.h"
#include "RHI/Pipeline/GPUSubPass.h"

ArisenEngine::RHI::RHIVkGPUPipelineManager::RHIVkGPUPipelineManager(RHIVkDevice* device, UInt32 maxFramesInFlight): GPUPipelineManager(maxFramesInFlight),
m_Device(device)
{
    
}

ArisenEngine::RHI::RHIVkGPUPipelineManager::~RHIVkGPUPipelineManager() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineManager::~RHIVkGPUPipelineManager]: ~RHIVkGPUPipelineManager");
    // Release handles from pool
    for (auto const& [hash, handle] : m_PipelineHandles)
    {
        m_Device->GetPipelinePool()->Deallocate(handle);
    }
    m_GPUPipelines.clear();
    m_PipelineHandles.clear();
}

ArisenEngine::RHI::RHIPipelineHandle ArisenEngine::RHI::RHIVkGPUPipelineManager::GetGraphicsPipeline(GPUPipelineStateObject* pso)
{
    auto hash = pso->GetHash();
    if (!m_GPUPipelines.contains(hash))
    {
        auto pipeline = std::make_unique<RHIVkGPUPipeline>(m_Device, pso, m_MaxFramesInFlight);
        auto* rawPtr = pipeline.get();
        m_GPUPipelines.emplace(hash, std::move(pipeline));
        
        // Not using deferred destroy here as Manager owns the unique_ptr and pool just stores observation
        // Actually, if we use handles, we should be careful about ownership.
        // For now, let's say the Pool observation is valid as long as m_GPUPipelines has it.
        auto handle = m_Device->GetPipelinePool()->Allocate([rawPtr](RHIVkPipelinePoolItem* item) {
            *item = RHIVkPipelinePoolItem();
            item->pipeline = rawPtr;
        });
        m_PipelineHandles.emplace(hash, handle);
        return handle;
    }
    else
    {
        m_GPUPipelines[hash].get()->BindPipelineStateObject(pso);
        return m_PipelineHandles[hash];
    }
}

std::unique_ptr<ArisenEngine::RHI::GPUPipelineStateObject> ArisenEngine::RHI::RHIVkGPUPipelineManager::GetPipelineState()
{
    return std::make_unique<RHIVkGPUPipelineStateObject>(m_Device);
}

