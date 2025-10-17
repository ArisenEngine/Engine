#pragma once
#include <dxgiformat.h>

#include "ContextCommonD3D12.h"
#include "CoreMinimalD3D12.h"
#include "RHITypes.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
DXGI_FORMAT TextureFormatToDXGI_Format(TextureFormat textureFormat);

DXGI_FORMAT ParameterDescToDXGIFormatAndSize(const D3D12_SIGNATURE_PARAMETER_DESC& param_desc, uint32_t& out_element_byte_size);



ARISENRHI_D3D12_END_NAMESPACE
