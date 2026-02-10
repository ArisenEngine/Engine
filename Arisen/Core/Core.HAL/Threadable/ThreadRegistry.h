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

        struct RegistryState {
            bool usedIndices[MAX_THREADS] = {false};
            size_t lastAssigned = 0;
            std::mutex mutex;
        };

        static RegistryState& GetState() {
            static RegistryState state;
            return state;
        }

        static size_t AssignThreadIndex()
        {
            auto& state = GetState();
            std::lock_guard<std::mutex> lock(state.mutex);
            for (size_t i = 0; i < MAX_THREADS; ++i)
            {
                size_t idx = (state.lastAssigned + i) % MAX_THREADS;
                if (!state.usedIndices[idx])
                {
                    state.usedIndices[idx] = true;
                    state.lastAssigned = (idx + 1) % MAX_THREADS;
                    return idx;
                }
            }
            
            // Fallback: This should ideally never happen in a well-behaved engine
            return 0; 
        }

        static void ReleaseThreadIndex(size_t index)
        {
            if (index >= MAX_THREADS) return;
            auto& state = GetState();
            std::lock_guard<std::mutex> lock(state.mutex);
            state.usedIndices[index] = false;
        }
    };
}
