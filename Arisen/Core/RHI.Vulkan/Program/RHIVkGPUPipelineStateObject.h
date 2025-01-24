#pragma once
#include "RHI/Program/GPUPipelineStateObject.h"
#include <vulkan/vulkan_core.h>

#include "RHI/Program/RHIDescriptorUpdateInfo.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
  
    
}

namespace ArisenEngine::RHI
{
    class RHIVkGPUPipelineStateObject final : public GPUPipelineStateObject
    {
        friend class RHIVkGPUPipeline;
        friend class RHIVkDescriptorPool;
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipelineStateObject)
        ~RHIVkGPUPipelineStateObject() noexcept override;
        RHIVkGPUPipelineStateObject(RHIVkDevice* device);
        void AddProgram(UInt32 programId) override;
        void ClearAllPrograms() override;

        const UInt32 GetHash() const override;
        const EPipelineBindPoint GetBindPoint() const override { return m_BindPoint; }

        void Clear() override;

        void* GetStageCreateInfo() override
        {
            return m_PipelineStageCreateInfos.data();
        }

        // Vertex
        void AddVertexInputAttributeDescription(UInt32 location, UInt32 binding, Format format, UInt32 offset) override;
        UInt32 GetVertexInputAttributeDescriptionCount() override;
        void* GetVertexInputAttributeDescriptions() override;
        void ClearVertexBindingDescriptions() override;
        
        void AddVertexBindingDescription(UInt32 binding, UInt32 stride, EVertexInputRate inputRate) override;
        UInt32 GetVertexBindingDescriptionCount() override;
        void* GetVertexBindingDescriptions() override;
        void ClearVertexInputAttributeDescriptions() override;
        
        UInt32 GetStageCount() override;
       
        // Blend state
    public:

        void AddBlendAttachmentState(bool enable, EBlendFactor srcColor, EBlendFactor dstColor, EBlendOp colorBlendOp,
            EBlendFactor srcAlpha, EBlendFactor dstAlpha, EBlendOp alphaBlendOp, UInt32 writeMask) override;
        void AddBlendAttachmentState(bool enable, UInt32 writeMask) override;
        void ClearBlendState() override;
        const UInt32 GetBlendStateCount() const override;  
        void* GetBlendAttachmentStates() override;

        // Descriptor
        void AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
            UInt32 descriptorCount, UInt32 shaderStageFlags, const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos,
             ImmutableSamplers* pImmutableSamplers = nullptr) override;
        void AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                                   UInt32 descriptorCount, UInt32 shaderStageFlags,
                                                   const Containers::Vector<std::shared_ptr<BufferHandle>>&& bufferHandles) override;
        void AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                                  UInt32 descriptorCount, UInt32 shaderStageFlags,
                                                  const Containers::Vector<BufferView*>&& bufferViews) override;
        
    private:
        
        void InternalAddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding,
    EDescriptorType type, UInt32 descriptorCount, UInt32 shaderStageFlags, ImmutableSamplers* pImmutableSamplers);
        
        void InternalAddDescriptorUpdateInfo(UInt32 layoutIndex, UInt32 binding,EDescriptorType type,
            UInt32 descriptorCount, const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos,
            const Containers::Vector<std::shared_ptr<BufferHandle>>&& bufferHandles, const Containers::Vector<BufferView*>&& bufferViews);
        
    public:
        
        void ClearDescriptorSetLayoutBindings() override;
      
        void* GetDescriptorSetLayouts() override;
        UInt32 DescriptorSetLayoutCount() override;
        void ClearDescriptorSetLayouts() override;
        void BuildDescriptorSetLayout() override;

    public:
        // Vk API
        VkDescriptorSetLayout GetVkDescriptorSetLayout(UInt32 layoutIndex) const;

        Containers::Map<UInt32, Containers::UnorderedMap<EDescriptorType, RHIDescriptorUpdateInfo>>
        GetDescriptorUpdateInfos(UInt32 layoutIndex) const;
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
        Containers::Map<UInt32, Containers::Vector<VkDescriptorSetLayoutBinding>> m_DescriptorSetLayoutBindings {};
        Containers::Vector<VkDescriptorSetLayout> m_DescriptorSetLayouts {};
        // layout index,
        //     binding index,
        //             Descriptor Type - RHIDescriptorUpdateInfo
        //               
        Containers::Map<UInt32, Containers::Map<UInt32, Containers::UnorderedMap<EDescriptorType, RHIDescriptorUpdateInfo>>> m_DescriptorUpdateInfos {};
    };
}
