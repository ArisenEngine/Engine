#pragma once
#include "RHI/Program/GPUPipelineStateObject.h"
#include <vulkan/vulkan_core.h>

namespace NebulaEngine::RHI
{
    class RHIVkDevice;
  
    
}

namespace NebulaEngine::RHI
{
    class RHIVkGPUPipelineStateObject final : public GPUPipelineStateObject
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipelineStateObject)
        ~RHIVkGPUPipelineStateObject() noexcept override;
        RHIVkGPUPipelineStateObject(RHIVkDevice* device);
        void AddProgram(u32 programId) override;
        void ClearAllPrograms() override;

        const u32 GetHash() const override;
        const EPipelineBindPoint GetBindPoint() const override { return m_BindPoint; }

        void Clear() override;

        void* GetStageCreateInfo() override
        {
            return m_PipelineStageCreateInfos.data();
        }

        // Vertex
        void AddVertexInputAttributeDescription(u32 location, u32 binding, Format format, u32 offset) override;
        u32 GetVertexInputAttributeDescriptionCount() override;
        void* GetVertexInputAttributeDescriptions() override;
        void ClearVertexBindingDescriptions() override;
        
        void AddVertexBindingDescription(u32 binding, u32 stride, EVertexInputRate inputRate) override;
        u32 GetVertexBindingDescriptionCount() override;
        void* GetVertexBindingDescriptions() override;
        void ClearVertexInputAttributeDescriptions() override;
        
        u32 GetStageCount() override;
       
        // Blend state
    public:

        void AddBlendAttachmentState(bool enable, EBlendFactor srcColor, EBlendFactor dstColor, EBlendOp colorBlendOp,
            EBlendFactor srcAlpha, EBlendFactor dstAlpha, EBlendOp alphaBlendOp, u32 writeMask) override;
        void AddBlendAttachmentState(bool enable, u32 writeMask) override;
        void ClearBlendState() override;
        const u32 GetBlendStateCount() const override;  
        void* GetBlendAttachmentStates() override;

        // Descriptor
        void AddDescriptorSetLayoutBinding(u32 layoutIndex, u32 binding, EDescriptorType type,
            u32 descriptorCount, u32 shaderStageFlags, void* pImmutableSamplers = nullptr) override;
        void ClearDescriptorSetLayoutBindings() override;
        void BuildDescriptorSetLayout() override;
        void* GetDescriptorSetLayouts() override;
        u32 DescriptorSetLayoutCount() override;
        void ClearDescriptorSetLayouts() override;
        
    private:
        
        RHIVkDevice* m_Device;

        EPipelineBindPoint m_BindPoint { EPipelineBindPoint::PIPELINE_BIND_POINT_GRAPHICS };
        
        // stages
        Containers::Vector<VkPipelineShaderStageCreateInfo> m_PipelineStageCreateInfos {};
        Containers::Vector<VkPipelineColorBlendAttachmentState> m_BlendAttachmentStates {};

        // vertex
        Containers::Vector<VkVertexInputBindingDescription> m_VertexInputBindingDescriptions {};
        Containers::Vector<VkVertexInputAttributeDescription> m_VertexInputAttributeDescriptions {};

        // descriptor
        Containers::Map<u32, Containers::Vector<VkDescriptorSetLayoutBinding>> m_DescriptorSetLayoutBindings {};
        Containers::Vector<VkDescriptorSetLayout> m_DescriptorSetLayouts {};
    };
}
