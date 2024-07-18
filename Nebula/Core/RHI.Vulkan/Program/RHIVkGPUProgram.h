#pragma once
#include <vulkan/vulkan_core.h>
#include "RHI/Program/GPUProgram.h"

namespace NebulaEngine::RHI
{
    class RHIVkGPUProgram final : public GPUProgram 
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUProgram)
        RHIVkGPUProgram(VkDevice device);
        ~RHIVkGPUProgram() noexcept override;
        void* GetHandle() const override { return m_VkShaderModule; }
        bool AttachProgramByteCode(GPUProgramDesc&& desc) override;

    protected:
        void DestroyHandle() override;
        
    private:

        VkDevice m_VkDevice;
        VkShaderModule m_VkShaderModule;
    };
}
