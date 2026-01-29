#include "MPSCQueue.h"
#include <mutex>

#if defined(_WIN32)
#include <windows.h>
#endif

namespace ArisenEngine::Concurrency::Containers {
    
    MPSCQueueNodePool& GetGlobalMPSCQueueNodePool() noexcept
    {
        static MPSCQueueNodePool s_Pool;
        return s_Pool;
    }

    MPSCQueueNode* MPSCQueueNode::Acquire(MPSCQueueNodePool& pool) noexcept { return pool.Acquire(); }
    void MPSCQueueNode::Recycle(MPSCQueueNodePool& pool) noexcept { pool.Release(this); }

    MPSCQueueNodePool::~MPSCQueueNodePool()
    {
        std::lock_guard<std::mutex> lock(_poolMutex);
        MPSCQueueNode* node = _freeHead.load(std::memory_order_relaxed);
        while (node)
        {
            MPSCQueueNode* next = node->_next.load(std::memory_order_relaxed);
            delete node;
            node = next;
        }
        _freeHead.store(nullptr, std::memory_order_relaxed);
    }

    MPSCQueueNode* MPSCQueueNodePool::Acquire() noexcept
    {
        std::lock_guard<std::mutex> lock(_poolMutex);
        MPSCQueueNode* head = _freeHead.load(std::memory_order_relaxed);
        if (head)
        {
            _freeHead.store(head->_next.load(std::memory_order_relaxed), std::memory_order_relaxed);
            head->_next.store(nullptr, std::memory_order_relaxed);
            head->data[0] = head->data[1] = head->data[2] = nullptr;
            return head;
        }
        return new MPSCQueueNode();
    }

    void MPSCQueueNodePool::Release(MPSCQueueNode* node) noexcept
    {
        std::lock_guard<std::mutex> lock(_poolMutex);
        node->_next.store(_freeHead.load(std::memory_order_relaxed), std::memory_order_relaxed);
        _freeHead.store(node, std::memory_order_relaxed);
    }

    void MPSCQueueNodePool::Preallocate(std::size_t count)
    {
        for (std::size_t i = 0; i < count; ++i)
        {
            Release(new MPSCQueueNode());
        }
    }

    IntrusiveMPSCQueue::IntrusiveMPSCQueue() noexcept
    {
        _stub._next.store(nullptr, std::memory_order_relaxed);
        _head.store(&_stub, std::memory_order_relaxed);
        _tail = &_stub;
    }

    void IntrusiveMPSCQueue::Enqueue(MPSCQueueNode* node) noexcept
    {
        node->_next.store(nullptr, std::memory_order_relaxed);
        MPSCQueueNode* prev = _head.exchange(node, std::memory_order_acq_rel);
        prev->_next.store(node, std::memory_order_release);
    }

    MPSCQueueNode* IntrusiveMPSCQueue::Dequeue() noexcept
    {
        MPSCQueueNode* tail = _tail;
        MPSCQueueNode* next = tail->_next.load(std::memory_order_acquire);
        if (next)
        {
            _tail = next;
            return next;
        }

        MPSCQueueNode* head = _head.load(std::memory_order_acquire);
        if (tail != head)
        {
            unsigned spins = 0;
            while ((next = tail->_next.load(std::memory_order_acquire)) == nullptr)
            {
                if (_pauseHook) _pauseHook();
                if (_spinMax && ++spins >= _spinMax)
                {
                    if (_yieldOnSpin)
                    {
#if defined(_WIN32)
                        SwitchToThread();
#elif defined(__linux__) || defined(__APPLE__)
                        sched_yield();
#endif
                    }
                    spins = 0;
                }
            }
            _tail = next;
            return next;
        }

        return nullptr;
    }

    MPSCQueueNode* IntrusiveMPSCQueue::TryDequeue() noexcept
    {
        MPSCQueueNode* tail = _tail;
        MPSCQueueNode* next = tail->_next.load(std::memory_order_acquire);
        if (next)
        {
            _tail = next;
            return next;
        }
        
        // If tail == head, it's either really empty or a producer is currently Enqueuing (between exchange and store).
        // TryDequeue returns nullptr in both cases to remain non-blocking.
        return nullptr;
    }

    bool IntrusiveMPSCQueue::Empty() const noexcept
    {
        MPSCQueueNode* tail = _tail;
        if (tail->_next.load(std::memory_order_acquire) != nullptr) return false;
        return tail == _head.load(std::memory_order_acquire);
    }

    std::size_t IntrusiveMPSCQueue::DequeueAll(MPSCQueueNode*& first, MPSCQueueNode*& last, std::size_t maxCount) noexcept
    {
        first = nullptr;
        last = nullptr;

        std::size_t count = 0;
        MPSCQueueNode* n = Dequeue();
        if (!n) return 0;

        first = n;
        last = n;
        ++count;

        while (count < maxCount)
        {
            MPSCQueueNode* next = Dequeue();
            if (!next) break;
            last = next;
            ++count;
        }
        return count;
    }
}
