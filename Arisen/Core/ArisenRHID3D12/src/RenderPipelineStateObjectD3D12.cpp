#include "RenderPipelineStateObjectD3D12.h"

#include "d3dx12.h"
#include "ExceptionHandle.h"
#include "RenderCommandListD3D12.h"
#include "RenderContextD3D12.h"
#include "TypeConverterDX.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
RenderPipelineStateObjectD3D12::RenderPipelineStateObjectD3D12(const IRHIContext& context,
                                                               const RenderPipelineStateObjectSettings& settings)
        :RenderPipelineStateObjectBase(context, settings)
{
    Reset(settings);
}

void RenderPipelineStateObjectD3D12::Apply(const IRenderCommandList& command_list) const
{
    const auto& command_list_d3d12 = static_cast<const RenderCommandListD3D12&>(command_list);
    auto& native_command_list = command_list_d3d12.GetNativeCommandList();

    native_command_list.SetPipelineState(GetNativePSO().Get());

    native_command_list.SetGraphicsRootSignature(GetProgramD3D12().GetNativeRootSignature().Get());
}


CD3DX12_SHADER_BYTECODE GetShaderByteCode(const Ptr<IShader>& shader)
{
    if (!shader) {
        return CD3DX12_SHADER_BYTECODE(nullptr, 0);
    }

    const ShaderD3D12& shaderD3D12 = static_cast<const ShaderD3D12&>(*shader);
    return CD3DX12_SHADER_BYTECODE(shaderD3D12.GetNativeByteCode()->GetBufferPointer(), shaderD3D12.GetNativeByteCode()->GetBufferSize());
}

void RenderPipelineStateObjectD3D12::Reset(const RenderPipelineStateObjectSettings& settings)
{
    RenderPipelineStateObjectBase::Reset(settings);

    CD3DX12_RASTERIZER_DESC rasterizer_desc(D3D12_DEFAULT);

    CD3DX12_BLEND_DESC blend_desc(D3D12_DEFAULT);

    CD3DX12_DEPTH_STENCIL_DESC depth_stencil_desc(D3D12_DEFAULT);

    const AttachmentFormats attachment_formats =settings.render_pattern->GetAttachmentFormats();

    bool bDepthEnabled = false;

    const ProgramD3D12& program_d3d12 = GetProgramD3D12();
    m_pso_desc.InputLayout = program_d3d12.GetNativeInputLayoutDesc();
    m_pso_desc.pRootSignature = program_d3d12.GetNativeRootSignature().Get();
    m_pso_desc.VS = GetShaderByteCode(program_d3d12.GetShader(ShaderType::Vertex));
    m_pso_desc.PS = GetShaderByteCode(program_d3d12.GetShader(ShaderType::Pixel));
    m_pso_desc.RasterizerState = rasterizer_desc;
    m_pso_desc.BlendState = blend_desc;
    m_pso_desc.DepthStencilState = depth_stencil_desc;
    m_pso_desc.SampleMask = UINT_MAX;
    m_pso_desc.PrimitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_TRIANGLE;
    m_pso_desc.NumRenderTargets = attachment_formats.colors.size();
    uint32_t attachment_index = 0;
    for (TextureFormat color_format : attachment_formats.colors)
    {
        m_pso_desc.RTVFormats[attachment_index++] = TextureFormatToDXGI_Format(color_format);
    }
    m_pso_desc.SampleDesc.Count = 1;// TODO:refactor to settings.
    m_pso_desc.DSVFormat = bDepthEnabled ? TextureFormatToDXGI_Format(attachment_formats.depth) : DXGI_FORMAT_UNKNOWN;

    const_cast<RenderPipelineStateObjectD3D12*>(this)->m_pso_cptr.Reset();
}

ComPtr<ID3D12PipelineState>& RenderPipelineStateObjectD3D12::GetNativePSO() const
{
    if (!m_pso_cptr) {
        const_cast<RenderPipelineStateObjectD3D12*>(this)->InitializeNativePSO();
    }

    return const_cast<ComPtr<ID3D12PipelineState>&>(m_pso_cptr);
}

void RenderPipelineStateObjectD3D12::InitializeNativePSO() const
{
    if (m_pso_cptr) {
        return;
    }

    ComPtr<ID3D12Device>& device = GetRenderContextD3D12().GetDeviceD3D12().GetNativeDevice();
    ThrowIfFailed(device->CreateGraphicsPipelineState(&m_pso_desc, IID_PPV_ARGS(&const_cast<RenderPipelineStateObjectD3D12*>(this)->m_pso_cptr)));
    // setname.
}

ProgramD3D12& RenderPipelineStateObjectD3D12::GetProgramD3D12() const
{
    return static_cast<ProgramD3D12&>(const_cast<RenderPipelineStateObjectD3D12*>(this)->GetProgram());
}

const RenderContextD3D12& RenderPipelineStateObjectD3D12::GetRenderContextD3D12() const
{
    return static_cast<const RenderContextD3D12&>(GetContext());
}

ARISENRHI_D3D12_END_NAMESPACE
