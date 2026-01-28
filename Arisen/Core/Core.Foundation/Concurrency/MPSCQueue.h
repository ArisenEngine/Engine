#pragma once
#include "../Base/FoundationMinimal.h"

namespace ArisenEngine::Concurrency
{
    // Forward declaration
    class AtomicNodePool;

	// Intrusive node used by MPSCQueue
	class FOUNDATION_DLL AtomicNode
	{
		friend class AtomicNodePool;
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
    class FOUNDATION_DLL AtomicNodePool
    {
    public:
        AtomicNodePool() noexcept : _freeHead(nullptr) {}
        NO_COPY_NO_MOVE(AtomicNodePool)

        // Acquire a node from the pool, or allocate if empty
        AtomicNode* Acquire() noexcept;
        // Return a node to the pool
        void Release(AtomicNode* node) noexcept;
        // Preallocate N nodes into the pool
        void Preallocate(std::size_t count);

    private:
        std::atomic<AtomicNode*> _freeHead; // Treiber stack head
    };

    // Global default pool accessor
    FOUNDATION_DLL AtomicNodePool& GetGlobalAtomicNodePool() noexcept;

	class FOUNDATION_DLL MPSCQueue
	{
	public:
		MPSCQueue() noexcept;
		NO_COPY_NO_MOVE(MPSCQueue)

		void Enqueue(AtomicNode* node) noexcept;
		AtomicNode* TryDequeue() noexcept;
		bool Empty() const noexcept;

        // Batch dequeue
        std::size_t TryDequeueAll(AtomicNode*& first, AtomicNode*& last, std::size_t maxCount = (std::size_t)-1) noexcept;

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
		AtomicNode _stub;
		std::atomic<AtomicNode*> _head{ nullptr };
		AtomicNode* _tail{ nullptr };

        unsigned _spinMax = 0;
        bool _yieldOnSpin = true;
        void (*_pauseHook)() = nullptr;
	};
}
