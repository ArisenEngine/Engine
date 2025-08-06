#pragma once
#include "ContextBase.h"
#include "CoreMinimalRHI.h"
#include "IDevice.h"
#include "IRenderContext.h"

ARISENRHI_BEGIN_NAMEPSACE
template<typename TContextInterface, typename TSettings>
class RenderContextBase : public ContextBase<TContextInterface>
{
public:
    RenderContextBase(IDevice& device, const TSettings& settings)
    :ContextBase<TContextInterface>(device, ContextType::Render)
    ,m_settings(settings)
    {
    }

    virtual void Initialize() override
    {
        
    }

    const virtual TSettings& GetSettings() const noexcept override final {return m_settings;}
    virtual ContextOption GetOptions() const noexcept override
    {
        return m_settings.options;
    }
private:
    TSettings m_settings;
    
};
ARISENRHI_END_NAMESPACE
