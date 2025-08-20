#pragma once
#include <array>
#include<format>

#include "CommandKit.h"
#include "CommonFlags.hpp"
#include "CoreMinimalRHI.h"
#include "ICommandKit.h"
#include "ICommandList.h"
#include "ICommandQueue.h"
#include "ObjectBase.h"
#include "IRHIContext.h"
#include "IDevice.h"
#include "DebugUtils/Checks.h"
#include "DebugUtils/Verifies.h"

ARISENRHI_BEGIN_NAMEPSACE
    static const std::array<std::string, static_cast<size_t>(CommandListType::__COUNT)> g_default_command_kit_names{
    "Upload",
    "Render",
    "Compute"
};

template<typename RHIImplTraits, typename ContextInterface> requires std::is_base_of_v<IRHIContext , ContextInterface>
class ContextBase : public ObjectBase<ContextInterface>
{
public:
    using DescriptorManagerImplType = typename RHIImplTraits::DescriptorManagerImplType;
    
    ContextBase(IDevice& device, ContextType type, UniquePtr<DescriptorManagerImplType>&& descriptor_manager_ptr)
    :m_device_ptr(device.GetInterface<IDevice>())
    ,m_type(type)
    ,m_descriptor_manager_ptr(std::move(descriptor_manager_ptr))
    {}

    virtual void Initialize()
    {
        LOG_RHI_DEBUG("Context {} Initialize!", this->GetName())

        if (const std::string_view context_name = this->GetName();
            !context_name.empty() && m_device_ptr != nullptr)
        {
            m_device_ptr->SetName(std::format("{} Device", context_name));
        }
    }

    virtual const IDevice& GetDevice() const
    {
        CHECK_VALID(m_device_ptr);
        return *m_device_ptr;
    }

    virtual Ptr<ICommandKit> CreateCommandKit(CommandListType type) const override final
    {
        return MakePtr(CommandKit, *this, type);
    }
    
    // lazy create 
    virtual ICommandKit& GetDefaultCommandKit(CommandListType type) const override final
    {
        Ptr<ICommandKit>& cmd_kit_Ptr = m_default_Command_Kit_Ptrs[ToInt32(type)];
        if (cmd_kit_Ptr)
        {
            return *cmd_kit_Ptr;
        }

        // Create
        cmd_kit_Ptr = CreateCommandKit(type);
        cmd_kit_Ptr->SetName(std::format("{} {}", ContextBase::GetName(), g_default_command_kit_names[ToInt32(type)] ));

        m_default_command_kit_ptrs_byQueue[std::addressof(cmd_kit_Ptr->GetQueue())] = cmd_kit_Ptr;
        return * cmd_kit_Ptr;
    }

    virtual IDescriptorManager& GetDescriptorManager() override
    {
        VERIFY_NOT_NULL(m_descriptor_manager_ptr, "DescriptorManager is null!");
        return *m_descriptor_manager_ptr;
    }
    
private:
    typedef std::array<Ptr<ICommandKit>, static_cast<size_t>(CommandListType::__COUNT)> CommandKitPtrsByType;
    typedef std::map<ICommandQueue*, Ptr<ICommandKit>> CommandKitByQueue;
    
    const ContextType m_type;
    Ptr<IDevice> m_device_ptr;
    mutable CommandKitPtrsByType m_default_Command_Kit_Ptrs;
    mutable CommandKitByQueue m_default_command_kit_ptrs_byQueue;

    UniquePtr<DescriptorManagerImplType> m_descriptor_manager_ptr;
};
ARISENRHI_END_NAMESPACE
