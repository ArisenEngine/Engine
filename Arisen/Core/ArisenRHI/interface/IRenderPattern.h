#pragma once
#include "CoreMinimalRHI.h"
#include "EnumMask.h"
#include "IObject.h"
#include "RHITypes.h"

ARISENRHI_BEGIN_NAMEPSACE
struct RenderPassAttachment
{
    enum class Type:uint8_t
    {
        Color = 0u,
        Depth,
        Stencil
    };

    enum class LoadAction : uint8_t
    {
        DontCare = 0u,
        Load,
        Clear
    };

    enum class StoreAction : uint8_t
    {
        DontCare = 0u,
        Store,
        Resolve,
    };

    int32_t attachment_index = 0u;
    TextureFormat format = TextureFormat::UnKnown;
    LoadAction load_action = LoadAction::DontCare;
    StoreAction store_action = StoreAction::DontCare;
};

struct RenderPassColorAttachment
{
    
};

enum class RenderPassAccess : uint32_t
{
    ShaderResources,
    Samplers,
    RenderTargets,
    DepthStencil,
};

struct RenderPatternSettings
{
    EnumMask<RenderPassAccess> shader_access_mask; 
};

struct IRenderPattern : public IObject
{
};
ARISENRHI_END_NAMESPACE
