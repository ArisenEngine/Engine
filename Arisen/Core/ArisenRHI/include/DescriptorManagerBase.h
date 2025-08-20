#pragma once
#include "ContextBase.h"
#include "CoreMinimalRHI.h"
#include "IDescriptorManager.h"
#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename RHIImplTraits> requires std::is_base_of_v<IDescriptorManager,typename RHIImplTraits::DescriptorInterface>
class DescriptorManagerBase : public ObjectBase<typename RHIImplTraits::DescriptorInterface>
{
public:
    using BaseInterface = typename RHIImplTraits::DescriptorInterface;
    
    explicit DescriptorManagerBase(IRHIContext& context)

        :m_common_context(context)
    {}
private:
    IRHIContext& m_common_context;

};
ARISENRHI_END_NAMESPACE
