#pragma once
#include <atomic>
#include <cstdint>

namespace ArisenEngine::Threadable::Containers
{
    /**
     * @brief A lock-free stack for UInt32 values, specifically designed for free-lists.
     * Uses a 64-bit tagged value to avoid the ABA problem.
     */
    class AtomicStack
    {
    public:
        // Sentinel value for empty stack
        static constexpr uint32_t InvalidIndex = 0xFFFFFFFF;

        AtomicStack() noexcept
        {
            TaggedIndex initial;
            initial.bits.index = InvalidIndex;
            initial.bits.tag = 0;
            _head.store(initial.raw, std::memory_order_relaxed);
        }

        /**
         * @brief Pushes a new index onto the stack.
         * @param index The index to push.
         * @param nextPtr A pointer to where the 'next' pointer for this index is stored.
         *                In a resource pool, this is usually entry[index].nextIndex.
         */
        void Push(uint32_t index, uint32_t* nextPtr) noexcept
        {
            TaggedIndex currentHead;
            currentHead.raw = _head.load(std::memory_order_relaxed);
            
            TaggedIndex newHead;
            newHead.bits.index = index;

            do {
                *nextPtr = currentHead.bits.index;
                newHead.bits.tag = currentHead.bits.tag + 1;
            } while (!_head.compare_exchange_weak(currentHead.raw, newHead.raw, 
                                                  std::memory_order_release, 
                                                  std::memory_order_relaxed));
        }

        /**
         * @brief Pops an index from the stack.
         * @param getNextFn A function that returns the 'next' index for a given index.
         *                  Usually: [](uint32_t idx) { return entries[idx].nextIndex; }
         * @return The popped index, or InvalidIndex if the stack is empty.
         */
        template<typename TGetNext>
        uint32_t Pop(TGetNext&& getNextFn) noexcept
        {
            TaggedIndex currentHead;
            currentHead.raw = _head.load(std::memory_order_acquire);

            while (currentHead.bits.index != InvalidIndex)
            {
                uint32_t next = getNextFn(currentHead.bits.index);
                
                TaggedIndex newHead;
                newHead.bits.index = next;
                newHead.bits.tag = currentHead.bits.tag + 1;

                if (_head.compare_exchange_weak(currentHead.raw, newHead.raw,
                                                std::memory_order_acq_rel,
                                                std::memory_order_acquire))
                {
                    return currentHead.bits.index;
                }
                // currentHead is updated on failure by compare_exchange_weak
            }

            return InvalidIndex;
        }

    private:
        union TaggedIndex
        {
            struct
            {
                uint32_t index;
                uint32_t tag;
            } bits;
            uint64_t raw;
        };

        static_assert(sizeof(TaggedIndex) == sizeof(uint64_t), "TaggedIndex size mismatch");
        std::atomic<uint64_t> _head;
    };
}
