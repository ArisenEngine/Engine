#pragma once
#include "ResourceD3D12.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
#include "TextureBase.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
class TextureD3D12 final : public ResourceD3D12<TextureBase<RHID3D12ImplTraits>, TextureSettings>
{
public:
    TextureD3D12(const IRHIContext& context, const TextureSettings& settings);

private:
    void CreateAsFrameBuffer();
};
ARISENRHI_D3D12_END_NAMESPACE
