#pragma once
#include "CoreMinimalRHI.h"
#include "IRenderContext.h"
#include "IRenderPattern.h"
#include "ObjectBase.h"

ARISENRHI_BEGIN_NAMEPSACE
    struct IRenderContext;

    template<typename RHIImplTraits> requires std::is_base_of_v<IRenderPattern, typename RHIImplTraits::RenderPatternInterface>
class RenderPatternBase : public ObjectBase<typename RHIImplTraits::RenderPatternInterface>
{
public:
    RenderPatternBase(IRenderContext& render_context, const RenderPatternSettings& settings)
        :m_render_context_ptr(render_context.GetInterface<IRenderContext>()), m_settings(settings)
        {
        }

        // IRenderPattern
        [[nodiscard]] AttachmentFormats GetAttachmentFormats() const override final
        {
            AttachmentFormats formats;

            formats.colors.reserve(m_settings.color_attachments.size());
            std::ranges::transform(m_settings.color_attachments, std::back_inserter(formats.colors), [](const auto& attachment) {return attachment.format;});

            if (m_settings.depth_attachment)
            {
                formats.depth = m_settings.depth_attachment->format;
            }

            if (m_settings.stencil_attachment)
            {
                formats.stencil = m_settings.stencil_attachment->format;
            }

            return formats;
        }
private:
    const Ptr<IRenderContext> m_render_context_ptr;
    const RenderPatternSettings m_settings;
};
ARISENRHI_END_NAMESPACE
