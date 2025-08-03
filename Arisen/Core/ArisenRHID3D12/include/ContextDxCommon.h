#pragma once
#include "ContextBase.h"
#include "CoreMinimalD3D12.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename TContext> requires std::is_base_of_v<ContextBase, TContext>
class ContextDxCommon : public TContext
{
public:
    ContextDxCommon(IDevice& device, const )
    
};

ARISENRHI_END_NAMESPACE