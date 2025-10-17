#pragma once
#include <d3d12shader.h>

#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
#include "ShaderBase.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class ShaderD3D12 final : public ShaderBase<RHID3D12ImplTraits>
{
public:
    ShaderD3D12(ShaderType type, const IRHIContext& context, const ShaderSettings& settings);

    std::vector<D3D12_INPUT_ELEMENT_DESC> GetNativeInputElementLayout() const;
    const ComPtr<ID3DBlob>& GetNativeByteCode() const;
private:
    ComPtr<ID3DBlob> m_byte_code_cptr;
    ComPtr<ID3D12ShaderReflection> m_reflection_cptr;
};
ARISENRHI_D3D12_END_NAMESPACE
