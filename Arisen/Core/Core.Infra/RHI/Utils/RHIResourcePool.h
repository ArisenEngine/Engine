#pragma once

#include "../../Common/CommandHeaders.h"
#include "../../Containers/Containers.h"
#include "../Handles/RHIHandle.h"
#include <mutex>

namespace ArisenEngine {
namespace RHI {
/**
 * @brief A thread-safe resource pool for managing RHI resources with
 * generation-backed handles.
 * @tparam THandle The specific RHIHandle type (e.g., RHIBufferHandle).
 * @tparam TResource The resource type being managed (e.g., RHIVkBuffer).
 */
template <typename THandle, typename TResource> class RHIResourcePool {
public:
  struct PoolEntry {
    TResource *resource{nullptr};
    UInt32 generation{0};
  };

  explicit RHIResourcePool(size_t initialCapacity = 256) {
    std::lock_guard<std::mutex> lock(m_Mutex);
    m_Entries.resize(initialCapacity);
    m_FreeIndices.reserve(initialCapacity);
    for (size_t i = 0; i < initialCapacity; ++i) {
      // Push indices so we allocate from the front
      m_FreeIndices.push_back(static_cast<UInt32>(initialCapacity - 1 - i));
    }
  }

  ~RHIResourcePool() = default;

  /**
   * @brief Allocates a new handle for the given resource.
   * @return The new handle with incremented generation.
   */
  THandle Allocate(TResource *resource) {
    std::lock_guard<std::mutex> lock(m_Mutex);
    if (m_FreeIndices.empty()) {
      size_t oldSize = m_Entries.size();
      size_t newSize = oldSize * 2;
      m_Entries.resize(newSize);
      m_FreeIndices.reserve(newSize - oldSize);
      for (size_t i = oldSize; i < newSize; ++i) {
        m_FreeIndices.push_back(
            static_cast<UInt32>(newSize - 1 - (i - oldSize)));
      }
    }

    UInt32 index = m_FreeIndices.back();
    m_FreeIndices.pop_back();

    auto &entry = m_Entries[index];
    entry.resource = resource;
    entry.generation++;

    THandle handle;
    handle.index = index;
    handle.generation = entry.generation;
    return handle;
  }

  /**
   * @brief Returns the resource associated with the handle, or nullptr if
   * invalid/stale.
   */
  TResource *Get(THandle handle) const {
    if (!handle.IsValid())
      return nullptr;

    std::lock_guard<std::mutex> lock(m_Mutex);
    if (handle.index >= static_cast<UInt32>(m_Entries.size()))
      return nullptr;

    const auto &entry = m_Entries[handle.index];
    if (entry.generation == handle.generation) {
      return entry.resource;
    }
    return nullptr;
  }

  /**
   * @brief Deallocates the handle and returns the resource for external
   * cleanup.
   * @return The resource pointer if the handle was valid and matched
   * generation, nullptr otherwise.
   */
  TResource *Deallocate(THandle handle) {
    if (!handle.IsValid())
      return nullptr;

    std::lock_guard<std::mutex> lock(m_Mutex);
    if (handle.index >= static_cast<UInt32>(m_Entries.size()))
      return nullptr;

    auto &entry = m_Entries[handle.index];
    if (entry.generation == handle.generation) {
      TResource *resource = entry.resource;
      entry.resource = nullptr;
      m_FreeIndices.push_back(handle.index);
      return resource;
    }
    return nullptr;
  }

  /**
   * @brief Clears all entries and resets the pool.
   * @warning Does NOT deallocate resource pointers.
   */
  void Clear() {
    std::lock_guard<std::mutex> lock(m_Mutex);
    for (auto &entry : m_Entries) {
      entry.resource = nullptr;
      // We don't reset generation to avoid reusing handles of cleared resources
    }
    m_FreeIndices.clear();
    for (size_t i = 0; i < m_Entries.size(); ++i) {
      m_FreeIndices.push_back(static_cast<UInt32>(m_Entries.size() - 1 - i));
    }
  }

private:
  mutable std::mutex m_Mutex;
  Containers::Vector<PoolEntry> m_Entries;
  Containers::Vector<UInt32> m_FreeIndices;
};
} // namespace RHI
} // namespace ArisenEngine
