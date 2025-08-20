#pragma once
#include "ContextBase.h"
#include "CoreMinimalRHI.h"
#include "IDevice.h"
#include "IRenderContext.h"
#include "DebugUtils/Verifies.h"

ARISENRHI_BEGIN_NAMEPSACE
    template<typename RHIImplTraits> requires std::is_base_of_v<IRenderContext, typename RHIImplTraits::RenderContextInterface>
class RenderContextBase : public ContextBase<RHIImplTraits, typename RHIImplTraits::RenderContextInterface>
{
public:
    using BaseInterface = typename RHIImplTraits::RenderContextInterface;
    using DescriptorManagerImplType = typename RHIImplTraits::DescriptorManagerImplType;
    
    RenderContextBase(IDevice& device, const RenderContextSettings& settings, UniquePtr<DescriptorManagerImplType>&& descriptor_manager_ptr)
        :ContextBase<RHIImplTraits, BaseInterface>(device, ContextType::Render, std::move(descriptor_manager_ptr))
        ,m_settings(settings)
    {
    }

    virtual void Initialize() override
    {
        ContextBase<RHIImplTraits, BaseInterface>::Initialize();
        m_frame_index = 0u;
    }

    const virtual RenderContextSettings& GetSettings() const noexcept override final {return m_settings;}
    virtual ContextOption GetOptions() const noexcept override
    {
        return m_settings.options;
    }

protected:
    void UpdateFrameBufferIndex()
    {
        m_frame_buffer_index = GetNextFrameBufferIndex();
        VERIFY_LESS(m_frame_buffer_index, m_settings.frame_buffers_Count,"");
        m_frame_index++;
    }

    virtual uint32_t GetNextFrameBufferIndex()
    {
        return (m_frame_index + 1) % m_settings.frame_buffers_Count;
    }
private:
    RenderContextSettings m_settings;
    uint32_t m_frame_buffer_index = 0u;
    uint32_t m_frame_index = 0u;
};
ARISENRHI_END_NAMESPACE
