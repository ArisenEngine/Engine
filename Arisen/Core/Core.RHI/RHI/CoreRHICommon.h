#pragma once

#ifdef CORE_RHI_EXPORTS
#define RHI_DLL __declspec(dllexport)
#else
#define RHI_DLL __declspec(dllimport)
#endif
