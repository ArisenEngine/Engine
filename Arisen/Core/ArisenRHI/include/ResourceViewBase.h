#pragma once
#include "IResource.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
class ResourceViewBase
{
public:
    ResourceViewBase(IResource& resource);
private:
    Ptr<IResource> m_resource_ptr;
};


ARISENRHI_END_NAMESPACE