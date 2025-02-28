#include "RHIVkGPUPipelineManager.h"
#include "RHIVkGPUPipeline.h"
#include "RHIVkGPUPipelineStateObject.h"
#include "../Devices/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "RHI/Program/GPUPipelineStateObject.h"
#include "RHI/Program/GPUSubPass.h"

ArisenEngine::RHI::RHIVkGPUPipelineManager::RHIVkGPUPipelineManager(RHIVkDevice* device, UInt32 maxFramesInFlight): GPUPipelineManager(maxFramesInFlight),
m_Device(device)
{
    
}

ArisenEngine::RHI::RHIVkGPUPipelineManager::~RHIVkGPUPipelineManager() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineManager::~RHIVkGPUPipelineManager]: ~RHIVkGPUPipelineManager");
    m_GPUPipelines.clear();
}

ArisenEngine::RHI::GPUPipeline* ArisenEngine::RHI::RHIVkGPUPipelineManager::GetGraphicsPipeline(GPUPipelineStateObject* pso)
{
    auto hash = pso->GetHash();
    if (!m_GPUPipelines.contains(hash))
    {
        m_GPUPipelines.insert({hash, std::make_unique<RHIVkGPUPipeline>(m_Device, pso, m_MaxFramesInFlight)});
    }
    else
    {
        m_GPUPipelines[hash].get()->BindPipelineStateObject(pso);
    }
    
    return m_GPUPipelines[hash].get();
}

std::unique_ptr<ArisenEngine::RHI::GPUPipelineStateObject> ArisenEngine::RHI::RHIVkGPUPipelineManager::GetPipelineState()
{
    return std::make_unique<RHIVkGPUPipelineStateObject>(m_Device);
}

