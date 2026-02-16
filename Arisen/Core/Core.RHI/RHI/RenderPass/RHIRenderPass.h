#pragma once
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/Definitions/CoreRHICommon.h"

namespace ArisenEngine::RHI
{
    class RHISubPass;

    class RHI_DLL RHIRenderPass
    {
        
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIRenderPass)
        RHIRenderPass(UInt32 maxFramesInFlight);
        VIRTUAL_DECONSTRUCTOR(RHIRenderPass)
        virtual void* GetHandle(UInt32 frameIndex) = 0;

    protected:
        UInt32 m_MaxFramesInFlight;
    };

}

