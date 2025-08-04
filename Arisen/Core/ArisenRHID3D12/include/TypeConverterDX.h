#pragma once
#include <dxgiformat.h>

#include "CoreMinimalD3D12.h"
#include "RHITypes.h"

ARISENRHI_D3D12_BEGIN_NAMEPSACE
    DXGI_FORMAT TextureFormatToDXGI_Format(TextureFormat textureFormat);

    

ARISENRHI_D3D12_END_NAMESPACE
