#pragma once
#include "IShader.h"
#include "ObjectBase.h"
#include "RHIMacros.h"
ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IShader, typename RHIImplTraits::ShaderInterface>
class ShaderBase : public ObjectBase<typename RHIImplTraits::ShaderInterface>
{
public:
    ShaderBase(ShaderType type, const IRHIContext& context, const ShaderSettings& settings)
        : m_type(type), m_settings(settings), m_context(context)
    {
    }

    // IShader
    virtual ShaderType GetType() const override final { return m_type; }
private:
    ShaderType m_type;
    ShaderSettings m_settings;
    const IRHIContext& m_context;
};
ARISENRHI_END_NAMESPACE
