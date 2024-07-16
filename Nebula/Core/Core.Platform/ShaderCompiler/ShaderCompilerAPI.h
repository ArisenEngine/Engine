#pragma once
#include <windows.h>
#include "dxcapi.h"
#include <atlbase.h>

#include "../Common.h"
#include "Logger/Logger.h"

namespace NebulaEngine::Platforms
{
    typedef enum ShaderStage
    {
        Vertex = 0,
        Hull,
        Domain,
        Fragment,
        Geometry,
        Compute,
        // Shader Model 6.3
        RayTracing,
        // Shader Model 6.5
        Amplification,
        // Shader Model 6.5
        Mesh,
        STAGE_MAX
    } ShaderStage;

    static std::wstring s_Stages[STAGE_MAX] =
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
    
    struct ShaderCompileParams
    {
        std::wstring name {L"Unknow" };
        // eg: main
        std::wstring entry { L"main" };
        // eg: 6_1
        std::wstring shaderModel { L"6_4"} ;
        // eg: -spirv
        std::wstring target { L" -spirv" };
        // eg: vulkan1.3
        std::wstring targetEnv;
        std::wstring optimizeLevel { L"0" };
        ShaderStage stage;
        
        // eg: _TEST_KEY_WORDS_
        Containers::Vector<std::wstring> defines;
        // eg: "./shader_includes/"
        Containers::Vector<std::wstring> includes;
        std::optional<std::wstring> output;
    };
    
    static CComPtr<IDxcLibrary> s_DXCLibrary = nullptr;
    static CComPtr<IDxcCompiler3> s_DXCompiler = nullptr;
    static CComPtr<IDxcUtils> s_DXCUtils = nullptr;
    extern "C" DLL void InitDXC();
    inline void InitDXC()
    {
        HRESULT hres;
        
        // Initialize DXC library
        hres = DxcCreateInstance(CLSID_DxcLibrary, IID_PPV_ARGS(&s_DXCLibrary));
        if (FAILED(hres))
        {
            LOG_FATAL_AND_THROW("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Library");
        }

        // Initialize DXC compiler
        hres = DxcCreateInstance(CLSID_DxcCompiler, IID_PPV_ARGS(&s_DXCompiler));
        if (FAILED(hres))
        {
            LOG_FATAL_AND_THROW("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Compiler");
        }

        // Initialize DXC utility
        hres = DxcCreateInstance(CLSID_DxcUtils, IID_PPV_ARGS(&s_DXCUtils));
        if (FAILED(hres))
        {
           LOG_FATAL_AND_THROW("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Utiliy");
        }

        LOG_DEBUG("[NebulaEngine::Platforms::InitDXC]: DXC init. ");
        
    }

    extern "C" DLL void ReleaseDXC();
    inline void ReleaseDXC()
    {
        s_DXCompiler.Release();
        s_DXCLibrary.Release();
        s_DXCUtils.Release();
        
        s_DXCompiler = nullptr;
        s_DXCUtils = nullptr;
        s_DXCUtils = nullptr;
        LOG_DEBUG("[Platforms::ReleaseDXC]: DXC Release. ");
    }

    extern "C" DLL bool CompileShaderFromFile(std::wstring&& filePath, ShaderCompileParams&& params);
    inline bool CompileShaderFromFile(std::wstring&& filePath, ShaderCompileParams&& params)
    {
        ASSERT(s_DXCompiler != nullptr && s_DXCLibrary != nullptr && s_DXCUtils != nullptr);
        HRESULT hres;
        // Load the HLSL text shader from disk
        uint32_t codePage = DXC_CP_ACP;
        CComPtr<IDxcBlobEncoding> sourceBlob;
        hres = s_DXCUtils->LoadFile(filePath.c_str(), &codePage, &sourceBlob);
        if (FAILED(hres))
        {
            LOG_FATAL_AND_THROW("[Platforms::CompileShaderFromFile]: Could not load shader file");
        }

        // Configure the compiler arguments for compiling the HLSL shader to SPIR-V
        auto stage = s_Stages[(u32)params.stage];
        stage.append(params.shaderModel);
        std::wstring env = L"-fspv-target-env=";
        env.append(params.targetEnv);
        std::wstring optimize = L"-O";
        optimize.append(params.optimizeLevel);
        Containers::Vector<LPCWSTR> arguments = {
            // (Optional) name of the shader file to be displayed e.g. in an error message
            params.name.c_str(),
            // Shader main entry point
            L"-E", params.entry.c_str(),
            // Shader target profile
            L"-T", stage.c_str(),
            //eg: Compile to SPIRV
            params.target.c_str(),
            // env
            env.c_str(),
            // optimize level
           optimize.c_str()
        };

        for (const auto& dir : params.includes)
        {
            arguments.push_back(L"-I");
            arguments.push_back(dir.c_str());
        }

        // define
        for (const auto& define : params.defines)
        {
            arguments.push_back(L"-D");
            arguments.push_back(define.c_str());
        }

        if (params.output.has_value())
        {
            arguments.push_back(L"-Fo");
            arguments.push_back(params.output->c_str());
        }

        // Compile shader
        DxcBuffer buffer{};
        buffer.Encoding = DXC_CP_ACP;
        buffer.Ptr = sourceBlob->GetBufferPointer();
        buffer.Size = sourceBlob->GetBufferSize();

        CComPtr<IDxcResult> result{ nullptr };
        hres = s_DXCompiler->Compile(
            &buffer,
            arguments.data(),
            (uint32_t)arguments.size(),
            nullptr,
            IID_PPV_ARGS(&result));

        if (SUCCEEDED(hres))
        {
            result->GetStatus(&hres);
        }

        // Output error if compilation failed
        if (FAILED(hres) && (result)) {
            CComPtr<IDxcBlobEncoding> errorBlob;
            hres = result->GetErrorBuffer(&errorBlob);
            if (SUCCEEDED(hres) && errorBlob)
            {
                std::string errorMsg= std::string((const char*)errorBlob->GetBufferPointer());
                LOG_ERROR("[Platforms::CompileShaderFromFile]: Shader compilation failed : " + errorMsg);
            }

            return false;
        }

        return true;
    }

    
    
    
}
