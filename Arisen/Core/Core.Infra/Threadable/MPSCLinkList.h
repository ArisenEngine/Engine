#pragma once
#include "CoreInfraCommon.h"
#include <atomic>
#include <utility>
#include <cstddef>

namespace ArisenEngine::Containers
{
	// Intrusive node used by ConcurrentLinkList
	COREINFRA_DLL class AtomicNode
	{
		friend class MPSCLinkList;
	public:
		AtomicNode() noexcept : _next(nullptr) {}
		AtomicNode* Next() const noexcept { return _next.load(std::memory_order_acquire); }
		AtomicNode* Link(AtomicNode* next) noexcept { _next.store(next, std::memory_order_release); return next; }
		void* data[3] = { nullptr, nullptr, nullptr };
	private:
		std::atomic<AtomicNode*> _next;
	};

	COREINFRA_DLL class MPSCLinkList
	{
	public:
		MPSCLinkList() noexcept;
		MPSCLinkList(const MPSCLinkList&) = delete;
		MPSCLinkList& operator=(const MPSCLinkList&) = delete;

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
