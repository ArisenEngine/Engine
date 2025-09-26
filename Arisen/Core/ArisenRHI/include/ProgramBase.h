#pragma once
#include "IProgram.h"
#include "IRHIContext.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IProgram, typename RHIImplTraits::ProgramInterface>
class ProgramBase : public ObjectBase<typename RHIImplTraits::ProgramInterface>
{
public:
    ProgramBase(const IRHIContext& context, const ProgramSettings& settings)
        :m_context(context),m_settings(settings)
    {
    }

private:
    const IRHIContext& m_context;
    ProgramSettings m_settings;
};
ARISENRHI_END_NAMESPACE