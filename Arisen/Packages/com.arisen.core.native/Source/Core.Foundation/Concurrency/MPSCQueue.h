#pragma once
#include "../Base/FoundationMinimal.h"
#include <atomic>

namespace ArisenEngine::Concurrency::Containers
{
    // Forward declaration
    class MPSCQueueNodePool;

    /**
     * @brief Intrusive node used by IntrusiveMPSCQueue.
     * To avoid frequent allocations, nodes should be managed via MPSCQueueNodePool.
     */
    class FOUNDATION_DLL MPSCQueueNode
    {
        friend class MPSCQueueNodePool;
        friend class IntrusiveMPSCQueue;

    public:
        MPSCQueueNode() noexcept : _next(nullptr)
        {
        }

        MPSCQueueNode* Next() const noexcept { return _next.load(std::memory_order_acquire); }

        // Link this node to the next one. Returns the next node.
        MPSCQueueNode* Link(MPSCQueueNode* next) noexcept
        {
            _next.store(next, std::memory_order_release);
            return next;
        }

        // Generic data storage for intrusive use.
        void* data[3] = {nullptr, nullptr, nullptr};

        // Helper to acquire/recycle from/to a pool.
        static MPSCQueueNode* Acquire(MPSCQueueNodePool& pool) noexcept;
        void Recycle(MPSCQueueNodePool& pool) noexcept;

    private:
        std::atomic<MPSCQueueNode*> _next;
    };

    /**
     * @brief A simple object pool for MPSCQueueNode.
     * Addresses the ABA problem using a lightweight lock for management operations (Acquire/Release).
     */
    class FOUNDATION_DLL MPSCQueueNodePool
    {
    public:
        MPSCQueueNodePool() noexcept : _freeHead(nullptr)
        {
        }

        ~MPSCQueueNodePool();
        NO_COPY_NO_MOVE(MPSCQueueNodePool)

        // Acquire a node from the pool, or allocate if empty
        MPSCQueueNode* Acquire() noexcept;
        // Return a node to the pool
        void Release(MPSCQueueNode* node) noexcept;
        // Preallocate N nodes into the pool
        void Preallocate(std::size_t count);

    private:
        std::atomic<MPSCQueueNode*> _freeHead;
        std::mutex _poolMutex; // Used to prevent ABA in lock-free style pop, or just simplify pool management.
    };

    // Global default pool accessor (Thread-safe)
    FOUNDATION_DLL MPSCQueueNodePool& GetGlobalMPSCQueueNodePool() noexcept;

    /**
     * @brief A Multi-Producer Single-Consumer (MPSC) intrusive lock-free queue.
     * Based on Vyukov's MPSC queue.
     */
    class FOUNDATION_DLL IntrusiveMPSCQueue
    {
    public:
        IntrusiveMPSCQueue() noexcept;
        ~IntrusiveMPSCQueue() = default;
        NO_COPY_NO_MOVE(IntrusiveMPSCQueue)

        // Producers: Push a node to the queue. Always non-blocking.
        void Enqueue(MPSCQueueNode* node) noexcept;

        // Consumer: Attempt to dequeue a node.
        // This version might spin if a producer is in the middle of an Enqueue.
        MPSCQueueNode* Dequeue() noexcept;

        // Consumer: Truly non-blocking attempt to dequeue.
        // Returns nullptr if empty OR if a producer is currently Enqueuing.
        MPSCQueueNode* TryDequeue() noexcept;

        bool Empty() const noexcept;

        // Batch dequeue
        std::size_t DequeueAll(MPSCQueueNode*& first, MPSCQueueNode*& last,
                               std::size_t maxCount = (std::size_t)-1) noexcept;

        void ConfigureSpin(unsigned maxSpins, bool yieldOnSpin) noexcept
        {
            _spinMax = maxSpins;
            _yieldOnSpin = yieldOnSpin;
        }

        void SetPauseHook(void (*hook)()) noexcept { _pauseHook = hook; }

        template <class F>
        void Drain(F&& fn)
        {
            for (MPSCQueueNode* n; (n = Dequeue()) != nullptr;)
            {
                fn(n);
            }
        }

    private:
        MPSCQueueNode _stub;
        std::atomic<MPSCQueueNode*> _head{nullptr};
        MPSCQueueNode* _tail{nullptr};

        unsigned _spinMax = 0;
        bool _yieldOnSpin = true;
        void (*_pauseHook)() = nullptr;
    };

    /**
     * @brief A templated wrapper around IntrusiveMPSCQueue for convenience.
     */
    template <typename T>
    class TMPSCQueue
    {
    public:
        TMPSCQueue() = default;

        ~TMPSCQueue()
        {
            T* item;
            while ((item = Dequeue()) != nullptr)
            {
                delete item;
            }
        }

        struct Node : public MPSCQueueNode
        {
            T value;

            Node(T&& v) : value(std::move(v))
            {
            }

            Node(const T& v) : value(v)
            {
            }
        };

        void Enqueue(T&& value)
        {
            Node* node = new Node(std::move(value));
            _internalQueue.Enqueue(node);
        }

        T* Dequeue()
        {
            Node* node = static_cast<Node*>(_internalQueue.Dequeue());
            if (!node) return nullptr;
            T* result = new T(std::move(node->value));
            delete node;
            return result;
        }

        // Note: TMPSCQueue as implemented here is less efficient because of node allocations.
        // For performance critical paths, use IntrusiveMPSCQueue directly.

    private:
        IntrusiveMPSCQueue _internalQueue;
    };
}
