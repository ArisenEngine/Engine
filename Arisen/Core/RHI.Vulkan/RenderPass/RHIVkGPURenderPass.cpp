#include "RenderPass/RHIVkGPURenderPass.h"
#include "Logger/Logger.h"
#include "Core/RHIVkDevice.h"

namespace ArisenEngine::RHI
{
    ArisenEngine::RHI::RHIVkGPURenderPass::RHIVkGPURenderPass(RHIVkDevice* device, UInt32 maxFramesInFlight):
        RHIRenderPass(maxFramesInFlight), m_Device(device)
    {
        m_VkRenderPasses.resize(maxFramesInFlight, VK_NULL_HANDLE);
    }

    ArisenEngine::RHI::RHIVkGPURenderPass::~RHIVkGPURenderPass() noexcept
    {
        auto device = static_cast<VkDevice>(m_Device->GetHandle());
        for (auto pass : m_VkRenderPasses)
        {
            if (pass != VK_NULL_HANDLE)
            {
                vkDestroyRenderPass(device, pass, nullptr);
            }
        }
        m_VkRenderPasses.clear();
    }
} // namespace ArisenEngine::RHI
