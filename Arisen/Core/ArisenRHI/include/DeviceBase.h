#pragma once
#include "IDevice.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
class DeviceBase : public ObjectBase<IDevice>
{
public:
    DeviceBase(const std::string& adapter_name);

private:
    const std::string m_adapter_name;
    
};
ARISENRHI_END_NAMESPACE
