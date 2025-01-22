#pragma once

#include "./Common/CommandHeaders.h"

#ifdef RHIDX12_EXPORTS

#define RHI_DX12_DLL   __declspec( dllexport )

#else

#define RHI_DX12_DLL   __declspec( dllimport )

#endif 