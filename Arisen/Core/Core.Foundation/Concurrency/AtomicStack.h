#pragma once
#include <atomic>
#include "../Base/PrimitiveTypes.h"

namespace ArisenEngine::Concurrency::Containers
{
    /**
     * @brief A lock-free, intrusive index-based stack.
     * Used for resource pools where indices are stored in a free-list.
     */
    class AtomicStack
    {
    public:
        static constexpr uint32_t InvalidIndex = 0xFFFFFFFF;

        AtomicStack() : m_Head(InvalidIndex)
        {
        }

        void Push(uint32_t index, uint32_t* nextPtr)
        {
            uint32_t oldHead = m_Head.load(std::memory_order_relaxed);
            do
            {
                *nextPtr = oldHead;
            }
            while (!m_Head.compare_exchange_weak(oldHead, index,
                                                 std::memory_order_release,
                                                 std::memory_order_relaxed));
        }

        template <typename TGetNext>
        uint32_t Pop(TGetNext&& getNext)
        {
            uint32_t oldHead = m_Head.load(std::memory_order_acquire);
            while (oldHead != InvalidIndex)
            {
                uint32_t next = getNext(oldHead);
                if (m_Head.compare_exchange_weak(oldHead, next,
                                                 std::memory_order_release,
                                                 std::memory_order_acquire))
                {
                    return oldHead;
                }
            }
            return InvalidIndex;
        }

    private:
        std::atomic<uint32_t> m_Head;
    };
}
