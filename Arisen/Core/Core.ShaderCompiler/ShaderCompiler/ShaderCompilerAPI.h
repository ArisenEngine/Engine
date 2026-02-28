#pragma once
#include "Base/BindingMacros.h"
#include <windows.h>
#include "DxcCompat.h"
#include <initguid.h>
#include "dxcapi.h"
#include <wrl/client.h>  // Replace CComPtr with Microsoft WRL smart pointer
#include "CoreShaderCompilerCommon.h"
#include "../Core.HAL/CoreHALCommon.h"

ARISEN_BIND_MODULE("Core.ShaderCompiler.dll")
ARISEN_BIND_NAMESPACE("Arisen.Native.ShaderCompiler")

#include "Logger/Logger.h"
#include "RHI/Enums/Pipeline/EProgramStage.h"
#include <fstream>
#if __has_include(<optional>)
#include <optional>
#elif __has_include(<experimental/optional>)
#include <experimental/optional>
namespace std { using experimental::optional; }
#else
// Fallback to satisfy parser when neither optional header is present
namespace std { template <class T> class optional; }
#endif
#include <cstring>
#include <string>
#include <vector>
#if __has_include(<filesystem>)
#include <filesystem>
namespace fs = std::filesystem;
#elif __has_include(<experimental/filesystem>)
#include <experimental/filesystem>
namespace fs = std::experimental::filesystem;
#else
// Fallback type for parser-only environments
namespace fs { class path {}; }
#endif

using Microsoft::WRL::ComPtr;

namespace ArisenEngine::HAL
{
#if defined(ARISEN_AUTOBINDING)
    inline String GetStagePrefix(RHI::EProgramStage stage)
    {
        switch (static_cast<uint32_t>(stage))
        {
        case 0: return L"vs_"; // Vertex
        case 1: return L"hs_"; // Hull
        case 2: return L"ds_"; // Domain
        case 3: return L"gs_"; // Geometry
        case 4: return L"ps_"; // Pixel
        case 5: return L"cs_"; // Compute
        case 6: return L"lib_"; // Library
        case 7: return L"as_"; // Amplification
        case 8: return L"ms_"; // Mesh
        default: return L"";
        }
    }
#define STAGE_PREFIX_ENUM(e) GetStagePrefix(e)
#else
#define STAGE_PREFIX_ENUM(e) s_Stages[static_cast<uint32_t>(e)]
#endif
#if !defined(ARISEN_AUTOBINDING)
    static String s_Stages[RHI::STAGE_MAX] =
    {
        L"vs_",
        L"hs_",
        L"ds_",
        L"gs_",
        L"ps_",
        L"cs_",
        L"lib_",
        L"as_",
        L"ms_"
    };
#endif

    struct ShaderCompilerOutput
    {
        // Use smart pointer to avoid leaks, currently there are leaks
        void* codePointer = nullptr;
        SIZE_T codeSize = 0;
        String msgOut;
    };

    struct ShaderCompileParams
    {
        String input{L""};
        String entry{L"main"};
        String shaderModel{L"6_4"};
        String target{L"-spirv"};
        String targetEnv;
        String optimizeLevel{L"0"};
        RHI::EProgramStage stage;

        std::vector<String> defines;
        std::vector<String> includes;
        std::optional<String> output;
        std::optional<bool> useDXLayout;
    };


    extern "C" SHADERCOMPILER_DLL void InitDXC();

    extern "C" SHADERCOMPILER_DLL void ReleaseDXC();

    extern "C" SHADERCOMPILER_DLL bool
    CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output);

    // A simpler C ABI for managed callers: avoids constructing STL containers from C# side
    extern "C" SHADERCOMPILER_DLL bool CompileShaderFromFileSimple(
        const wchar_t* input,
        RHI::EProgramStage stage,
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
        bool useDXLayout);
}
