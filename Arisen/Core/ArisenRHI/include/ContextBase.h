#pragma once
#include "CommonFlags.hpp"
#include "CoreMinimalRHI.h"
#include "ICommandKit.h"
#include "ICommandList.h"
#include "ICommandQueue.h"
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

    virtual Ptr<ICommandKit> CreateCommandKit(CommandListType type) const override
    {
        
    }
    
    // lazy create 
    virtual ICommandKit& GetDefaultCommandKit(CommandListType type) const override
    {
        Ptr<ICommandKit>& cmdKitPtr = mDefaultCommandKitPtrs[ToInt32(type)];
        if (cmdKitPtr)
        {
            return *cmdKitPtr;
        }

        // Create
        
    }
private:
    typedef std::array<Ptr<ICommandKit>, static_cast<size_t>(CommandListType::__COUNT)> CommandKitPtrsByType;
    typedef std::map<ICommandQueue*, Ptr<ICommandKit>> CommandKitByQueue;
    
    const ContextType mType;
    Ptr<IDevice> mDevicePtr;
    CommandKitPtrsByType mDefaultCommandKitPtrs;
    CommandKitByQueue mDefaultCommandKitPtrsByQueue;
};
ARISENRHI_END_NAMESPACE
