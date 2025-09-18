#pragma once
#include "ResourceD3D12.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
#include "TextureBase.h"


ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderContextD3D12;

class TextureD3D12 final : public ResourceD3D12<TextureBase<RHID3D12ImplTraits>, TextureSettings>
{
public:
    TextureD3D12(const IRHIContext& context, const TextureSettings& settings);

    const RenderContextD3D12& GetRenderContextD3D12()const;
private:
    void CreateAsFrameBuffer();
};
ARISENRHI_D3D12_END_NAMESPACE
