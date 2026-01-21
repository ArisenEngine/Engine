#pragma once

#include "Common/CommandHeaders.h"

#include "../Handles/RHIHandle.h"
#include "RHIDeferredDeletionQueue.h"


#include <mutex>

namespace ArisenEngine::RHI {
// Backend-agnostic registry:
// - generation guards against use-after-free
// - refCount is protected by a mutex (simple and portable)
// - destruction is routed through IRHIDeferredDeletionQueue (GPU-safe)
class RHIResourceRegistry final {
public:
  NO_COPY_NO_MOVE_NO_DEFAULT(RHIResourceRegistry)
  explicit RHIResourceRegistry(IRHIDeferredDeletionQueue *deletionQueue)
      : m_DeletionQueue(deletionQueue) {}

  // Create a new entry with refCount=1.
  // item is the object + deleter; backend-specific cleanup should live in the
  // object's destructor.
  RHIResourceHandle Create(RHIDeferredDeleteItem item) {
    std::lock_guard<std::mutex> lock(m_Mutex);

    UInt32 idx = 0;
    if (!m_FreeList.empty()) {
      idx = m_FreeList.back();
      m_FreeList.pop_back();
    } else {
      idx = static_cast<UInt32>(m_Entries.size());
      m_Entries.emplace_back();
    }

    auto &e = m_Entries[idx];
    e.refCount = 1;
    e.item = item;
    e.maxTicket = 0;
    return RHIResourceHandle{idx, e.generation};
  }

  bool Retain(RHIResourceHandle h) {
    std::lock_guard<std::mutex> lock(m_Mutex);
    if (!ValidateUnlocked(h))
      return false;
    m_Entries[h.index].refCount += 1;
    return true;
  }

  void Release(RHIResourceHandle h, RHIQueueType queue, RHIGpuTicket ticket) {
    RHIDeferredDeleteItem item{};
    RHIGpuTicket finalTicket = 0;
    {
      std::lock_guard<std::mutex> lock(m_Mutex);
      if (!ValidateUnlocked(h))
        return;

      auto &e = m_Entries[h.index];
      if (ticket > e.maxTicket)
        e.maxTicket = ticket;

      if (e.refCount > 1) {
        e.refCount -= 1;
        return;
      }

      // last ref
      item = e.item;
      finalTicket = e.maxTicket;
      e.item = {};
      e.refCount = 0;
      e.generation += 1;
      e.maxTicket = 0;
      m_FreeList.emplace_back(h.index);
    }

    if (item.ptr && item.deleter && m_DeletionQueue) {
      m_DeletionQueue->Enqueue(queue, finalTicket, item);
    }
  }

  bool IsAlive(RHIResourceHandle h) const {
    std::lock_guard<std::mutex> lock(m_Mutex);
    return ValidateUnlocked(h);
  }

private:
  struct Entry {
    UInt32 refCount{0};
    UInt32 generation{1};
    RHIDeferredDeleteItem item;
    RHIGpuTicket maxTicket{0};
  };

  bool ValidateUnlocked(RHIResourceHandle h) const {
    if (!h.IsValid())
      return false;
    if (h.index >= m_Entries.size())
      return false;
    const auto &e = m_Entries[h.index];
    if (e.generation != h.generation)
      return false;
    if (e.refCount == 0)
      return false;
    return true;
  }

  IRHIDeferredDeletionQueue *m_DeletionQueue{nullptr};
  mutable std::mutex m_Mutex;
  Containers::Vector<Entry> m_Entries;
  Containers::Vector<UInt32> m_FreeList;
};
} // namespace ArisenEngine::RHI
