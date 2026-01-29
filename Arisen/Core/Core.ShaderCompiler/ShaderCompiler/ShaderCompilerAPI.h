#pragma once
#include <windows.h>
#include "DxcCompat.h"
#include <initguid.h>
#include "dxcapi.h"
#include <wrl/client.h>  // 用微软WRL智能指针替代CComPtr
#include "CoreShaderCompilerCommon.h"
#include "../Core.HAL/CoreHALCommon.h"
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
        // 使用智能指针避免泄露，目前存在泄露
        void* codePointer = nullptr;
        SIZE_T codeSize = 0;
        String msgOut;
    };

    struct ShaderCompileParams
    {
        String input{ L"" };
        String entry{ L"main" };
        String shaderModel{ L"6_4" };
        String target{ L"-spirv" };
        String targetEnv;
        String optimizeLevel{ L"0" };
        RHI::EProgramStage stage;

        std::vector<String> defines;
        std::vector<String> includes;
        std::optional<String> output;
        std::optional<bool> useDXLayout;
    };

    static ComPtr<IDxcLibrary> s_DXCLibrary = nullptr;
    static ComPtr<IDxcCompiler3> s_DXCompiler = nullptr;
    static ComPtr<IDxcUtils> s_DXCUtils = nullptr;

    extern "C" SHADERCOMPILER_DLL void InitDXC();
    void InitDXC()
    {
        HRESULT hres = DxcCreateInstance(CLSID_DxcLibrary, IID_PPV_ARGS(&s_DXCLibrary));
        if (FAILED(hres))
        {
            LOG_ERROR("[ArisenEngine::HAL::InitDXC]: Could not init DXC Library");
            return;
        }

        hres = DxcCreateInstance(CLSID_DxcCompiler, IID_PPV_ARGS(&s_DXCompiler));
        if (FAILED(hres))
        {
            LOG_ERROR("[ArisenEngine::HAL::InitDXC]: Could not init DXC Compiler");
            return;
        }

        hres = DxcCreateInstance(CLSID_DxcUtils, IID_PPV_ARGS(&s_DXCUtils));
        if (FAILED(hres))
        {
            LOG_ERROR("[ArisenEngine::HAL::InitDXC]: Could not init DXC Utility");
            return;
        }

        LOG_DEBUG("[ArisenEngine::HAL::InitDXC]: DXC initialized.");
    }

    extern "C" SHADERCOMPILER_DLL void ReleaseDXC();
    void ReleaseDXC()
    {
        s_DXCompiler.Reset();
        s_DXCLibrary.Reset();
        s_DXCUtils.Reset();

        LOG_DEBUG("[HAL::ReleaseDXC]: DXC released.");
    }

    extern "C" SHADERCOMPILER_DLL bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output);
    bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output)
    {
        ASSERT(s_DXCompiler != nullptr && s_DXCLibrary != nullptr && s_DXCUtils != nullptr);

        HRESULT hres = S_OK;

        // Load shader file with UTF8 encoding (跨平台友好)
        uint32_t codePage = DXC_CP_UTF8;
        ComPtr<IDxcBlobEncoding> sourceBlob;
        hres = s_DXCUtils->LoadFile(params.input.ToWString().c_str(), &codePage, &sourceBlob);
        if (FAILED(hres))
        {
            output.msgOut = "Could not load shader file.";
            LOG_ERROR("[HAL::CompileShaderFromFile]: Failed to load shader: " + params.input);
            return false;
        }

        // 组装编译参数
        String stage = STAGE_PREFIX_ENUM(params.stage);
        stage += params.shaderModel;

        String env = L"-fspv-target-env=" + params.targetEnv;
        String optimize = L"-O" + params.optimizeLevel;

        // We need to keep the wstrings alive while the arguments vector holds pointers to their c_str()
        std::vector<std::wstring> wArguments;
        wArguments.push_back(params.input.ToWString());
        wArguments.push_back(L"-E"); wArguments.push_back(params.entry.ToWString());
        wArguments.push_back(L"-T"); wArguments.push_back(stage.ToWString());
        wArguments.push_back(params.target.ToWString());
        wArguments.push_back(env.ToWString());
        wArguments.push_back(optimize.ToWString());

        // includes
        for (const auto& inc : params.includes)
        {
            wArguments.push_back(L"-I");
            wArguments.push_back(inc.ToWString());
        }

        // defines
        for (const auto& def : params.defines)
        {
            wArguments.push_back(L"-D");
            wArguments.push_back(def.ToWString());
        }

        // 输出路径
        if (params.output.has_value())
        {
            wArguments.push_back(L"-Fo");
            wArguments.push_back(params.output->ToWString());
        }

        std::vector<LPCWSTR> arguments;
        for (const auto& warg : wArguments)
        {
            arguments.push_back(warg.c_str());
        }

        if (params.useDXLayout.has_value() && params.useDXLayout.value())
        {
            arguments.push_back(L"-fvk-use-dx-layout");
        }

#if _DEBUG
        String argLog;
        for (auto arg : arguments)
            argLog += String(arg) + " ";
        LOG_DEBUG("[CompileShaderFromFile] Arguments: " + argLog);
#endif

        DxcBuffer buffer{};
        buffer.Encoding = DXC_CP_UTF8;
        buffer.Ptr = sourceBlob->GetBufferPointer();
        buffer.Size = sourceBlob->GetBufferSize();

        ComPtr<IDxcResult> result;
        ComPtr<IDxcIncludeHandler> includeHandler;
        if (s_DXCUtils)
        {
            s_DXCUtils->CreateDefaultIncludeHandler(&includeHandler);
        }
        hres = s_DXCompiler->Compile(
            &buffer,
            arguments.data(),
            static_cast<UINT32>(arguments.size()),
            includeHandler.Get(),
            IID_PPV_ARGS(&result));

        if (SUCCEEDED(hres))
        {
            hres = S_OK;
            result->GetStatus(&hres);
        }

        if (FAILED(hres))
        {
            ComPtr<IDxcBlobEncoding> errorBlob;
            if (result && SUCCEEDED(result->GetErrorBuffer(&errorBlob)) && errorBlob)
            {
                output.msgOut = String(reinterpret_cast<const char*>(errorBlob->GetBufferPointer())); // Note: Size might be needed if not null-terminated
                LOG_ERROR("[CompileShaderFromFile] Shader compilation failed: " + output.msgOut);
            }
            else
            {
                LOG_ERROR("[CompileShaderFromFile] Shader compilation failed with unknown error.");
            }
            return false;
        }

        ComPtr<IDxcBlob> shaderCode;
        if (FAILED(result->GetResult(&shaderCode)) || !shaderCode)
        {
            LOG_ERROR("[CompileShaderFromFile] Failed to get compiled shader bytecode.");
            return false;
        }

        output.codeSize = shaderCode->GetBufferSize();
        output.codePointer = std::malloc(output.codeSize);
        if (!output.codePointer)
        {
            LOG_ERROR("[CompileShaderFromFile] Memory allocation failed.");
            return false;
        }
        memcpy(output.codePointer, shaderCode->GetBufferPointer(), output.codeSize);

        // 写输出文件（如果指定了）
        if (params.output.has_value())
        {
            fs::path outputPath(params.output.value().ToWString());
            std::ofstream outFile(outputPath, std::ios::binary);

             if (!outFile) 
             {
                LOG_ERROR("Failed to open: " + params.output.value());
                return false;
             }

            if (outFile.is_open())
            {
                outFile.write(reinterpret_cast<const char*>(shaderCode->GetBufferPointer()), 
                static_cast<std::streamsize>(shaderCode->GetBufferSize()));
                outFile.close();
                LOG_DEBUG("[CompileShaderFromFile] Shader bytecode written to: " + params.output.value());
            }
            else
            {
                LOG_ERROR("[CompileShaderFromFile] Failed to write shader bytecode to file: " + params.output.value());
                return false;
            }
        }

        return true;
    }

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
