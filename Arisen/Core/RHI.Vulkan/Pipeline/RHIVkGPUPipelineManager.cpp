#include "Pipeline/RHIVkGPUPipelineManager.h"
#include "Pipeline/RHIVkGPUPipeline.h"
#include "Profiler.h"
#include "Pipeline/RHIVkGPUPipelineStateObject.h"
#include "Core/RHIVkDevice.h"
#include "Logger/Logger.h"
#include "RHI/Pipeline/RHIPipeline.h"
#include "RHI/Pipeline/RHIPipelineState.h"
#include "RHI/RenderPass/RHISubPass.h"

#include <fstream>
#include "PlatformPath.h"
#include "Pipeline/RHIVkPSOCache.h"

ArisenEngine::RHI::RHIVkGPUPipelineManager::RHIVkGPUPipelineManager(RHIVkDevice* device, UInt32 maxFramesInFlight):
    RHIPipelineCache(maxFramesInFlight),
    m_Device(device)
{
    m_PSOCache = std::make_unique<RHIVkPSOCache>(m_Device);
    LoadPipelineCache();
}

ArisenEngine::RHI::RHIVkGPUPipelineManager::~RHIVkGPUPipelineManager() noexcept
{
    LOG_DEBUG("[RHIVkGPUPipelineManager::~RHIVkGPUPipelineManager]: ~RHIVkGPUPipelineManager");

    SavePipelineCache();

    if (m_VkPipelineCache != VK_NULL_HANDLE)
    {
        vkDestroyPipelineCache(static_cast<VkDevice>(m_Device->GetHandle()), m_VkPipelineCache, nullptr);
    }

    // Release handles from pool
    for (auto const& [hash, handle] : m_PipelineHandles)
    {
        m_Device->GetPipelinePool()->Deallocate(handle);
    }
    m_GPUPipelines.clear();
    m_PipelineHandles.clear();
}

ArisenEngine::RHI::RHIPipelineHandle ArisenEngine::RHI::RHIVkGPUPipelineManager::GetGraphicsPipeline(
    RHIPipelineState* pso)
{
    ARISEN_PROFILE_ZONE("RHI::GetGraphicsPipeline");
    static_cast<RHIVkGPUPipelineStateObject*>(pso)->BuildDescriptorSetLayout();
    auto hash = pso->GetHash();
    if (!m_GPUPipelines.contains(hash))
    {
        auto pipeline = std::make_unique<RHIVkGPUPipeline>(m_Device, pso, m_MaxFramesInFlight);
        auto* rawPtr = pipeline.get();
        m_GPUPipelines.emplace(hash, std::move(pipeline));

        // Not using deferred destroy here as Manager owns the unique_ptr and pool just stores observation
        // Actually, if we use handles, we should be careful about ownership.
        // For now, let's say the Pool observation is valid as long as m_GPUPipelines has it.
        auto handle = m_Device->GetPipelinePool()->Allocate([rawPtr](RHIVkPipelinePoolItem* item)
        {
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

ArisenEngine::RHI::RHIPipelineHandle ArisenEngine::RHI::RHIVkGPUPipelineManager::GetComputePipeline(
    RHIPipelineState* pso)
{
    // Implementation is same as Graphics for and based on PSO hash
    return GetGraphicsPipeline(pso);
}

ArisenEngine::RHI::RHIPipelineHandle ArisenEngine::RHI::RHIVkGPUPipelineManager::GetRayTracingPipeline(
    RHIPipelineState* pso)
{
    ARISEN_PROFILE_ZONE("RHI::GetRayTracingPipeline");
    static_cast<RHIVkGPUPipelineStateObject*>(pso)->BuildDescriptorSetLayout();
    auto hash = pso->GetHash();
    if (!m_GPUPipelines.contains(hash))
    {
        auto pipeline = std::make_unique<RHIVkGPUPipeline>(m_Device, pso, m_MaxFramesInFlight);
        auto* rawPtr = pipeline.get();
        m_GPUPipelines.emplace(hash, std::move(pipeline));

        auto handle = m_Device->GetPipelinePool()->Allocate([rawPtr](RHIVkPipelinePoolItem* item)
        {
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

std::unique_ptr<ArisenEngine::RHI::RHIPipelineState> ArisenEngine::RHI::RHIVkGPUPipelineManager::GetPipelineState()
{
    return std::make_unique<RHIVkGPUPipelineStateObject>(m_Device);
}

void ArisenEngine::RHI::RHIVkGPUPipelineManager::LoadPipelineCache()
{
    ARISEN_PROFILE_ZONE("RHI::LoadPipelineCache");
    VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    Containers::Vector<char> cacheData;

    String cachePath = HAL::PlatformPath::GetExecutableDirectory() + "/" + m_PipelineCacheFileName;
    std::ifstream file(cachePath.c_str(), std::ios::binary | std::ios::ate);
    if (file.is_open())
    {
        std::streamsize size = file.tellg();
        file.seekg(0, std::ios::beg);
        cacheData.resize(size);
        if (file.read(cacheData.data(), size))
        {
            LOG_INFO("[RHIVkGPUPipelineManager]: Loaded PSO cache from disk (" + std::to_string(size) + " bytes)");
        }
        file.close();
    }

    VkPipelineCacheCreateInfo cacheCreateInfo{};
    cacheCreateInfo.sType = VK_STRUCTURE_TYPE_PIPELINE_CACHE_CREATE_INFO;
    cacheCreateInfo.initialDataSize = cacheData.size();
    cacheCreateInfo.pInitialData = cacheData.data();

    if (vkCreatePipelineCache(vkDevice, &cacheCreateInfo, nullptr, &m_VkPipelineCache) != VK_SUCCESS)
    {
        LOG_ERROR("[RHIVkGPUPipelineManager]: Failed to create pipeline cache!");
    }
}

void ArisenEngine::RHI::RHIVkGPUPipelineManager::SavePipelineCache()
{
    ARISEN_PROFILE_ZONE("RHI::SavePipelineCache");
    if (m_VkPipelineCache == VK_NULL_HANDLE) return;

    VkDevice vkDevice = static_cast<VkDevice>(m_Device->GetHandle());
    size_t cacheSize = 0;
    vkGetPipelineCacheData(vkDevice, m_VkPipelineCache, &cacheSize, nullptr);

    if (cacheSize > 0)
    {
        Containers::Vector<char> cacheData(cacheSize);
        if (vkGetPipelineCacheData(vkDevice, m_VkPipelineCache, &cacheSize, cacheData.data()) == VK_SUCCESS)
        {
            String cachePath = HAL::PlatformPath::GetExecutableDirectory() + "/" + m_PipelineCacheFileName;
            std::ofstream file(cachePath.c_str(), std::ios::binary);
            if (file.is_open())
            {
                file.write(cacheData.data(), cacheSize);
                file.close();
                LOG_INFO(
                    "[RHIVkGPUPipelineManager]: Saved PSO cache to disk (" + std::to_string(cacheSize) + " bytes)");
            }
        }
    }
}
