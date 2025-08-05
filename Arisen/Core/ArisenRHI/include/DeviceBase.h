#pragma once
#include "IDevice.h"
#include "ObjectBase.h"
#include "RHIMacros.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename BaseInterface> requires std::is_base_of_v<IDevice, BaseInterface>
class DeviceBase : public ObjectBase<BaseInterface>
{
public:
    DeviceBase(const std::string& adapter_name)
    :m_adapter_name(adapter_name){}

private:
    const std::string m_adapter_name;
};
ARISENRHI_END_NAMESPACE
