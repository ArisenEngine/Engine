#include "MPSCLinkList.h"
#include <atomic>
#if defined(_WIN32)
#include <windows.h>
#endif

using namespace ArisenEngine::Containers;

MPSCLinkList::MPSCLinkList() noexcept
{
	_stub._next.store(nullptr, std::memory_order_relaxed);
	_head.store(&_stub, std::memory_order_relaxed);
	_tail = &_stub;
}

void MPSCLinkList::Enqueue(AtomicNode* node) noexcept
{
	// Prepare node link before publish
	node->_next.store(nullptr, std::memory_order_relaxed);

	// Publish new head; acquire isn't needed here, we are the producer
	AtomicNode* prev = _head.exchange(node, std::memory_order_release);

	// Link previous to this node so the consumer can see it via acquire load
	prev->_next.store(node, std::memory_order_release);
}

AtomicNode* MPSCLinkList::TryDequeue() noexcept
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

bool MPSCLinkList::Empty() const noexcept
{
	AtomicNode* tail = _tail;
	if (tail->_next.load(std::memory_order_acquire) != nullptr) return false;
	return tail == _head.load(std::memory_order_acquire);
}

std::size_t MPSCLinkList::TryDequeueAll(AtomicNode*& first, AtomicNode*& last, std::size_t maxCount) noexcept
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
