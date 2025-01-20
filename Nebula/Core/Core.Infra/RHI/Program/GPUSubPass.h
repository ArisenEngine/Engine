#pragma once

#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Pipeline/EDynamicState.h"
#include "RHI/Enums/Pipeline/EPipelineBindPoint.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Pipeline/EPrimitiveTopology.h"
#include "RHI/Enums/Pipeline/EVertexInputRate.h"
#include "RHI/Enums/Subpass/ESubpassDescFlag.h"

namespace NebulaEngine::RHI
{
    typedef struct SubpassDescription
    {
        EPipelineBindPoint bindPoint;
        UInt32 colorRefCount;
        void* colorReferences;
        UInt32 preserveCount;
        void* preserves;
        std::optional<UInt32> inputRefCount;
        std::optional<void*> inputReferences;
        std::optional<void*> resolveReference;
        std::optional<void*> depthStencilReference;
        std::optional<UInt32> flag;
        
    } SubpassDescription;

    typedef struct SubpassDependency
    {
        UInt32 previousIndex;
        UInt32 previousStage;
        UInt32 previousAccessMask;
        UInt32 currentStage;
        UInt32 currentAccessMask;
        UInt32 syncFlag;
    } SubpassDependency;
    
    class GPUSubPass
    {
    public:
        NO_COPY_NO_MOVE_NO_DEFAULT(GPUSubPass)
        GPUSubPass(GPURenderPass* parent): m_OwnerPass(parent) {}
        virtual ~GPUSubPass() noexcept
        {
            m_OwnerPass = nullptr;
        }
        
        virtual void AddInputReference(UInt32 index, EImageLayout layout) = 0;
        virtual void AddColorReference(UInt32 index, EImageLayout layout) = 0;
        virtual void SetResolveReference(UInt32 index, EImageLayout layout) = 0;
        virtual void SetDepthStencilReference(UInt32 index, EImageLayout layout) = 0;
        
        virtual void ClearAll() = 0;

        virtual SubpassDescription GetDescriptions() = 0;
        SubpassDependency GetDependency() const { return m_Dependency; }

        virtual const UInt32 GetIndex() const = 0;
        
    public:

        /// \brief set the subPass dependency
        /// \param preIndex indicate that current subPass is dependent to which subPass or outSide the render pass
        /// \param preStage indicate that curr subPass dependent to which stage
        /// \param preAccessMask indicate that stage access mask which curr subPass dependent to 
        /// \param currStage curr subPass stage 
        /// \param currAccessMask  curr subPass stage access mask
        /// \param syncFlag sync flags
        void SetDependency(UInt32 preIndex, UInt32 preStage, UInt32 preAccessMask, UInt32 currStage, UInt32 currAccessMask, UInt32 syncFlag)
        {
            m_Dependency.previousIndex = preIndex;
            m_Dependency.previousStage = preStage;
            m_Dependency.previousAccessMask = preAccessMask;
            m_Dependency.currentStage = currStage;
            m_Dependency.currentAccessMask = currAccessMask;
            m_Dependency.syncFlag = syncFlag;
        }
        
        GPURenderPass* GetOwner() const { return m_OwnerPass; }

        EPipelineBindPoint GetBindPoint() const { return m_BindPoint; }
        void SetBindPoint(EPipelineBindPoint point) { m_BindPoint = point; }
        UInt32 GetSubPassDescriptionFlag() const { return m_SubPassDescriptionFlag; }
        void SetSubPassDescriptionFlag(UInt32 flag) { m_SubPassDescriptionFlag = flag; }
    protected:
        GPURenderPass* m_OwnerPass;
    private:

        friend GPURenderPass;
        virtual void Bind(UInt32 index) = 0;
        SubpassDependency m_Dependency {};
        EPipelineBindPoint m_BindPoint;
        UInt32 m_SubPassDescriptionFlag;
        
        
    };
}
