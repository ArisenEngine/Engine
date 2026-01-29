#include "ShaderCompilerAPI.h"

using namespace ArisenEngine;
using namespace ArisenEngine::HAL;

extern "C" SHADERCOMPILER_DLL bool CompileShaderFromFileSimple(
    const wchar_t* input,
    ArisenEngine::RHI::EProgramStage stage,
    const wchar_t* entry,
    const wchar_t* shaderModel,
    const wchar_t* target,
    const wchar_t* targetEnv,
    const wchar_t* optimizeLevel,
    const wchar_t** defines,
    int numDefines,
    const wchar_t** includes,
    int numIncludes,
    const wchar_t* output,
    bool useDXLayout)
{
    ShaderCompileParams params{};
    params.input = input;
    params.entry = entry ? String(entry) : String(L"main");
    params.shaderModel = shaderModel ? String(shaderModel) : String(L"6_4");
    params.target = target ? String(target) : String(L"-spirv");
    if (targetEnv && *targetEnv) params.targetEnv = targetEnv;
    params.optimizeLevel = optimizeLevel ? String(optimizeLevel) : String(L"0");
    params.stage = stage;

    if (includes && numIncludes > 0)
    {
        params.includes.reserve(static_cast<size_t>(numIncludes));
        for (int i = 0; i < numIncludes; ++i)
        {
            const wchar_t* inc = includes[i];
            if (inc && *inc) params.includes.emplace_back(inc);
        }
    }
    if (defines && numDefines > 0)
    {
        params.defines.reserve(static_cast<size_t>(numDefines));
        for (int i = 0; i < numDefines; ++i)
        {
            const wchar_t* def = defines[i];
            if (def && *def) params.defines.emplace_back(def);
        }
    }
    if (output && *output)
    {
        params.output = String(output);
    }
    if (useDXLayout)
    {
        params.useDXLayout = true;
    }

    ShaderCompilerOutput out{};
    return CompileShaderFromFile(std::move(params), out);
}