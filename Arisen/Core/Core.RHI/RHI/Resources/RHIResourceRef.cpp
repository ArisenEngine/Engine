#include "RHIResourceRef.h"

#include "RHIResourceRegistry.h"

ArisenEngine::RHI::RHIResourceRef::RHIResourceRef(const RHIResourceRef& other)
    : m_Registry(other.m_Registry), m_Handle(other.m_Handle)
{
    if (m_Registry && m_Handle.IsValid())
    {
        m_Registry->Retain(m_Handle);
    }
}

ArisenEngine::RHI::RHIResourceRef& ArisenEngine::RHI::RHIResourceRef::operator=(const RHIResourceRef& other)
{
    if (this == &other) return *this;
    ResetNoRelease();
    m_Registry = other.m_Registry;
    m_Handle = other.m_Handle;
    if (m_Registry && m_Handle.IsValid())
    {
        m_Registry->Retain(m_Handle);
    }
    return *this;
}

ArisenEngine::RHI::RHIResourceRef::RHIResourceRef(RHIResourceRef&& other) noexcept
    : m_Registry(other.m_Registry), m_Handle(other.m_Handle)
{
    other.m_Registry = nullptr;
    other.m_Handle = RHIResourceHandle::Invalid();
}

ArisenEngine::RHI::RHIResourceRef& ArisenEngine::RHI::RHIResourceRef::operator=(RHIResourceRef&& other) noexcept
{
    if (this == &other) return *this;
    ResetNoRelease();
    m_Registry = other.m_Registry;
    m_Handle = other.m_Handle;
    other.m_Registry = nullptr;
    other.m_Handle = RHIResourceHandle::Invalid();
    return *this;
}

void ArisenEngine::RHI::RHIResourceRef::Release()
{
    if (!m_Registry || !m_Handle.IsValid()) return;
    m_Registry->Release(m_Handle);
    ResetNoRelease();
}
