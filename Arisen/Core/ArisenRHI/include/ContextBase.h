#pragma once
#include "CoreMiminalRHI.h"
#include "ObjectBase.h"
#include "IContext.h"
#include "IDevice.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename ContextInterface> requires std::is_base_of_v<IContext , ContextInterface>
class ContextBase : public ObjectBase<ContextInterface>
{
public:
    ContextBase(IDevice& device, ContextType type)
    :m_pDevice(device.GetInterface<IDevice>())
    ,m_type(type){}

    virtual void Initialize(){}
private:
    const ContextType m_type;
    Ptr<IDevice> m_pDevice;
};
ARISENRHI_END_NAMESPACE
