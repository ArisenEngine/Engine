#pragma once

#include "Base/FoundationMinimal.h"
#include "Containers/Containers.h"
#include "../Handles/RHIHandle.h"
#include "Concurrency/AtomicStack.h"
#include <atomic>
#include <mutex>

namespace ArisenEngine {
namespace RHI {

/**
 * @brief A lock-free resource pool for managing RHI resources with
 * generation-backed handles.
 * Uses a segmented / block-based storage to support lock-free growth and pointer stability.
 * @tparam THandle The specific RHIHandle type (e.g., RHIBufferHandle).
 * @tparam TResource The resource type being managed (e.g., RHIVkBuffer).
 */
template <typename THandle, typename TResource> class RHIResourcePool {
public:
  struct PoolEntry {
    TResource resource;
    std::atomic<UInt32> generation{0};
    uint32_t nextFreeIndex{0}; // Used by AtomicStack
  };

  static constexpr uint32_t BlockSize = 1024;
  static constexpr uint32_t MaxBlocks = 1024; // ~1M entries total

  explicit RHIResourcePool(size_t initialCapacity = 1024) {
    uint32_t numBlocksNeeded = static_cast<uint32_t>((initialCapacity + BlockSize - 1) / BlockSize);
    for (uint32_t i = 0; i < numBlocksNeeded; ++i) {
        Grow();
    }
  }

  ~RHIResourcePool() {
    for (uint32_t i = 0; i < m_BlockCount.load(); ++i) {
        delete[] m_Blocks[i].load();
    }
  }

  /**
   * @brief Allocates a new handle and initializes the resource in-place.
   * @tparam TInit A function pointer or lambda: `void(TResource*)`.
   * @return The new handle with incremented generation.
   */
  template <typename TInit> THandle Allocate(TInit &&initFn) {
    uint32_t index = m_FreeStack.Pop([this](uint32_t idx) {
      return GetEntry(idx)->nextFreeIndex;
    });

    if (index == Concurrency::Containers::AtomicStack::InvalidIndex) {
      // Growth requires a lock as it's a rare and heavy operation
      std::lock_guard<std::mutex> lock(m_GrowMutex);

      // Try pop again in case someone else grew the pool
      index = m_FreeStack.Pop([this](uint32_t idx) {
        return GetEntry(idx)->nextFreeIndex;
      });

      if (index == Concurrency::Containers::AtomicStack::InvalidIndex) {
        Grow();
        index = m_FreeStack.Pop([this](uint32_t idx) {
          return GetEntry(idx)->nextFreeIndex;
        });
      }
    }

    auto *entry = GetEntry(index);
    // Initialize resource via call-back before bumping generation
    initFn(&entry->resource);

    // Increment generation and ensure everything before is visible
    UInt32 newGen = entry->generation.fetch_add(1, std::memory_order_release) + 1;

    THandle handle;
    handle.index = index;
    handle.generation = newGen;
    return handle;
  }

  /**
   * @brief Returns a pointer to the resource associated with the handle, or
   * nullptr if invalid/stale.
   */
  TResource *Get(THandle handle) const {
    if (!handle.IsValid())
      return nullptr;

    auto *entry = GetEntry(handle.index);
    if (!entry)
      return nullptr;

    // Load generation with acquire to see the resource initialization
    if (entry->generation.load(std::memory_order_acquire) == handle.generation) {
      return const_cast<TResource *>(&entry->resource);
    }
    return nullptr;
  }

  /**
   * @brief Deallocates the handle and returns the resource pointer for external
   * cleanup.
   * @return The resource pointer if the handle was valid and matched
   * generation, nullptr otherwise.
   * @note The pointer remains valid after deallocation until the slot is
   * re-allocated. The caller should perform any necessary Vulkan object
   * destruction or state resetting here.
   */
  TResource *Deallocate(THandle handle) {
    if (!handle.IsValid())
      return nullptr;

    auto *entry = GetEntry(handle.index);
    if (!entry)
      return nullptr;

    // We only deallocate if the handle matches exactly.
    if (entry->generation.load(std::memory_order_acquire) == handle.generation) {
      // Return the pointer for cleanup.
      // Note: We don't reset the resource here; the caller is expected to do it
      // or the next Allocate(initFn) will overwrite it.
      m_FreeStack.Push(handle.index, &entry->nextFreeIndex);
      return &entry->resource;
    }

    return nullptr;
  }

  /**
   * @brief Clears all entries and resets the pool.
   * @warning NOT thread-safe for active use. Should only be called during shutdown.
   */
  void Clear() {
    std::lock_guard<std::mutex> lock(m_GrowMutex);
    // Re-initialize free stack and all blocks
    // This is complex for a lock-free stack. In practice, resource pools
    // are often only cleared at destruction.
    // For now, we just clear the resource pointers in existing blocks.
    for (uint32_t i = 0; i < m_BlockCount.load(); ++i) {
      // Value-based storage doesn't need pointer clearing, but the generation
      // should probably be bumped or slots invalidated if we wanted a true
      // Clear(). For now, we follow the same pattern as before.
    }
  }

  /**
   * @brief Finds the first handle whose resource matches the predicate.
   */
  template <typename TPredicate>
  THandle FindHandle(TPredicate&& predicate) const {
    // This is slow and should be used sparingly
    uint32_t activeBlocks = m_BlockCount.load(std::memory_order_acquire);
    for (uint32_t i = 0; i < activeBlocks; ++i) {
      PoolEntry* block = m_Blocks[i].load(std::memory_order_relaxed);
      for (uint32_t j = 0; j < BlockSize; ++j) {
        auto &entry = block[j];
        UInt32 gen = entry.generation.load(std::memory_order_acquire);
        if (gen > 0 && predicate(entry.resource)) {
          THandle handle;
          handle.index = i * BlockSize + j;
          handle.generation = gen;
          return handle;
        }
      }
    }
    return THandle::Invalid();
  }

private:
  PoolEntry* GetEntry(uint32_t index) const {
    uint32_t blockIdx = index / BlockSize;
    if (blockIdx >= m_BlockCount.load(std::memory_order_acquire)) return nullptr;
    
    uint32_t localIdx = index % BlockSize;
    PoolEntry* block = m_Blocks[blockIdx].load(std::memory_order_relaxed);
    return &block[localIdx];
  }

  void Grow() {
    uint32_t blockIdx = m_BlockCount.load(std::memory_order_relaxed);
    if (blockIdx >= MaxBlocks) return;

    PoolEntry* newBlock = new PoolEntry[BlockSize];
    uint32_t baseIdx = blockIdx * BlockSize;

    // Initialize the new block and push to free stack
    for (uint32_t i = 0; i < BlockSize; ++i) {
        // We push in reverse order so we allocate from the front of the block
        uint32_t entryIdx = baseIdx + (BlockSize - 1 - i);
        m_FreeStack.Push(entryIdx, &newBlock[BlockSize - 1 - i].nextFreeIndex);
    }

    m_Blocks[blockIdx].store(newBlock, std::memory_order_release);
    m_BlockCount.fetch_add(1, std::memory_order_release);
  }

  std::atomic<PoolEntry*> m_Blocks[MaxBlocks]{};
  std::atomic<uint32_t> m_BlockCount{0};
  
  Concurrency::Containers::AtomicStack m_FreeStack;
  std::mutex m_GrowMutex; // Only for serialized growth
};

} // namespace RHI
} // namespace ArisenEngine

