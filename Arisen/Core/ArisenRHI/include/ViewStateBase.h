#pragma once
#include "IViewState.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IViewState,typename RHIImplTraits::ViewStateInterface>
class ViewStateBase : public ObjectBase<typename RHIImplTraits::ViewStateInterface>
{
public:
    ViewStateBase(const ViewSettings& viewSettings)
        :m_settings(viewSettings)
    {
        
    }
private:
    ViewSettings m_settings;
};
ARISENRHI_END_NAMESPACE