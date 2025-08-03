#pragma once
#include "CoreMiminalRHI.h"
#include "ObjectBase.h"
#include "IContext.h"
#include "IDevice.h"

ARISENRHI_BEGIN_NAMEPSACE
    class ContextBase : virtual IContext, virtual ObjectBase
{
public:
    ContextBase(IDevice& device, ContextType type);
private:
    const ContextType m_type;
    Ptr<IDevice> m_pDevice;
};
ARISENRHI_END_NAMESPACE
