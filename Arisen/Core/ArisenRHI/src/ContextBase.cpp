#include "ContextBase.h"

ArisenRHI::ContextBase::ContextBase(IDevice& device, ContextType type)
    :m_pDevice(device.GetInterface<IDevice>())
,m_type(type)
{
}
