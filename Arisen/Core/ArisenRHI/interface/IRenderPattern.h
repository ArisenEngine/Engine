#pragma once
#include "CoreMinimalRHI.h"
#include "DataType.h"
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

    RenderPassAttachment() = default;
    RenderPassAttachment(int32_t attachment_index,TextureFormat format, LoadAction load_action = LoadAction::DontCare, StoreAction store_action = StoreAction::DontCare)
        :attachment_index(attachment_index),format(format),load_action(load_action),store_action(store_action)
    {}

    int32_t attachment_index = 0u;
    TextureFormat format = TextureFormat::UnKnown;
    LoadAction load_action = LoadAction::DontCare;
    StoreAction store_action = StoreAction::DontCare;
};

struct RenderPassColorAttachment final: RenderPassAttachment
{
    Vector4F clear_color;
    RenderPassColorAttachment(int32_t attachment_index, TextureFormat format
        , LoadAction load_action = LoadAction::DontCare, StoreAction store_action = StoreAction::DontCare, const Vector4F& clear_color = Vector4F())
        :RenderPassAttachment(attachment_index, format, load_action, store_action)
        ,clear_color(clear_color)
    {}
    
};

struct RenderPassDepthAttachment final : RenderPassAttachment
{
    float clear_value = 1.0f;

    RenderPassDepthAttachment() = default;
    RenderPassDepthAttachment(int32_t attachment_index, TextureFormat format
        , LoadAction load_action = LoadAction::DontCare, StoreAction store_action = StoreAction::DontCare, float clear_value = 1.0f)
        :RenderPassAttachment(attachment_index, format, load_action, store_action)
        ,clear_value(clear_value)
    {}
};

struct RenderPassStencilAttachment final : RenderPassAttachment
{
    uint8_t clear_value = 0u;

    RenderPassStencilAttachment() = default;
    RenderPassStencilAttachment(int32_t attachment_index, TextureFormat format
        , LoadAction load_action = LoadAction::DontCare, StoreAction store_action = StoreAction::DontCare, uint8_t clear_value = 0u)
        :RenderPassAttachment(attachment_index, format, load_action, store_action)
        ,clear_value(clear_value)
    {}
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
    std::vector<RenderPassColorAttachment> color_attachments;
    Opt<RenderPassDepthAttachment> depth_attachment;
    Opt<RenderPassStencilAttachment> stencil_attachment;
    
    EnumMask<RenderPassAccess> shader_access_mask;
    bool is_final_pass = true;
};

struct IRenderPattern : public IObject
{
    [[nodiscard]] virtual AttachmentFormats GetAttachmentFormats() const = 0;
};
ARISENRHI_END_NAMESPACE
