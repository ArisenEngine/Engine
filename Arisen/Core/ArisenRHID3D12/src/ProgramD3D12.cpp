#include "ProgramD3D12.h"

#include "d3dx12.h"
#include "ExceptionHandle.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
ProgramD3D12::ProgramD3D12(const IRHIContext& context, const ProgramSettings& settings)
    :ProgramBase(context, settings)
    , m_context_dx(DYNAMIC_CAST(const IRHIContextCommonD3D12&, context))

{
    InitRootSignature();
}

D3D12_INPUT_LAYOUT_DESC ProgramD3D12::GetNativeInputLayoutDesc() const
{
    if (m_vertex_input_layout.empty()) {
        m_vertex_input_layout = GetShaderD3D12(ShaderType::Vertex).GetNativeInputElementLayout();
    }

    return { m_vertex_input_layout.data(), static_cast<UINT>(m_vertex_input_layout.size()) };
}

ShaderD3D12& ProgramD3D12::GetShaderD3D12(ShaderType shader_type) const
{
    return static_cast<ShaderD3D12&>(*GetShader(shader_type));
}


void ProgramD3D12::InitRootSignature()
{
    CD3DX12_VERSIONED_ROOT_SIGNATURE_DESC root_signature_desc;
    root_signature_desc.Init_1_1(0, nullptr, 0, nullptr, D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT);

    ComPtr<ID3D12Device>& native_device = m_context_dx.GetDeviceD3D12().GetNativeDevice();

    ComPtr<ID3DBlob> root_signature_blob;
    ComPtr<ID3DBlob> error;
    ThrowIfFailed(D3DX12SerializeVersionedRootSignature(&root_signature_desc, D3D_ROOT_SIGNATURE_VERSION_1_1, &root_signature_blob, &error));
    ThrowIfFailed(native_device->CreateRootSignature(0, root_signature_blob->GetBufferPointer(), root_signature_blob->GetBufferSize(), IID_PPV_ARGS(&m_root_signature_cptr)));
}

ARISENRHI_D3D12_END_NAMESPACE
