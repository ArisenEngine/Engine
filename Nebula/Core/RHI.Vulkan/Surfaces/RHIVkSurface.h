#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Surfaces/Surface.h"
#include "../Common.h"

#if VK_USE_PLATFORM_WIN32_KHR
#include <vulkan/vulkan_win32.h>
#endif

#include <optional>

namespace NebulaEngine::RHI
{
    class DLL RHIVkSurface final : public Surface
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkSurface);
        ~RHIVkSurface() noexcept override;
        explicit RHIVkSurface(u32&& id, Instance*  instance);
        [[nodiscard]] void* GetHandle() const override { return m_Surface; }
        
    private:

        VkSurfaceKHR m_Surface;
    };
}
