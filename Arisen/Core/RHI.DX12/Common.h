#pragma once

#include "./Common/CommandHeaders.h"

#ifdef RHIDX12_EXPORTS

#define RHI_DX12_DLL   __declspec( dllexport )

#else

#define RHI_DX12_DLL   __declspec( dllimport )

#endif

extern "C" RHI_DX12_DLL void dummy_dx12_function();
inline void dummy_dx12_function()
{
    
}