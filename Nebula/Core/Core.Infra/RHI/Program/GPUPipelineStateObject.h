#pragma once
#include "DescriptorUpdateInfo.h"
#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Attachment/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/Format.h"
#include "RHI/Enums/Pipeline/EBlendFactor.h"
#include "RHI/Enums/Pipeline/EBlendOp.h"
#include "RHI/Enums/Pipeline/ECullMode.h"
#include "RHI/Enums/Pipeline/EDescriptorType.h"
#include "RHI/Enums/Pipeline/EDynamicState.h"
#include "RHI/Enums/Pipeline/EFrontFace.h"
#include "RHI/Enums/Pipeline/ELogicOp.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Enums/Pipeline/EPolygonMode.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Pipeline/EVertexInputRate.h"

namespace NebulaEngine::RHI
{
    using ImmutableSamplers = void;
    class GPUPipelineStateObject
    {
        friend class GPUPipeline;
    public:
        NO_COPY_NO_MOVE(GPUPipelineStateObject)
        GPUPipelineStateObject() = default;
        virtual ~GPUPipelineStateObject() noexcept { ClearDynamicPipelineStates(); }

        virtual void AddProgram(UInt32 programId) = 0;
        virtual void ClearAllPrograms() = 0;

        virtual const UInt32 GetHash() const = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;

        virtual void Clear() = 0;
        
        virtual void AddVertexInputAttributeDescription(UInt32 location, UInt32 binding, Format format, UInt32 offset) = 0;
        virtual UInt32 GetVertexInputAttributeDescriptionCount() = 0;
        virtual void* GetVertexInputAttributeDescriptions() = 0;
        virtual void ClearVertexInputAttributeDescriptions() = 0;

        virtual void AddVertexBindingDescription(UInt32 binding, UInt32 stride, EVertexInputRate inputRate) = 0;
        virtual UInt32 GetVertexBindingDescriptionCount() = 0;
        virtual void* GetVertexBindingDescriptions() = 0;
        virtual void ClearVertexBindingDescriptions() = 0;
        
