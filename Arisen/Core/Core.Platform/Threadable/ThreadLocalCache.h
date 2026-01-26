#pragma once

namespace ArisenEngine
{
    /**
     * @brief A simple one-slot thread-local cache to optimize lookups for per-instance thread-local data.
     * 
     * @tparam TKey   Typically the type of the owner instance (e.g., RHIVkCommandBufferPool).
     * @tparam TValue The data to be cached (e.g., VkCommandPool).
     * @tparam Tag    An optional type tag to allow multiple independent caches for the same TKey/TValue pair.
     * 
     * @note This is a "last-seen" optimization. If multiple threads or instances compete, the cache 
     *       is simply overwritten. The Tag ensures that different systems do not collide.
     */
    template <typename TKey, typename TValue, typename Tag = void>
    class ThreadLocalCache
    {
    public:
        /**
         * @brief Attempt to retrieve the cached value for a specific key.
         * @return true if the key matches the cached slot, false otherwise.
         */
        static inline bool Get(const TKey* key, TValue& outValue)
        {
            if (s_LastKey == key)
            {
                outValue = s_LastValue;
                return true;
            }
            return false;
        }

        /**
         * @brief Update the cache slot with a new key-value pair.
         */
        static inline void Set(const TKey* key, TValue value)
        {
            s_LastKey = key;
            s_LastValue = value;
        }

        /**
         * @brief Explicitly clear the cache slot.
         */
        static inline void Clear()
        {
            s_LastKey = nullptr;
            s_LastValue = TValue{};
        }

    private:
        static thread_local const TKey* s_LastKey;
        static thread_local TValue s_LastValue;
    };

    template <typename TKey, typename TValue, typename Tag>
    thread_local const TKey* ThreadLocalCache<TKey, TValue, Tag>::s_LastKey = nullptr;

    template <typename TKey, typename TValue, typename Tag>
    thread_local TValue ThreadLocalCache<TKey, TValue, Tag>::s_LastValue = TValue{};
}
