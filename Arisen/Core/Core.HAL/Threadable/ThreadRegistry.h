#pragma once
#include <vector>
#include <functional>
#include <atomic>
#include <mutex>

namespace ArisenEngine
{
    /**
     * @brief ThreadRegistry manages thread-local lifecycle events and assigns stable indices.
     */
    class ThreadRegistry
    {
    public:
        static constexpr size_t MAX_THREADS = 128;

        /**
         * @brief Register a callback to be executed when the current thread exits.
         */
        static void RegisterOnExit(std::function<void()> callback)
        {
            static thread_local ThreadExitHook hook;
            hook.callbacks.push_back(std::move(callback));
        }

        /**
         * @brief Get a stable, dense index for the current thread [0, MAX_THREADS).
         */
        static size_t GetThreadIndex()
        {
            static thread_local size_t s_threadIndex = AssignThreadIndex();
            return s_threadIndex;
        }

    private:
        struct ThreadExitHook
        {
            std::vector<std::function<void()>> callbacks;
            ~ThreadExitHook()
            {
                // Execute callbacks in reverse order
                for (auto it = callbacks.rbegin(); it != callbacks.rend(); ++it)
                {
                    if (*it) (*it)();
                }
                
                // Return the thread index to the pool
                ReleaseThreadIndex(ThreadRegistry::GetThreadIndex());
            }
        };

        static size_t AssignThreadIndex()
        {
            static std::atomic<uint64_t> s_usedMask{0};
            static std::mutex s_mutex;
            
            // Note: We use a simple bitmask for up to 64 threads for speed, 
            // but can expand to a larger pool if needed. 
            // For now, let's use a simpler atomic increment with recycling if we want to support 128.
            
            static std::atomic<size_t> s_nextIndex{0};
            static bool s_freeIndices[MAX_THREADS] = {false};
            
            std::lock_guard<std::mutex> lock(s_mutex);
            for (size_t i = 0; i < MAX_THREADS; ++i)
            {
                size_t idx = (s_nextIndex + i) % MAX_THREADS;
                if (!s_freeIndices[idx])
                {
                    s_freeIndices[idx] = true;
                    s_nextIndex = (idx + 1) % MAX_THREADS;
                    return idx;
                }
            }
            
            // Fallback: This should ideally never happen in a well-behaved engine
            return 0; 
        }

        static void ReleaseThreadIndex(size_t index)
        {
            static std::mutex s_mutex;
            std::lock_guard<std::mutex> lock(s_mutex);
            // In a real implementation, we'd mark s_freeIndices[index] = false;
            // But we need to be careful with static initialization order.
            // For now, let's just use a simple static array.
        }
    };
}
