#pragma once
#include "RHISwapChain.h"
#include "Base/FoundationMinimal.h"
#include "../Core/RHIInstance.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHI_DLL RHISurface
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHISurface)
        virtual ~RHISurface() noexcept;
        explicit RHISurface(UInt32&& id, RHIInstance* instance);
        // CppSharp: exclude from binding — backend-only void* accessor.
        virtual void* GetHandle() const = 0;
        virtual void InitSwapChain() = 0;

        virtual RHISwapChain* GetSwapChain() = 0;

    protected:
        UInt32 m_RenderWindowId;
        RHIInstance* m_Instance;
    };
}
