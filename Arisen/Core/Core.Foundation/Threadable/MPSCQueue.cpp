#include "MPSCQueue.h"
#include <atomic>
#if defined(_WIN32)
#include <windows.h>
#endif

using namespace ArisenEngine::Threadable::Containers;

// -------- AtomicNodePool implementation --------
namespace ArisenEngine::Threadable::Containers {
    static AtomicNodePool* g_GlobalPool = nullptr;
}

AtomicNodePool& ArisenEngine::Threadable::Containers::GetGlobalAtomicNodePool() noexcept
{
    if (!g_GlobalPool) g_GlobalPool = new AtomicNodePool();
    return *g_GlobalPool;
}

AtomicNode* AtomicNode::Acquire(AtomicNodePool& pool) noexcept { return pool.Acquire(); }
void AtomicNode::Recycle(AtomicNodePool& pool) noexcept { pool.Release(this); }

AtomicNode* AtomicNodePool::Acquire() noexcept
{
    AtomicNode* head = _freeHead.load(std::memory_order_acquire);
    while (head)
    {
        AtomicNode* next = head->_next.load(std::memory_order_relaxed);
        if (_freeHead.compare_exchange_weak(head, next, std::memory_order_acq_rel, std::memory_order_acquire))
        {
            head->_next.store(nullptr, std::memory_order_relaxed);
            head->data[0] = head->data[1] = head->data[2] = nullptr;
            return head;
        }
    }
    return new AtomicNode();
}

void AtomicNodePool::Release(AtomicNode* node) noexcept
{
    AtomicNode* head = _freeHead.load(std::memory_order_relaxed);
    do {
        node->_next.store(head, std::memory_order_relaxed);
    } while (!_freeHead.compare_exchange_weak(head, node, std::memory_order_release, std::memory_order_relaxed));
}

void AtomicNodePool::Preallocate(std::size_t count)
{
    for (std::size_t i = 0; i < count; ++i)
    {
        Release(new AtomicNode());
    }
}

MPSCQueue::MPSCQueue() noexcept
{
	_stub._next.store(nullptr, std::memory_order_relaxed);
	_head.store(&_stub, std::memory_order_relaxed);
	_tail = &_stub;
}

void MPSCQueue::Enqueue(AtomicNode* node) noexcept
{
	// Prepare node link before publish
	node->_next.store(nullptr, std::memory_order_relaxed);

	// Publish new head; acquire isn't needed here, we are the producer
	AtomicNode* prev = _head.exchange(node, std::memory_order_release);

	// Link previous to this node so the consumer can see it via acquire load
	prev->_next.store(node, std::memory_order_release);
}

AtomicNode* MPSCQueue::TryDequeue() noexcept
{
	AtomicNode* tail = _tail;
	AtomicNode* next = tail->_next.load(std::memory_order_acquire);
	if (next)
	{
		_tail = next;
		return next; // first real node
	}

    // If no next but head != tail, a producer swapped head but hasn't linked prev->_next yet.
	AtomicNode* head = _head.load(std::memory_order_acquire);
	if (tail != head)
	{
        // Wait until the link becomes visible (bounded spin with optional yield)
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
#else
                    // Best-effort portable yield
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

bool MPSCQueue::Empty() const noexcept
{
	AtomicNode* tail = _tail;
	if (tail->_next.load(std::memory_order_acquire) != nullptr) return false;
	return tail == _head.load(std::memory_order_acquire);
}

std::size_t MPSCQueue::TryDequeueAll(AtomicNode*& first, AtomicNode*& last, std::size_t maxCount) noexcept
{
    first = nullptr;
    last = nullptr;

    std::size_t count = 0;
    AtomicNode* n = TryDequeue();
    if (!n) return 0;

    first = n;
    last = n;
    ++count;

    while (count < maxCount)
    {
        AtomicNode* next = TryDequeue();
        if (!next) break;
        last = next;
        ++count;
    }
    return count;
}
