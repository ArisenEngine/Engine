#pragma once

#include "Base/FoundationMinimal.h"

#include "../Handles/RHIHandle.h"
#include "RHIDeferredDeletionQueue.h"


namespace ArisenEngine::RHI
{
    class RHIResourceRegistry;

    // Small RAII wrapper for registry-backed resources.
    // Explicit Release(ticket) is required to route destruction through the
    // deferred deletion queue.
    class RHIResourceRef final
    {
    public:
        RHIResourceRef() = default;

        RHIResourceRef(RHIResourceRegistry* registry, RHIResourceHandle handle)
            : m_Registry(registry), m_Handle(handle)
        {
        }

        ~RHIResourceRef() = default;

        RHIResourceRef(const RHIResourceRef& other);
        RHIResourceRef& operator=(const RHIResourceRef& other);

        RHIResourceRef(RHIResourceRef&& other) noexcept;
        RHIResourceRef& operator=(RHIResourceRef&& other) noexcept;

        [[nodiscard]] bool IsValid() const { return m_Handle.IsValid(); }
        [[nodiscard]] RHIResourceHandle Get() const { return m_Handle; }

        void Release();

    private:
        void ResetNoRelease()
        {
            m_Registry = nullptr;
            m_Handle = RHIResourceHandle::Invalid();
        }

        RHIResourceRegistry* m_Registry{nullptr};
        RHIResourceHandle m_Handle{};
    };
} // namespace ArisenEngine::RHI
