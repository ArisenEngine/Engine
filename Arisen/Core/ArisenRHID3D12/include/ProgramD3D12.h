#pragma once
#include "IContextCommonD3D12.h"
#include "ProgramBase.h"
#include "RHID3D12ImplTraits.h"
#include "RHIMacrosD3D12.h"
#include "ShaderD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class ProgramD3D12 final : public ProgramBase<RHID3D12ImplTraits>
{
public:
    ProgramD3D12(const IRHIContext& context, const ProgramSettings& settings);
    const ComPtr<ID3D12RootSignature>& GetNativeRootSignature() const { return m_root_signature_cptr; }
    D3D12_INPUT_LAYOUT_DESC GetNativeInputLayoutDesc() const;

    ShaderD3D12& GetShaderD3D12(ShaderType shader_type) const;
private:
    void InitRootSignature();

private:
    const IRHIContextCommonD3D12& m_context_dx;
    ComPtr<ID3D12RootSignature> m_root_signature_cptr;
    mutable std::vector<D3D12_INPUT_ELEMENT_DESC> m_vertex_input_layout;
};
ARISENRHI_D3D12_END_NAMESPACE
