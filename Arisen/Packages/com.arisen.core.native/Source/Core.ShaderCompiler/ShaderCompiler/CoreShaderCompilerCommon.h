#pragma once

#ifdef CORE_SHADERCOMPILER_EXPORTS
#define SHADERCOMPILER_DLL __declspec(dllexport)
#else
#define SHADERCOMPILER_DLL __declspec(dllimport)
#endif
