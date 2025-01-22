#pragma once
#include <vulkan/vulkan_core.h>
#include "Logger/Logger.h"
#include "RHI/Program/GPUProgram.h"

namespace ArisenEngine::RHI
{
    class RHIVkGPUProgram final : public GPUProgram 
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUProgram)
        RHIVkGPUProgram(VkDevice device);
        ~RHIVkGPUProgram() noexcept override;
        void* GetHandle() const override { ASSERT(m_VkShaderModule != VK_NULL_HANDLE); return m_VkShaderModule; }
        bool AttachProgramByteCode(GPUProgramDesc&& desc) override;

        // TODO:
        UInt32 GetShaderStageCreateFlags() override { return 0; }
        void* GetSpecializationInfo() override { return nullptr; }
    protected:
        void DestroyHandle() override;
        
    private:

        VkDevice m_VkDevice;
        VkShaderModule m_VkShaderModule;
    };
}
