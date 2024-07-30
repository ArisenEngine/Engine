#include "RHIVkGPUSubPass.h"

#include "Logger/Logger.h"

NebulaEngine::RHI::RHIVkGPUSubPass::RHIVkGPUSubPass(RHIVkGPURenderPass* renderPass, EPipelineBindPoint bindPoint, u32 index):
GPUSubPass(renderPass), m_BindPoint(bindPoint), m_Index(index)
{
    m_PreserveAttachments.resize(renderPass->GetAttachmentCount());
    for(int i = 0; i < m_PreserveAttachments.size(); ++i)
    {
        m_PreserveAttachments[i] = i;
    }
}

NebulaEngine::RHI::RHIVkGPUSubPass::~RHIVkGPUSubPass()
{
    LOG_DEBUG("[RHIVkGPUSubPass::~RHIVkGPUSubPass]: ~RHIVkGPUSubPass");
    ClearAll();
}

void NebulaEngine::RHI::RHIVkGPUSubPass::Bind(EPipelineBindPoint bindPoint, u32 index)
{
    m_BindPoint = bindPoint;
    m_Index = index;
}

void NebulaEngine::RHI::RHIVkGPUSubPass::AddInputReference(u32 index, ImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
}

void NebulaEngine::RHI::RHIVkGPUSubPass::AddColorReference(u32 index, ImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
}

void NebulaEngine::RHI::RHIVkGPUSubPass::SetResolveReference(u32 index, ImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
}

void NebulaEngine::RHI::RHIVkGPUSubPass::SetDepthStencilReference(u32 index, ImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
}

void NebulaEngine::RHI::RHIVkGPUSubPass::ClearAll()
{
    m_PreserveAttachments.clear();
    m_ColorReferences.clear();
    m_InputReferences.clear();

    m_ResolveReference = { };
    m_DepthStencilReference = { };
    m_PreserveAttachments  = { };
}

NebulaEngine::RHI::SubpassDescription NebulaEngine::RHI::RHIVkGPUSubPass::GetDescriptions()
{
    return SubpassDescription{};
}

void NebulaEngine::RHI::RHIVkGPUSubPass::RemovePreserve(u32 index)
{
    const auto it = std::ranges::find(m_PreserveAttachments, index);
    if (it != m_PreserveAttachments.end())
    {
        m_PreserveAttachments.erase(it);
    } 
}

bool NebulaEngine::RHI::RHIVkGPUSubPass::IsInsidePreserve(u32 index)
{
    const auto it = std::ranges::find(m_PreserveAttachments, index);
    return it != m_PreserveAttachments.end();
}


