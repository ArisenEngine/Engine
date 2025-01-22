#include "RHIVkGPUSubPass.h"

#include "Logger/Logger.h"

ArisenEngine::RHI::RHIVkGPUSubPass::RHIVkGPUSubPass(RHIVkGPURenderPass* renderPass, UInt32 index):
GPUSubPass(renderPass), m_Index(index)
{
    ClearAll();
    ResizePreserve();
}

ArisenEngine::RHI::RHIVkGPUSubPass::~RHIVkGPUSubPass()
{
    LOG_DEBUG("[RHIVkGPUSubPass::~RHIVkGPUSubPass]: ~RHIVkGPUSubPass");
    ClearAll();
}

void ArisenEngine::RHI::RHIVkGPUSubPass::Bind(UInt32 index)
{
    m_Index = index;
    ClearAll();
    ResizePreserve();
}

void ArisenEngine::RHI::RHIVkGPUSubPass::AddInputReference(UInt32 index, EImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
    m_InputReferences.emplace_back(VkAttachmentReference{index, static_cast<VkImageLayout>(layout)});
}

void ArisenEngine::RHI::RHIVkGPUSubPass::AddColorReference(UInt32 index, EImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
    m_ColorReferences.emplace_back(VkAttachmentReference{index, static_cast<VkImageLayout>(layout)});
}

void ArisenEngine::RHI::RHIVkGPUSubPass::SetResolveReference(UInt32 index, EImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
    m_ResolveReference.attachment = static_cast<uint32_t>(index);
    m_ResolveReference.layout = static_cast<VkImageLayout>(layout);
}

void ArisenEngine::RHI::RHIVkGPUSubPass::SetDepthStencilReference(UInt32 index, EImageLayout layout)
{
    ASSERT(IsInsidePreserve(index));
    RemovePreserve(index);
    m_DepthStencilReference.attachment = static_cast<uint32_t>(index);
    m_DepthStencilReference.layout = static_cast<VkImageLayout>(layout);
}

void ArisenEngine::RHI::RHIVkGPUSubPass::ClearAll()
{
    m_PreserveAttachments.clear();
    m_ColorReferences.clear();
    m_InputReferences.clear();

    m_ResolveReference = { u32Invalid };
    m_DepthStencilReference = { u32Invalid };
}

ArisenEngine::RHI::SubpassDescription ArisenEngine::RHI::RHIVkGPUSubPass::GetDescriptions()
{
    SubpassDescription description {};
    description.bindPoint = GetBindPoint();
    description.colorRefCount = static_cast<UInt32>(m_ColorReferences.size());
    description.colorReferences = m_ColorReferences.data();
    description.preserveCount = static_cast<UInt32>(m_PreserveAttachments.size());
    description.preserves = m_PreserveAttachments.data();

    if (m_InputReferences.size() > 0)
    {
        description.inputRefCount = static_cast<const std::optional<UInt32>&>(m_InputReferences.size());
        description.inputReferences = m_InputReferences.data();
    }

    if (m_ResolveReference.attachment != u32Invalid)
    {
        description.resolveReference = &m_ResolveReference;
    }

    if (m_DepthStencilReference.attachment != u32Invalid)
    {
        description.depthStencilReference = &m_DepthStencilReference;
    }
    description.flag = GetSubPassDescriptionFlag();
    
    return description;
}

void ArisenEngine::RHI::RHIVkGPUSubPass::RemovePreserve(UInt32 index)
{
    const auto it = std::ranges::find(m_PreserveAttachments, index);
    if (it != m_PreserveAttachments.end())
    {
        m_PreserveAttachments.erase(it);
    } 
}

void ArisenEngine::RHI::RHIVkGPUSubPass::ResizePreserve()
{
    m_PreserveAttachments.resize(static_cast<std::vector<unsigned>::size_type>(m_OwnerPass->GetAttachmentCount()));
    for(int i = 0; i < m_PreserveAttachments.size(); ++i)
    {
        m_PreserveAttachments[i] = i;
    }
}

bool ArisenEngine::RHI::RHIVkGPUSubPass::IsInsidePreserve(UInt32 index)
{
    const auto it = std::ranges::find(m_PreserveAttachments, index);
    return it != m_PreserveAttachments.end();
}


