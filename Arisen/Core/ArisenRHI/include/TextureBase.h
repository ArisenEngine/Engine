#pragma once
#include "ITexture.h"
#include "ObjectBase.h"
#include "ResourceBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<ITexture, typename RHIImplTraits::TextureInterface>
class TextureBase : public ResourceBase<typename RHIImplTraits::TextureInterface>
{
public:
    TextureBase(const IRHIContext& context, const TextureSettings& settings)
        :ResourceBase<typename RHIImplTraits::TextureInterface>(context, ResourceType::Texture),m_settings(settings)
    {}

    virtual const TextureSettings& GetSettings() const override {return m_settings;}
private:
    const TextureSettings m_settings;
};
ARISENRHI_END_NAMESPACE