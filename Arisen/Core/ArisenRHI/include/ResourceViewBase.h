#pragma once
#include "IResource.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
class ResourceViewBase
{
public:
    ResourceViewBase(IResource& resource);

    const ResourceViewSettings& GetSettings() const { return m_settings; }

    IResource& GetResource() { return *m_resource_ptr; }

private:
    Ptr<IResource> m_resource_ptr;
    ResourceViewSettings m_settings;
};


ARISENRHI_END_NAMESPACE
