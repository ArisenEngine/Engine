#pragma once
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename TContext> requires std::is_base_of_v<IContext, TContext>
class ContextDxCommon : public TContext
{
public:
    ContextDxCommon(IDevice& device, const TContext::Settings& settings)
        :TContext(device, settings){}

    virtual void Initialize()
    {
        
    }
};



ARISENRHI_END_NAMESPACE
