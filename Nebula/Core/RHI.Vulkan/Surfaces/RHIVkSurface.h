#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Surfaces/Surface.h"
#include "../Common.h"
#include <optional>

namespace NebulaEngine::RHI
{
    class DLL RHIVkSurface final : public Surface
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSurface);
        ~RHIVkSurface() noexcept override;
    
    private:

        
    };
}
