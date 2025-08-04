#pragma once
#include "CoreMinimalRHI.h"
#include "ICommandKit.h"
#include "ICommandList.h"
#include "ObjectBase.h"
#include "IContext.h"
#include "IDevice.h"
#include "Logger/DebugUtils.h"

ARISENRHI_BEGIN_NAMEPSACE
    template<typename ContextInterface> requires std::is_base_of_v<IContext , ContextInterface>
class ContextBase : public ObjectBase<ContextInterface>
{
public:
    ContextBase(IDevice& device, ContextType type)
    :mDevicePtr(device.GetInterface<IDevice>())
    ,mType(type){}

    virtual void Initialize(){}

    IDevice& GetDevice()
    {
        CHECK_VALID(mDevicePtr);
        return *mDevicePtr;
    }

    ICommandKit& GetDefaultCommandKit(CommandListType type) const
    {
        
    }
private:
    using CommandKitPtrsByType = std::array<Ptr<ICommandKit>, CommandListType::__COUNT>;
    
    const ContextType mType;
    Ptr<IDevice> mDevicePtr;
    
};
ARISENRHI_END_NAMESPACE
