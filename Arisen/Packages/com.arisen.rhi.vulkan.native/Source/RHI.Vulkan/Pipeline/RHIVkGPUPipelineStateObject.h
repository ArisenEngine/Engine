#pragma once
#include "RHI/Pipeline/RHIPipelineState.h"
#include <vulkan/vulkan_core.h>

#include "RHI/Descriptors/RHIDescriptorUpdateInfo.h"
#include "RHI/Handles/RHIHandle.h"

namespace ArisenEngine::RHI
{
    class RHIVkDevice;
}

namespace ArisenEngine::RHI
{
    class RHIVkGPUPipelineStateObject final : public RHIPipelineState
    {
        friend class RHIVkGPUPipeline;
        friend class RHIVkDescriptorPool;

    public:
        friend class RHIVkGPUPipeline;
        NO_COPY_NO_MOVE_NO_DEFAULT(RHIVkGPUPipelineStateObject)
        ~RHIVkGPUPipelineStateObject() noexcept override;
        RHIVkGPUPipelineStateObject(RHIVkDevice* device);
        void AddProgram(RHIShaderProgramHandle handle) override;
        void ClearAllPrograms() override;

        const UInt32 GetHash() const override;
        void SetBindPoint(EPipelineBindPoint bindPoint) override { m_BindPoint = bindPoint; }
        const EPipelineBindPoint GetBindPoint() const override { return m_BindPoint; }

        void Clear() override;

        bool IsMeshPipeline() const override;

        bool IsRayTracingPipeline() const override
        {
            return m_BindPoint == EPipelineBindPoint::PIPELINE_BIND_POINT_RAY_TRACING_KHR;
        }

        void AddRayTracingShaderGroup(const RHIRayTracingShaderGroup& group) override;
        void SetMaxRecursionDepth(UInt32 depth) override { m_MaxRecursionDepth = depth; }
        void AddVertexInputAttributeDescription(UInt32 location, UInt32 binding, EFormat format,
                                                UInt32 offset) override;
        void AddVertexBindingDescription(UInt32 binding, UInt32 stride, EVertexInputRate inputRate) override;
        void ClearVertexInputDescriptions() override;

        // Resource Binding (Auto-Layout compatible)
        void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
                                 const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos) override;
        void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
                                 const Containers::Vector<RHIBufferHandle>&& bufferHandles) override;
        void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
                                 const Containers::Vector<RHIImageViewHandle>&& texelBufferViews) override;
        void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding,
                                 const Containers::Vector<RHIAccelerationStructureHandle>&&
                                 accelerationStructureHandles) override;

        void BuildDescriptorSetLayout() override;

        // Structured State
        void SetTessellationState(const RHITessellationState& state) override { m_TessellationState = state; }
        const RHITessellationState& GetTessellationState() const override { return m_TessellationState; }

        void SetColorBlendState(const RHIColorBlendState& state) override { m_ColorBlendState = state; }
        const RHIColorBlendState& GetColorBlendState() const override { return m_ColorBlendState; }

        void SetRenderingFormats(const Containers::Vector<EFormat>& colorFormats, EFormat depthFormat,
                                 EFormat stencilFormat) override;

    public:
        // Vk API
        VkDescriptorSetLayout GetVkDescriptorSetLayout(UInt32 layoutIndex) const;

        const Containers::Map<UInt32, Containers::UnorderedMap<EDescriptorType, RHIDescriptorUpdateInfo>>&
        GetDescriptorUpdateInfos(UInt32 layoutIndex) const;

        const VkDescriptorSetLayout* GetDescriptorSetLayouts() const;
        UInt32 GetDescriptorSetLayoutCount() const;
        void ClearDescriptorSetLayouts();
        void ClearDescriptorSetLayoutBindings() override;

        VkDescriptorUpdateTemplate GetVkDescriptorUpdateTemplate(UInt32 layoutIndex) const;
        void BuildDescriptorUpdateTemplate(UInt32 layoutIndex);

    private:
        RHIVkDevice* m_Device;

        EPipelineBindPoint m_BindPoint{EPipelineBindPoint::PIPELINE_BIND_POINT_GRAPHICS};

        RHITessellationState m_TessellationState;
        RHIColorBlendState m_ColorBlendState;

        Containers::Vector<VkFormat> m_ColorAttachmentFormats;
        VkFormat m_DepthAttachmentFormat{VK_FORMAT_UNDEFINED};
        VkFormat m_StencilAttachmentFormat{VK_FORMAT_UNDEFINED};

    public:
        bool IsDynamicRendering() const
        {
            return !m_ColorAttachmentFormats.empty() || m_DepthAttachmentFormat != VK_FORMAT_UNDEFINED ||
                m_StencilAttachmentFormat != VK_FORMAT_UNDEFINED;
        }

        void FillRenderingCreateInfo(VkPipelineRenderingCreateInfoKHR& createInfo) const;

    private:
        // stages
        Containers::Vector<VkPipelineShaderStageCreateInfo> m_PipelineStageCreateInfos{};

        // vertex
        Containers::Vector<VkVertexInputBindingDescription> m_VertexInputBindingDescriptions{};
        Containers::Vector<VkVertexInputAttributeDescription> m_VertexInputAttributeDescriptions{};

        // descriptor
        Containers::Map<UInt32, Containers::Vector<VkDescriptorSetLayoutBinding>> m_DescriptorSetLayoutBindings{};
        Containers::Vector<VkDescriptorSetLayout> m_DescriptorSetLayouts{};
        Containers::Vector<VkDescriptorUpdateTemplate> m_DescriptorUpdateTemplates{};

        Containers::Map<UInt32, Containers::Map<UInt32, Containers::UnorderedMap<
                                                    EDescriptorType, RHIDescriptorUpdateInfo>>> m_DescriptorUpdateInfos
            {};

        // push constants
        Containers::Vector<VkPushConstantRange> m_PushConstantRanges{};

        // ray tracing
        Containers::Vector<VkRayTracingShaderGroupCreateInfoKHR> m_RayTracingShaderGroups{};
        UInt32 m_MaxRecursionDepth = 1;

        class RHIVkBindlessDescriptorTable* m_BindlessTable = nullptr;

    public:
        void SetBindlessDescriptorTable(RHIBindlessDescriptorTable* table) override;
        RHIBindlessDescriptorTable* GetBindlessTable() const;
        const Containers::Vector<VkPushConstantRange>& GetPushConstantRanges() const { return m_PushConstantRanges; }

        // Internal helpers for pipeline creation
        UInt32 GetStageCount() const { return static_cast<UInt32>(m_PipelineStageCreateInfos.size()); }
        const VkPipelineShaderStageCreateInfo* GetStageCreateInfos() const { return m_PipelineStageCreateInfos.data(); }

        const Containers::Vector<VkVertexInputBindingDescription>& GetVertexInputBindingDescriptions() const
        {
            return m_VertexInputBindingDescriptions;
        }

        const Containers::Vector<VkVertexInputAttributeDescription>& GetVertexInputAttributeDescriptions() const
        {
            return m_VertexInputAttributeDescriptions;
        }

    private:
        void InternalAddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding,
                                                   EDescriptorType type, UInt32 descriptorCount,
                                                   UInt32 shaderStageFlags);

        void InternalAddDescriptorUpdateInfo(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                             UInt32 descriptorCount,
                                             const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos,
                                             const Containers::Vector<RHIBufferHandle>&& bufferHandles,
                                             const Containers::Vector<RHIImageViewHandle>&& bufferViews,
                                             const Containers::Vector<RHIAccelerationStructureHandle>&&
                                             accelerationStructureHandles);
    };
}
