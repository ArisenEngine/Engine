#include <Windows/RenderWindowAPI.h>
#include <ShaderCompiler/ShaderCompilerAPI.h>
#include <new>

extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_WindowInitInfo_WindowInitInfo(void* __instance) { ::new (__instance) ArisenEngine::Platforms::WindowInitInfo(); }
extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_ShaderCompilerOutput_ShaderCompilerOutput___1__N_ArisenEngine_N_Platforms_S_ShaderCompilerOutput(void* __instance, const ArisenEngine::Platforms::ShaderCompilerOutput& _0) { ::new (__instance) ArisenEngine::Platforms::ShaderCompilerOutput(_0); }
struct ArisenEngine::Platforms::ShaderCompilerOutput& (ArisenEngine::Platforms::ShaderCompilerOutput::*_0)(struct ArisenEngine::Platforms::ShaderCompilerOutput&&) = &ArisenEngine::Platforms::ShaderCompilerOutput::operator=;
extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_ShaderCompilerOutput__ShaderCompilerOutput(ArisenEngine::Platforms::ShaderCompilerOutput*__instance) { __instance->~ShaderCompilerOutput(); }
extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_ShaderCompilerOutput_ShaderCompilerOutput(void* __instance) { ::new (__instance) ArisenEngine::Platforms::ShaderCompilerOutput(); }
extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_ShaderCompileParams_ShaderCompileParams___1__N_ArisenEngine_N_Platforms_S_ShaderCompileParams(void* __instance, const ArisenEngine::Platforms::ShaderCompileParams& _0) { ::new (__instance) ArisenEngine::Platforms::ShaderCompileParams(_0); }
struct ArisenEngine::Platforms::ShaderCompileParams& (ArisenEngine::Platforms::ShaderCompileParams::*_1)(struct ArisenEngine::Platforms::ShaderCompileParams&&) = &ArisenEngine::Platforms::ShaderCompileParams::operator=;
extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_ShaderCompileParams__ShaderCompileParams(ArisenEngine::Platforms::ShaderCompileParams*__instance) { __instance->~ShaderCompileParams(); }
extern "C" __declspec(dllexport) void c__N_ArisenEngine_N_Platforms_S_ShaderCompileParams_ShaderCompileParams(void* __instance) { ::new (__instance) ArisenEngine::Platforms::ShaderCompileParams(); }
