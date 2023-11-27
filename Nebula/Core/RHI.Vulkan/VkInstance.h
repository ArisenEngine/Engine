#pragma once

#include "./Common.h"
#include "RHI/Instance.h"

namespace NebulaEngine::RHI
{
    class DLL VkInstance final : public Instance
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(VkInstance)
        VkInstance(AppInfo&& createInfo);
        ~VkInstance() noexcept final override;
    private:
        
        
    };
}

extern "C" DLL NebulaEngine::RHI::Instance * CreateInstance();