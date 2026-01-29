#pragma once
#include <vulkan/vulkan_core.h>
#include "Logger/Logger.h"
#include "RHI/Pipeline/RHIShaderProgram.h"
#include "RHI/Pipeline/RHIShaderReflection.h"

namespace ArisenEngine::RHI
{
    class RHIVkGPUProgram final : public RHIShaderProgram 
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUProgram)
        RHIVkGPUProgram(VkDevice device);
        ~RHIVkGPUProgram() noexcept override;
        void* GetHandle() const override { ASSERT(m_VkShaderModule != VK_NULL_HANDLE); return m_VkShaderModule; }
        bool AttachProgramByteCode(RHIShaderProgramDesc&& desc) override;

        // TODO:
        UInt32 GetShaderStageCreateFlags() override { return 0; }
        void* GetSpecializationInfo() override { return nullptr; }
        const RHIShaderReflectionData& GetReflectionData() const { return m_ReflectionData; }

    protected:
        void DestroyHandle() override;
        
    private:
        VkDevice m_VkDevice;
        VkShaderModule m_VkShaderModule;
        RHIShaderReflectionData m_ReflectionData;
    };
}




