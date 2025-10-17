#pragma once
#include "CoreInfraCommon.h"
#include <atomic>
#include <utility>
#include <cstddef>

namespace ArisenEngine::Threadable::Containers
{
    // Forward declaration
    class AtomicNodePool;
	// Intrusive node used by ConcurrentLinkList
	COREINFRA_DLL class AtomicNode
	{
		friend class MPSCQueue;
	public:
		AtomicNode() noexcept : _next(nullptr) {}
		AtomicNode* Next() const noexcept { return _next.load(std::memory_order_acquire); }
		AtomicNode* Link(AtomicNode* next) noexcept { _next.store(next, std::memory_order_release); return next; }
		void* data[3] = { nullptr, nullptr, nullptr };

		// Object pool helpers
		static AtomicNode* Acquire(AtomicNodePool& pool) noexcept;
		void Recycle(AtomicNodePool& pool) noexcept;
	private:
		std::atomic<AtomicNode*> _next;
	};

    // Simple lock-free object pool for AtomicNode
    COREINFRA_DLL class AtomicNodePool
    {
    public:
        AtomicNodePool() noexcept : _freeHead(nullptr) {}
        AtomicNodePool(const AtomicNodePool&) = delete;
        AtomicNodePool& operator=(const AtomicNodePool&) = delete;

        // Acquire a node from the pool, or allocate if empty
        AtomicNode* Acquire() noexcept;
        // Return a node to the pool
        void Release(AtomicNode* node) noexcept;
        // Preallocate N nodes into the pool
        void Preallocate(std::size_t count);

    private:
        std::atomic<AtomicNode*> _freeHead; // Treiber stack head; multiple consumers pop, single producer push is supported
    };

    // Global default pool accessor (lazy-initialized in cpp)
    COREINFRA_DLL AtomicNodePool& GetGlobalAtomicNodePool() noexcept;

	COREINFRA_DLL class MPSCQueue
	{
	public:
		MPSCQueue() noexcept;
		MPSCQueue(const MPSCQueue&) = delete;
		MPSCQueue& operator=(const MPSCQueue&) = delete;

		void Enqueue(AtomicNode* node) noexcept;
		AtomicNode* TryDequeue() noexcept;
		bool Empty() const noexcept;

        // Batch dequeue up to maxCount nodes; returns number dequeued.
        // Outputs first/last pointers for efficient bulk processing.
        std::size_t TryDequeueAll(AtomicNode*& first, AtomicNode*& last, std::size_t maxCount = (std::size_t)-1) noexcept;

        // Configure spin behavior when waiting for producer to link prev->_next.
        // maxSpins == 0 means infinite (but may still yield).
        void ConfigureSpin(unsigned maxSpins, bool yieldOnSpin) noexcept { _spinMax = maxSpins; _yieldOnSpin = yieldOnSpin; }
        void SetPauseHook(void(*hook)()) noexcept { _pauseHook = hook; }

		template <class F>
		void Drain(F&& fn)
		{
			for (AtomicNode* n; (n = TryDequeue()) != nullptr; )
			{
				fn(n);
			}
		}

	private:
		struct StubNode { std::atomic<AtomicNode*> _next{ nullptr }; } _stub;
		std::atomic<AtomicNode*> _head{ nullptr };
		AtomicNode* _tail{ nullptr };

        // Spin control
        unsigned _spinMax = 0;          // 0 => infinite spin (with optional yield)
        bool _yieldOnSpin = true;
        void (*_pauseHook)() = nullptr; // optional pause/yield hook
	};
}
