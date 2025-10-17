#pragma once
#include <magic_enum.hpp>

#include "IProgram.h"
#include "IRHIContext.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
using ShadersByType = std::array<Ptr<IShader>, magic_enum::enum_count<ShaderType>()>;

template<typename RHIImplTraits> requires std::is_base_of_v<IProgram, typename RHIImplTraits::ProgramInterface>
class ProgramBase : public ObjectBase<typename RHIImplTraits::ProgramInterface>
{
public:
    ProgramBase(const IRHIContext& context, const ProgramSettings& settings)
        :m_context(context),m_settings(settings),m_shaders_by_type(CreateShadersByType(settings.shaders))
    {
    }

    static ShadersByType CreateShadersByType(const Ptrs<IShader>& shaders)
    {
        ShadersByType shaders_by_type;
        for (const Ptr<IShader>& shader_ptr : shaders)
        {
            shaders_by_type[magic_enum::enum_index(shader_ptr->GetType()).value()] = shader_ptr;
        }
        return shaders_by_type;
    }

    const Ptr<IShader>& GetShader(ShaderType shader_type) const final
    {
        return m_shaders_by_type[magic_enum::enum_index(shader_type).value()];
    }

protected:
private:
    const IRHIContext& m_context;
    ProgramSettings m_settings;
    ShadersByType m_shaders_by_type;
};
ARISENRHI_END_NAMESPACE