        virtual void AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
            UInt32 descriptorCount, UInt32 shaderStageFlags, DescriptorImageInfo* pImageInfos, ImmutableSamplers* pImmutableSamplers = nullptr) = 0;
        virtual void AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                                   UInt32 descriptorCount, UInt32 shaderStageFlags, DescriptorBufferInfo* pBufferInfos) = 0;
        virtual void AddDescriptorSetLayoutBinding(UInt32 layoutIndex, UInt32 binding, EDescriptorType type,
                                                   UInt32 descriptorCount, UInt32 shaderStageFlags, BufferView* pTexelBufferView) = 0;
        virtual void ClearDescriptorSetLayoutBindings() = 0;
       
        virtual void* GetDescriptorSetLayouts() = 0;
        virtual UInt32 DescriptorSetLayoutCount() = 0;
        virtual void ClearDescriptorSetLayouts() = 0;
        
        virtual UInt32 GetStageCount() = 0;
        virtual void* GetStageCreateInfo() = 0;
        virtual void BuildDescriptorSetLayout() = 0;
        virtual Containers::Map<UInt32, Containers::Map<UInt32, Containers::UnorderedMap<EDescriptorType, DescriptorUpdateInfo>>>
        GetAllDescriptorUpdateInfos() const = 0;

    public:

        void ClearDynamicPipelineStates() { m_DynamicPipelineStates.clear(); }
        void AddDynamicPipelineState(EDynamicPipelineState state)
        {
            m_DynamicPipelineStates.insert(state);
        }
        
        const bool HasDynamicStates(EDynamicPipelineState state) const
        {
            return m_DynamicPipelineStates.contains(state);
        }
        
        const Containers::UnorderSet<EDynamicPipelineState>& GetDynamicStates() const
        {
            return m_DynamicPipelineStates;
        }
        
        void SetPrimitiveState(EPrimitiveTopology topology, bool primitiveRestart)
        {
            m_PrimitiveTopology = topology;
            m_PrimitiveRestart = primitiveRestart;
        }

        const EPrimitiveTopology GetTopology() const { return m_PrimitiveTopology; }
        const bool IsPrimitiveRestart() const { return m_PrimitiveRestart; }


        // Rasterizer
        bool DepthClampEnable() const { return m_DepthClampEnable; }
        void SetDepthClampEnable(bool enable) { m_DepthClampEnable = enable; }
        bool RasterizerDiscardEnable() const { return m_RasterizerDiscardEnable; }
        void SetRasterizerDiscardEnable(bool enable) { m_RasterizerDiscardEnable = enable; }
        EPolygonMode PolygonMode() const { return m_PolygonMode; }
        void SetPolygonMode(EPolygonMode mode) { m_PolygonMode = mode; }
        ECullModeFlagBits CullMode() const { return m_CullMode; }
        void SetCullMode(ECullModeFlagBits cull) { m_CullMode = cull; }
        EFrontFace FrontFace() { return m_FrontFace; }
        void SetFrontFace(EFrontFace face) { m_FrontFace = face; }
        bool DepthBiasEnable() const { return m_DepthBiasEnable; }
        void SetDepthBiasEnable(bool enable) { m_DepthBiasEnable = enable; }
        void SetDepthBiasConstantFactor(Float32 factor) { m_DepthBiasConstantFactor = factor; }
        Float32 DepthBiasConstantFactor() const { return m_DepthBiasConstantFactor; }
        void SetDepthBiasClamp(Float32 clamp) { m_DepthBiasClamp = clamp; }
        Float32 DepthBiasClamp() const { return m_DepthBiasClamp; }
        void SetDepthBiasSlopeFactor(Float32 slopeFactor) { m_DepthBiasSlopeFactor = slopeFactor; }
        Float32 DepthBiasSlopeFactor() const { return m_DepthBiasSlopeFactor; }
        void SetLineWidth(Float32 lineWidth) { m_LineWidth = lineWidth; }
        Float32 LineWidth() const { return m_LineWidth; }


        // Multiple sampling
        ESampleCountFlagBits SampleCount() const { return m_SampleCount; }
        void SetSampleCount(ESampleCountFlagBits sampleCount) { m_SampleCount = sampleCount; }
        bool SampleShadingEnable() const { return m_SampleShadingEnable; }
        void SetSampleShading(bool enable) { m_SampleShadingEnable = enable; }
        Float32 MinSampleShading() const { return m_MinSampleShading; }
        void SetMinSampleShading(Float32 sampleShading) { m_MinSampleShading = sampleShading; }
        UInt64* SampleMask() { return &m_SampleMask; }
        void SetSampleMask(UInt64 sampleMask) { m_SampleMask = sampleMask; }
        bool AlphaToCoverage() const { return m_AlphaToCoverage; }
        void SetAlphaToCoverage(bool alphaToCoverage) { m_AlphaToCoverage = alphaToCoverage; }
        bool AlphaToOne() const { return m_AlphaToOne; }
        void SetAlphaToOne(bool alphaToOne) { m_AlphaToOne = alphaToOne; }

        // blend state
        virtual void AddBlendAttachmentState(bool enable, EBlendFactor srcColor, EBlendFactor dstColor, EBlendOp colorBlendOp,
            EBlendFactor srcAlpha, EBlendFactor dstAlpha, EBlendOp alphaBlendOp, UInt32 writeMask) = 0;
        virtual void AddBlendAttachmentState(bool enable, UInt32 writeMask) = 0;
        virtual void ClearBlendState() = 0;
        virtual const UInt32 GetBlendStateCount() const = 0;  
        virtual void* GetBlendAttachmentStates() = 0;
        void SetLogicOp(bool enable, ELogicOp op) { m_LogicOp = op; m_LogicOpEnable = enable; }
        ELogicOp LogicOp() const { return m_LogicOp; }
        bool IsLogicOpEnable() const { return m_LogicOpEnable; }
        void SetBlendConstants(Float32 r, Float32 g, Float32 b, Float32 a)
        {
            m_BlendConstants[0] = r;
            m_BlendConstants[1] = g;
            m_BlendConstants[2] = b;
            m_BlendConstants[3] = a;
        }

        Float32 BlendConstantR() const { return  m_BlendConstants[0]; }
        Float32 BlendConstantG() const { return  m_BlendConstants[1]; }
        Float32 BlendConstantB() const { return  m_BlendConstants[2]; }
        Float32 BlendConstantA() const { return  m_BlendConstants[3]; }

    private:

        bool m_PrimitiveRestart {false};
        EPrimitiveTopology m_PrimitiveTopology { PRIMITIVE_TOPOLOGY_TRIANGLE_LIST };
        Containers::UnorderSet<EDynamicPipelineState> m_DynamicPipelineStates;

        // Rasterizer State
        bool m_DepthClampEnable { false };
        bool m_RasterizerDiscardEnable { false };
        EPolygonMode m_PolygonMode { EPOLYGON_MODE_FILL };
        ECullModeFlagBits m_CullMode { ECullModeFlagBits::CULL_MODE_BACK_BIT };
        EFrontFace m_FrontFace { EFrontFace::FRONT_FACE_CLOCKWISE };
        bool m_DepthBiasEnable { false };
        Float32 m_DepthBiasConstantFactor { 1.0f };
        Float32 m_DepthBiasClamp { 0.0f };
        Float32 m_DepthBiasSlopeFactor { 1.0f };
        Float32 m_LineWidth { 1.0f };

        // multiple sampling
        ESampleCountFlagBits m_SampleCount {ESampleCountFlagBits::SAMPLE_COUNT_1_BIT};
        bool m_SampleShadingEnable { false };
        Float32 m_MinSampleShading { 0.0f };
        UInt64 m_SampleMask { 0xFFFF };
        bool m_AlphaToCoverage { false };
        bool m_AlphaToOne { false };

        // blend state
        bool m_LogicOpEnable { false };
        ELogicOp m_LogicOp { ELogicOp::LOGIC_OP_NO_OP };
        Float32 m_BlendConstants[4] {1.f, 1.f, 1.f, 1.f};
    };
}
