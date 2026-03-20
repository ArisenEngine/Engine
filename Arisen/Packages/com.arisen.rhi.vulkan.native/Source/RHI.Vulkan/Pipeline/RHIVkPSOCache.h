#pragma once
#include <vulkan/vulkan_core.h>
#include <shared_mutex>
#include <unordered_map>
#include "Base/FoundationMinimal.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    struct RHIVkPSOCacheKey
    {
        UInt32 psoHash;
        VkRenderPass renderPass;
        UInt32 subpassIndex;

        bool operator==(const RHIVkPSOCacheKey& other) const
        {
            return psoHash == other.psoHash && renderPass == other.renderPass && subpassIndex == other.subpassIndex;
        }
    };

    struct RHIVkPSOCacheKeyHash
    {
        std::size_t operator()(const RHIVkPSOCacheKey& key) const
        {
            std::size_t h1 = std::hash<UInt32>{}(key.psoHash);
            std::size_t h2 = std::hash<void*>{}(reinterpret_cast<void*>(key.renderPass));
            std::size_t h3 = std::hash<UInt32>{}(key.subpassIndex);
            return h1 ^ (h2 << 1) ^ (h3 << 2);
        }
    };

    class RHIVkPSOCache
    {
    public:
        RHIVkPSOCache(RHIVkDevice* device);
        ~RHIVkPSOCache();

        VkPipeline GetPipeline(const RHIVkPSOCacheKey& key);
        void StorePipeline(const RHIVkPSOCacheKey& key, VkPipeline pipeline);

        VkPipelineLayout GetLayout(UInt32 psoHash);
        void StoreLayout(UInt32 psoHash, VkPipelineLayout layout);

        void Clear();

    private:
        RHIVkDevice* m_Device;
        VkDevice m_VkDevice;

        mutable std::shared_mutex m_Mutex;
        std::unordered_map<RHIVkPSOCacheKey, VkPipeline, RHIVkPSOCacheKeyHash> m_Pipelines;
        std::unordered_map<UInt32, VkPipelineLayout> m_Layouts;
    };
}
