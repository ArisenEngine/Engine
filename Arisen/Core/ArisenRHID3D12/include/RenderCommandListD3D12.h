#pragma once
#include "CommandListD3D12.h"
#include "IRenderCommandListD3D12.h"
#include "RenderCommandListBase.h"
#include "RHIMacrosD3D12.h"
ARISENRHI_D3D12_BEGIN_NAMEPSACE
class RenderCommandListD3D12 : public RenderCommandListBase<CommandListD3D12<IRenderCommandListD3D12>>
{
public:

};
ARISENRHI_D3D12_END_NAMESPACE
