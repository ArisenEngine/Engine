#pragma once
#include "IRenderPattern.h"
#include "IResource.h"
#include "IRHIContext.h"
#include "ObjectBase.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename BaseInterface> requires std::is_base_of_v<IResource, BaseInterface>
class ResourceBase : public ObjectBase<BaseInterface>
{
public:
    ResourceBase(const IRHIContext& context, ResourceType resourceType)
        :m_resourceType(resourceType), m_context(context){}

    // IResource
    virtual ResourceType GetResourceType() const override
    {
        return m_resourceType;
    }
protected:
    const IRHIContext& m_context;
    ResourceType m_resourceType;
};
ARISENRHI_END_NAMESPACE
