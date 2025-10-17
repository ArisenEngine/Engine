#include "ShaderD3D12.h"

#include "ExceptionHandle.h"
#include <d3dcompiler.h>

#include "TypeConverterDX.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
ShaderD3D12::ShaderD3D12(ShaderType type, const IRHIContext& context, const ShaderSettings& settings)
    :ShaderBase(type, context, settings)
{
    // TODO: replace with dxc

#if defined(_DEBUG)
    // Enable better shader debugging with the graphics debugging tools.
    const UINT shader_compile_flags = D3DCOMPILE_DEBUG | D3DCOMPILE_SKIP_OPTIMIZATION;
#else
    const UINT shader_compile_flags = 0;
#endif

    ComPtr<ID3DBlob> error;
    ThrowIfFailed(D3DCompileFromFile(
        // FIXME: full file path.
        reinterpret_cast<LPCWSTR>(settings.entry_function.file_name.c_str()),
        nullptr,//defines
        D3D_COMPILE_STANDARD_FILE_INCLUDE,
        settings.entry_function.function_name.c_str(),
        "vs_5_0",
        shader_compile_flags,
        0,
        &m_byte_code_cptr,
        &error
        ));

    ThrowIfFailed(D3DReflect(m_byte_code_cptr->GetBufferPointer(), m_byte_code_cptr->GetBufferSize(), IID_PPV_ARGS(&m_reflection_cptr)));
}

static D3D12_INPUT_CLASSIFICATION GetInputClassificationByLayoutStepType(InputBufferLayoutStepType step_type)
{
    switch (step_type)
    {
        case InputBufferLayoutStepType::PerVertex:     return D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA;
        case InputBufferLayoutStepType::PerInstance:   return D3D12_INPUT_CLASSIFICATION_PER_INSTANCE_DATA;
        default:                      CHECK_UNEXPECTED_RETURN(step_type, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA);
    }
}

std::vector<D3D12_INPUT_ELEMENT_DESC> ShaderD3D12::GetNativeInputElementLayout() const
{
    std::vector<D3D12_INPUT_ELEMENT_DESC> layout_desc;
    D3D12_SHADER_DESC shader_desc;
    m_reflection_cptr->GetDesc(&shader_desc);

    std::vector<uint32_t> input_buffer_byte_offset;
    for (UINT i = 0; i < shader_desc.InputParameters; ++i)
    {
        D3D12_SIGNATURE_PARAMETER_DESC param_desc;
        m_reflection_cptr->GetInputParameterDesc(i, &param_desc);

        // FIXME:input slot
        uint32_t buffer_index = 0;
        InputBufferLayoutStepType step_type = InputBufferLayoutStepType::PerVertex;

        if (buffer_index >= input_buffer_byte_offset.size())
        {
            input_buffer_byte_offset.resize(buffer_index + 1, 0);
        }

        uint32_t& buffer_byte_offset = input_buffer_byte_offset[buffer_index];

        uint32_t element_byte_size = 0;
        layout_desc.push_back({
            param_desc.SemanticName,
            param_desc.SemanticIndex,
            ParameterDescToDXGIFormatAndSize(param_desc, element_byte_size),
            buffer_index,
            buffer_byte_offset,
            GetInputClassificationByLayoutStepType(step_type),
            step_type == InputBufferLayoutStepType::PerVertex ? 0 : 1U
        });
        buffer_byte_offset += element_byte_size;
    }

    return layout_desc;
}

const ComPtr<ID3DBlob>& ShaderD3D12::GetNativeByteCode() const
{
    return m_byte_code_cptr;
}

ARISENRHI_D3D12_END_NAMESPACE
