#include "ResourceViewBase.h"
#include "ITexture.h"

ARISENRHI_BEGIN_NAMEPSACE
ResourceViewBase::ResourceViewBase(IResource& resource)
    :m_resource_ptr(resource.GetInterface<IResource>())
{
    
}

TextureViewBase::TextureViewBase(ITexture& texture)
    :ResourceViewBase(texture)
,m_texture_ptr(texture.GetInterface<ITexture>())
{
}

ARISENRHI_END_NAMESPACE
