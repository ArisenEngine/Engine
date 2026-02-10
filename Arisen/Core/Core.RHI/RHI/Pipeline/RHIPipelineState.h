#pragma once
#include "../Descriptors/RHIDescriptorUpdateInfo.h"
#include "Base/FoundationMinimal.h"
#include "RHI/Enums/Image/ESampleCountFlagBits.h"
#include "RHI/Enums/Image/EFormat.h"
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
#include "RHI/Handles/RHIHandle.h"
#include "RHIDepthStencilState.h"
#include "RHIPipelineStates.h"

namespace ArisenEngine::RHI
{
    enum class ERHIRayTracingShaderGroupType
    {
        General = 0,
        TrianglesHitGroup = 1,
        ProceduralHitGroup = 2
    };

    struct RHIRayTracingShaderGroup
    {
        ERHIRayTracingShaderGroupType type;
        UInt32 generalShaderIndex = 0xFFFFFFFF;
        UInt32 closestHitShaderIndex = 0xFFFFFFFF;
        UInt32 anyHitShaderIndex = 0xFFFFFFFF;
        UInt32 intersectionShaderIndex = 0xFFFFFFFF;
    };
}

namespace ArisenEngine::RHI
{
    class RHIPipelineState
    {
        friend class RHIPipeline;
    public:
        NO_COPY_NO_MOVE(RHIPipelineState)
        RHIPipelineState() = default;
        virtual ~RHIPipelineState() noexcept {}

        virtual void AddProgram(RHIShaderProgramHandle handle) = 0;
        virtual void ClearAllPrograms() = 0;

        virtual void SetBindPoint(EPipelineBindPoint bindPoint) = 0;
        virtual const EPipelineBindPoint GetBindPoint() const = 0;

        virtual void Clear() = 0;
        
        virtual void AddVertexInputAttributeDescription(UInt32 location, UInt32 binding, EFormat format, UInt32 offset) = 0;
        virtual void AddVertexBindingDescription(UInt32 binding, UInt32 stride, EVertexInputRate inputRate) = 0;
        virtual void ClearVertexInputDescriptions() = 0;
        virtual void ClearDescriptorSetLayoutBindings() = 0;

        // Resource Binding (Auto-Layout compatible)
        virtual void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding, const Containers::Vector<RHIDescriptorImageInfo>&& imageInfos) = 0;
        virtual void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding, const Containers::Vector<RHIBufferHandle>&& bufferHandles) = 0;
        virtual void UpdateDescriptorSet(UInt32 layoutIndex, UInt32 binding, const Containers::Vector<RHIImageViewHandle>&& texelBufferViews) = 0;

        virtual void BuildDescriptorSetLayout() = 0;

        virtual bool IsMeshPipeline() const = 0;
        virtual bool IsRayTracingPipeline() const = 0;

        virtual void AddRayTracingShaderGroup(const RHIRayTracingShaderGroup& group) = 0;
        virtual void SetMaxRecursionDepth(UInt32 depth) = 0;

        virtual const UInt32 GetHash() const = 0;

    public:
        // Structured State Setters
        void SetInputAssemblyState(const RHIInputAssemblyState& state) { m_InputAssemblyState = state; }
        const RHIInputAssemblyState& GetInputAssemblyState() const { return m_InputAssemblyState; }

        virtual void SetTessellationState(const RHITessellationState& state) = 0;
        virtual const RHITessellationState& GetTessellationState() const = 0;

        void SetRasterizationState(const RHIRasterizationState& state) { m_RasterizationState = state; }
        const RHIRasterizationState& GetRasterizationState() const { return m_RasterizationState; }

        void SetMultisampleState(const RHIMultisampleState& state) { m_MultisampleState = state; }
        const RHIMultisampleState& GetMultisampleState() const { return m_MultisampleState; }

        void SetDepthStencilState(const RHIDepthStencilState& state) { m_DepthStencilState = state; }
        const RHIDepthStencilState& GetDepthStencilState() const { return m_DepthStencilState; }

        virtual void SetColorBlendState(const RHIColorBlendState& state) = 0;
        virtual const RHIColorBlendState& GetColorBlendState() const = 0;

        void SetDynamicStateMask(UInt64 mask) { m_DynamicStateMask = mask; }
        UInt64 GetDynamicStateMask() const { return m_DynamicStateMask; }

        virtual void SetRenderingFormats(const Containers::Vector<EFormat>& colorFormats, EFormat depthFormat, EFormat stencilFormat) = 0;

    private:
        RHIInputAssemblyState m_InputAssemblyState;
        RHIRasterizationState m_RasterizationState;
        RHIMultisampleState m_MultisampleState;
        RHIDepthStencilState m_DepthStencilState;
        UInt64 m_DynamicStateMask { 0 };
    };
}

