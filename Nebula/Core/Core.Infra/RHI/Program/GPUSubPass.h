#pragma once

#include "../../Common/CommandHeaders.h"
#include "RHI/Enums/Pipeline/EPipelineStageFlag.h"
#include "RHI/Enums/Subpass/ESubpassDescFlag.h"

namespace NebulaEngine::RHI
{
    typedef struct SubpassDescription
    {
        EPipelineBindPoint bindPoint;
        u32 colorRefCount;
        void* colorReferences;
        u32 preserveCount;
        void* preserves;
        std::optional<u32> inputRefCount;
        std::optional<void*> inputReferences;
        std::optional<void*> resolveReference;
        std::optional<void*> depthStencilReference;
        std::optional<ESubpassDescriptionFlag> flag;
        
    } SubpassDescription;

    typedef struct SubpassDependency
    {
        u32 previousIndex;
        u32 previousStage;
        u32 previousAccessMask;
        u32 currentStage;
        u32 currentAccessMask;
        u32 syncFlag;
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
        
        virtual void AddInputReference(u32 index, ImageLayout layout) = 0;
        virtual void AddColorReference(u32 index, ImageLayout layout) = 0;
        virtual void SetResolveReference(u32 index, ImageLayout layout) = 0;
        virtual void SetDepthStencilReference(u32 index, ImageLayout layout) = 0;

        /// \brief set the subPass dependency
        /// \param preIndex indicate that current subPass is dependent to which subPass or outSide the render pass
        /// \param preStage indicate that curr subPass dependent to which stage
        /// \param preAccessMask indicate that stage access mask which curr subPass dependent to 
        /// \param currStage curr subPass stage 
        /// \param currAccessMask  curr subPass stage access mask
        /// \param syncFlag sync flags
        void SetDependency(u32 preIndex, u32 preStage, u32 preAccessMask, u32 currStage, u32 currAccessMask, u32 syncFlag)
        {
            m_Dependency.previousIndex = preIndex;
            m_Dependency.previousStage = preStage;
            m_Dependency.previousAccessMask = preAccessMask;
            m_Dependency.currentStage = currStage;
            m_Dependency.currentAccessMask = currAccessMask;
            m_Dependency.syncFlag = syncFlag;
        }
        
        virtual void ClearAll() = 0;

        virtual SubpassDescription GetDescriptions() = 0;
        SubpassDependency GetDependency() const { return m_Dependency; }

        virtual const u32 GetIndex() const = 0;
    
    private:

        friend GPURenderPass;
        virtual void Bind(EPipelineBindPoint bindPoint, u32 index) = 0;
        GPURenderPass* m_OwnerPass;
        SubpassDependency m_Dependency {};
    };
}
