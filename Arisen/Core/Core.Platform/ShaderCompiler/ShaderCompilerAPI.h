#pragma once
#include <windows.h>
#include "DxcCompat.h"
#include <initguid.h>
#include "dxcapi.h"
#include <wrl/client.h>  // 用微软WRL智能指针替代CComPtr
#include "../CorePlatformCommon.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Pipeline/ProgramStage.h"
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

namespace ArisenEngine::Platforms
{
#if defined(ARISEN_AUTOBINDING)
    inline std::wstring GetStagePrefix(RHI::ProgramStage stage)
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
    static std::wstring s_Stages[RHI::STAGE_MAX] =
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
        std::string msgOut;
    };

    struct ShaderCompileParams
    {
        std::wstring input{ L"" };
        std::wstring entry{ L"main" };
        std::wstring shaderModel{ L"6_4" };
        std::wstring target{ L"-spirv" };
        std::wstring targetEnv;
        std::wstring optimizeLevel{ L"0" };
        RHI::ProgramStage stage;

        std::vector<std::wstring> defines;
        std::vector<std::wstring> includes;
        std::optional<std::wstring> output;
        std::optional<bool> useDXLayout;
    };

    static ComPtr<IDxcLibrary> s_DXCLibrary = nullptr;
    static ComPtr<IDxcCompiler3> s_DXCompiler = nullptr;
    static ComPtr<IDxcUtils> s_DXCUtils = nullptr;

    extern "C" PLATFORM_DLL void InitDXC();
    void InitDXC()
    {
        HRESULT hres = DxcCreateInstance(CLSID_DxcLibrary, IID_PPV_ARGS(&s_DXCLibrary));
        if (FAILED(hres))
        {
            LOG_ERROR("[ArisenEngine::Platforms::InitDXC]: Could not init DXC Library");
            return;
        }

        hres = DxcCreateInstance(CLSID_DxcCompiler, IID_PPV_ARGS(&s_DXCompiler));
        if (FAILED(hres))
        {
            LOG_ERROR("[ArisenEngine::Platforms::InitDXC]: Could not init DXC Compiler");
            return;
        }

        hres = DxcCreateInstance(CLSID_DxcUtils, IID_PPV_ARGS(&s_DXCUtils));
        if (FAILED(hres))
        {
            LOG_ERROR("[ArisenEngine::Platforms::InitDXC]: Could not init DXC Utility");
            return;
        }

        LOG_DEBUG("[ArisenEngine::Platforms::InitDXC]: DXC initialized.");
    }

    extern "C" PLATFORM_DLL void ReleaseDXC();
    void ReleaseDXC()
    {
        s_DXCompiler.Reset();
        s_DXCLibrary.Reset();
        s_DXCUtils.Reset();

        LOG_DEBUG("[Platforms::ReleaseDXC]: DXC released.");
    }

    extern "C" PLATFORM_DLL bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output);
    bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output)
    {
        ASSERT(s_DXCompiler != nullptr && s_DXCLibrary != nullptr && s_DXCUtils != nullptr);

        HRESULT hres = S_OK;

        // Load shader file with UTF8 encoding (跨平台友好)
        uint32_t codePage = DXC_CP_UTF8;
        ComPtr<IDxcBlobEncoding> sourceBlob;
        hres = s_DXCUtils->LoadFile(params.input.c_str(), &codePage, &sourceBlob);
        if (FAILED(hres))
        {
            output.msgOut = "Could not load shader file.";
            LOG_ERROR("[Platforms::CompileShaderFromFile]: Failed to load shader: " + String::WStringToString(params.input));
            return false;
        }

        // 组装编译参数
        std::wstring stage = STAGE_PREFIX_ENUM(params.stage);
        stage.append(params.shaderModel);

        std::wstring env = L"-fspv-target-env=" + params.targetEnv;
        std::wstring optimize = L"-O" + params.optimizeLevel;

        // 参数集合
        std::vector<LPCWSTR> arguments = {
            params.input.c_str(),
            L"-E", params.entry.c_str(),
            L"-T", stage.c_str(),
            params.target.c_str(),
            env.c_str(),
            optimize.c_str()
        };

        // includes
        for (const auto& inc : params.includes)
        {
            arguments.push_back(L"-I");
            arguments.push_back(inc.c_str());
        }

        // defines
        for (const auto& def : params.defines)
        {
            arguments.push_back(L"-D");
            arguments.push_back(def.c_str());
        }

        // 输出路径
        if (params.output.has_value())
        {
            arguments.push_back(L"-Fo");
            arguments.push_back(params.output->c_str());
        }

        if (params.useDXLayout.has_value() && params.useDXLayout.value())
        {
            arguments.push_back(L"-fvk-use-dx-layout");
        }

#if _DEBUG
        std::string argLog;
        for (auto arg : arguments)
            argLog += String::WStringToString(std::wstring(arg)) + " ";
        LOG_DEBUG("[CompileShaderFromFile] Arguments: " + argLog);
#endif

        DxcBuffer buffer{};
        buffer.Encoding = DXC_CP_UTF8;
        buffer.Ptr = sourceBlob->GetBufferPointer();
        buffer.Size = sourceBlob->GetBufferSize();

        ComPtr<IDxcResult> result;
        hres = s_DXCompiler->Compile(
            &buffer,
            arguments.data(),
            static_cast<UINT32>(arguments.size()),
            nullptr,
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
                output.msgOut = std::string(reinterpret_cast<const char*>(errorBlob->GetBufferPointer()), errorBlob->GetBufferSize());
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
            fs::path outputPath(params.output.value());
            std::ofstream outFile(outputPath, std::ios::binary);

             if (!outFile) 
             {
                LOG_ERROR("Failed to open: " + String::WStringToString(outputPath.wstring()));
                return false;
             }

            if (outFile.is_open())
            {
                outFile.write(reinterpret_cast<const char*>(shaderCode->GetBufferPointer()), 
                static_cast<std::streamsize>(shaderCode->GetBufferSize()));
                outFile.close();
                LOG_DEBUG("[CompileShaderFromFile] Shader bytecode written to: " + String::WStringToString(params.output.value()));
            }
            else
            {
                LOG_ERROR("[CompileShaderFromFile] Failed to write shader bytecode to file: " + String::WStringToString(params.output.value()));
                return false;
            }
        }

        return true;
    }
}
