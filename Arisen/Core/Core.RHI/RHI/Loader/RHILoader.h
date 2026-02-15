#pragma once
#include "../Definitions/CoreRHICommon.h"
#include "RHI/Definitions/GraphicsAPI.h"
#include "RHI/Core/RHIInstance.h"

namespace ArisenEngine::RHI
{
    /**
     * @brief Responsible for loading and unloading RHI implementations (DLLs).
     */
    class RHI_DLL RHILoader
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHILoader)

        /**
         * @brief Set the current graphics API and load the corresponding implementation.
         */
        static void SetCurrentGraphicsAPI(GraphicsAPI api_type);

        /**
         * @brief Create an RHI instance using the currently loaded implementation.
         */
        static RHIInstance* CreateInstance(RHIInstanceInfo&& app_info);

        /**
         * @brief Unload the RHI implementation and cleanup.
         */
        static void Dispose();

    private:
        static inline GraphicsAPI _api_type { GraphicsAPI::None };
        static inline void* _rhi_dll { nullptr }; // Using void* to avoid including Windows.h in public header
    };
} // namespace ArisenEngine::RHI
