#include "Pipeline/RHIVkPSOCache.h"
#include "Core/RHIVkDevice.h"
#include "Logger/Logger.h"

namespace ArisenEngine::RHI
{
    RHIVkPSOCache::RHIVkPSOCache(RHIVkDevice* device)
        : m_Device(device), m_VkDevice(static_cast<VkDevice>(device->GetHandle()))
    {
    }

    RHIVkPSOCache::~RHIVkPSOCache()
    {
        Clear();
    }

    VkPipeline RHIVkPSOCache::GetPipeline(const RHIVkPSOCacheKey& key)
    {
        std::shared_lock lock(m_Mutex);
        auto it = m_Pipelines.find(key);
        if (it != m_Pipelines.end())
        {
            return it->second;
        }
        return VK_NULL_HANDLE;
    }

    void RHIVkPSOCache::StorePipeline(const RHIVkPSOCacheKey& key, VkPipeline pipeline)
    {
        std::unique_lock lock(m_Mutex);
        m_Pipelines[key] = pipeline;
    }

    VkPipelineLayout RHIVkPSOCache::GetLayout(UInt32 psoHash)
    {
        std::shared_lock lock(m_Mutex);
        auto it = m_Layouts.find(psoHash);
        if (it != m_Layouts.end())
        {
            return it->second;
        }
        return VK_NULL_HANDLE;
    }

    void RHIVkPSOCache::StoreLayout(UInt32 psoHash, VkPipelineLayout layout)
    {
        std::unique_lock lock(m_Mutex);
        m_Layouts[psoHash] = layout;
    }

    void RHIVkPSOCache::Clear()
    {
        std::unique_lock lock(m_Mutex);
        for (auto& [key, pipeline] : m_Pipelines)
        {
            if (pipeline != VK_NULL_HANDLE)
            {
                vkDestroyPipeline(m_VkDevice, pipeline, nullptr);
            }
        }
        m_Pipelines.clear();

        for (auto& [hash, layout] : m_Layouts)
        {
            if (layout != VK_NULL_HANDLE)
            {
                vkDestroyPipelineLayout(m_VkDevice, layout, nullptr);
            }
        }
        m_Layouts.clear();

        LOG_DEBUG("[RHIVkPSOCache]: Cleared all cached pipelines and layouts.");
    }
}
