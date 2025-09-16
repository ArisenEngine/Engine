#pragma once
#include "ITexture.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<ITexture, typename RHIImplTraits::TextureInterface>
class TextureBase : public ObjectBase<typename RHIImplTraits::TextureInterface>
{
public:
    
};
ARISENRHI_END_NAMESPACE