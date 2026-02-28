#pragma once
#include <vulkan/vulkan_core.h>
#include <map>
#include <vector>

#include "Base/FoundationMinimal.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Attachment/EAttachmentLoadOp.h"
#include "RHI/Enums/Attachment/EAttachmentStoreOp.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
#include "RHI/Enums/Image/EImageLayout.h"
#include "RHI/RenderPass/RHIRenderPass.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;

    class RHIVkGPURenderPass final : public RHIRenderPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPURenderPass)
        RHIVkGPURenderPass(RHIVkDevice* device, UInt32 maxFramesInFlight);
        ~RHIVkGPURenderPass() noexcept override;

        inline void* GetHandle(UInt32 frameIndex) override
        {
            return m_VkRenderPasses[frameIndex % m_MaxFramesInFlight];
        }

    private:
        RHIVkDevice* m_Device;
        // todo: support for legacy use, currently not found any creation for vkRenderPass
        Containers::Vector<VkRenderPass> m_VkRenderPasses;
    };
}
