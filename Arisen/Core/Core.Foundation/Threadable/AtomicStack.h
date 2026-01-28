#pragma once
#include <atomic>
#include <cstdint>

/**
 * @namespace ArisenEngine::Threadable::Containers
 * @brief Contains thread-safe and lock-free container implementations.
 */
namespace ArisenEngine::Threadable::Containers
{
    /**
     * @brief A lock-free, single-link stack for 32-bit indices, specifically designed for high-performance free-lists.
     * 
     * ### Design Goals:
     * - **Lock-Free**: Uses atomic operations instead of mutexes to avoid context switching and priority inversion.
     * - **ABA Problem Mitigation**: Uses a 64-bit "Tagged Index" approach. Each modification increments a version tag, 
     *   ensuring that even if an index is popped and pushed back, the atomic state will have changed.
     * - **Zero Allocation**: Operates on pre-allocated external storage (indices).
     * 
     * ### Typical Usage:
     * In a resource pool, `AtomicStack` stores indices of available slots. The actual data is stored in a 
     * fixed-size array/vector, and the 'next' pointer is embedded in the slot's metadata.
     */
    class AtomicStack
    {
    public:
        /**
         * @brief Sentinel value indicating an empty stack or an invalid index.
         */
        static constexpr uint32_t InvalidIndex = 0xFFFFFFFF;

        /**
         * @brief Initializes the stack as empty.
         * The initial head has InvalidIndex and a tag of 0.
         */
        AtomicStack() noexcept
        {
            TaggedIndex initial;
            initial.bits.index = InvalidIndex;
            initial.bits.tag = 0;
            // Relaxed is fine here as we're in the constructor and no other thread should have access yet.
            _head.store(initial.raw, std::memory_order_relaxed);
        }

        /**
         * @brief Pushes a new index onto the top of the stack.
         * 
         * @param index The index to be added to the stack.
         * @param nextPtr A pointer to the memory location where this node's "next" index should be stored.
         *                In a pool, this is typically `&pool[index].nextIndex`.
         * 
         * ### Implementation Details:
         * Uses a Compare-And-Swap (CAS) loop. 
         * 1. Read the current head.
         * 2. Set the current index's next pointer to the current head's index.
         * 3. Attempt to set the head to the new index while incrementing the version tag.
         * 
         * ### Memory Ordering:
         * - **Release**: Ensures that the write to `*nextPtr` is visible to any thread that subsequently 
         *   pops this index using Acquire semantics.
         */
        void Push(uint32_t index, uint32_t* nextPtr) noexcept
        {
            TaggedIndex currentHead;
            // Initial load can be relaxed; the CAS loop will handle synchronization.
            currentHead.raw = _head.load(std::memory_order_relaxed);
            
            TaggedIndex newHead;
            newHead.bits.index = index;

            do {
                // Point this node to the current top of the stack
                *nextPtr = currentHead.bits.index;
                // Increment tag to prevent ABA problem
                newHead.bits.tag = currentHead.bits.tag + 1;
                
                // Try to swap the head. If it fails, currentHead is refreshed with the latest value.
            } while (!_head.compare_exchange_weak(currentHead.raw, newHead.raw, 
                                                   std::memory_order_release, 
                                                   std::memory_order_relaxed));
        }

        /**
         * @brief Pops an index from the top of the stack.
         * 
         * @tparam TGetNext A functional/lambda type: `uint32_t(uint32_t currentIdx)`.
         * @param getNextFn A function that returns the 'next' index for the given index.
         *                  Example: `[&](uint32_t idx) { return pool[idx].next; }`
         * @return The popped index, or `InvalidIndex` if the stack is empty.
         * 
         * ### Memory Ordering:
         * - **Acquire**: Ensures that if we pop an index, we see the `*nextPtr` write performed by 
         *   the thread that pushed it.
         * - **AcqRel**: The CAS requires both Acquire (to read the next pointer safely) and 
         *   Release (to update the head for others).
         */
        template<typename TGetNext>
        uint32_t Pop(TGetNext&& getNextFn) noexcept
        {
            TaggedIndex currentHead;
            // Acquire the current head to ensure we see the 'next' data if it was just pushed.
            currentHead.raw = _head.load(std::memory_order_acquire);

            while (currentHead.bits.index != InvalidIndex)
            {
                // Retrieve the next element in the stack. 
                // This is why we need Acquire: to ensure getNextFn reads valid data.
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
                // On failure, currentHead is updated with the new head value by CAS.
            }

            return InvalidIndex;
        }

    private:
        /**
         * @brief A composite 64-bit value containing a 32-bit index and a 32-bit version tag.
         * Packing them into 64 bits allows for a single atomic operation on most modern architectures.
         */
        union TaggedIndex
        {
            struct
            {
                uint32_t index; ///< The actual index value.
                uint32_t tag;   ///< Version tag to solve the ABA problem.
            } bits;
            uint64_t raw;      ///< Raw 64-bit representation for atomic operations.
        };

        static_assert(sizeof(TaggedIndex) == sizeof(uint64_t), "TaggedIndex must be exactly 64 bits.");
        
        /**
         * @brief The atomic head of the stack.
         */
        std::atomic<uint64_t> _head;
    };
}

