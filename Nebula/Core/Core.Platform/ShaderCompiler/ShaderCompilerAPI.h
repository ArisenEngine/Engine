#pragma once
#include <windows.h>
#include "dxcapi.h"
#include <atlbase.h> 
#include "../CorePlatformCommon.h"
#include "Logger/Logger.h"
#include "RHI/Enums/Pipeline/ProgramStage.h"

namespace NebulaEngine::Platforms
{
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
    
    struct ShaderCompilerOutput
    {
        void* codePointer;
        SIZE_T codeSize;
        std::string msgOut;
    };
    
    struct ShaderCompileParams
    {
        std::wstring input {L"" };
        // eg: main
        std::wstring entry { L"main" };
        // eg: 6_1
        std::wstring shaderModel { L"6_4"} ;
        // eg: -spirv
        std::wstring target { L" -spirv" };
        // eg: vulkan1.3
        std::wstring targetEnv;
        std::wstring optimizeLevel { L"0" };
        RHI::ProgramStage stage;
        
        // eg: _TEST_KEY_WORDS_
        Containers::Vector<std::wstring> defines;
        // eg: "./shader_includes/"
        Containers::Vector<std::wstring> includes;
        std::optional<std::wstring> output;
        std::optional<bool> useDXLayout; // "-fvk-use-dx-layout"
    };

    // TODO: support for multiple thread compilation
    static CComPtr<IDxcLibrary> s_DXCLibrary = nullptr;
    static CComPtr<IDxcCompiler3> s_DXCompiler = nullptr;
    static CComPtr<IDxcUtils> s_DXCUtils = nullptr;
    extern "C" PLATFORM_DLL void InitDXC();
    inline void InitDXC() 
    {
        HRESULT hres;
        
        // Initialize DXC library
        hres = DxcCreateInstance(CLSID_DxcLibrary, IID_PPV_ARGS(&s_DXCLibrary));
        if (FAILED(hres))
        {
            LOG_ERROR("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Library");
        }

        // Initialize DXC compiler
        hres = DxcCreateInstance(CLSID_DxcCompiler, IID_PPV_ARGS(&s_DXCompiler));
        if (FAILED(hres))
        {
            LOG_ERROR("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Compiler");
        }

        // Initialize DXC utility
        hres = DxcCreateInstance(CLSID_DxcUtils, IID_PPV_ARGS(&s_DXCUtils));
        if (FAILED(hres))
        {
           LOG_ERROR("[NebulaEngine::Platforms::InitDXC]: Could not init DXC Utiliy");
        }

        LOG_DEBUG("[NebulaEngine::Platforms::InitDXC]: DXC init. ");
        
    }

    extern "C" PLATFORM_DLL void ReleaseDXC();
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

    extern "C" PLATFORM_DLL bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output);
    inline bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output)
    {
        ASSERT(s_DXCompiler != nullptr && s_DXCLibrary != nullptr && s_DXCUtils != nullptr);
        HRESULT hres;
        // Load the HLSL text shader from disk
        uint32_t codePage = DXC_CP_ACP;
        CComPtr<IDxcBlobEncoding> sourceBlob;
        hres = s_DXCUtils->LoadFile(params.input.c_str(), &codePage, &sourceBlob);
        if (FAILED(hres))
        {
            output.msgOut = "Could not load shader file. ";
            LOG_ERROR("[Platforms::CompileShaderFromFile]: Could not load shader file at path:" + String::WStringToString(params.input));
            return false;
        }

        // Configure the compiler arguments for compiling the HLSL shader to SPIR-V
        auto stage = s_Stages[(UInt32)params.stage];
        stage.append(params.shaderModel);
        std::wstring env = L"-fspv-target-env=";
        env.append(params.targetEnv);
        std::wstring optimize = L"-O";
        optimize.append(params.optimizeLevel);
        Containers::Vector<LPCWSTR> arguments = {
            // (Optional) name of the shader file to be displayed e.g. in an error message
            params.input.c_str(),
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

        if (params.useDXLayout.has_value() && params.useDXLayout.value())
        {
            arguments.push_back(L"-fvk-use-dx-layout");
        }

        // Compile shader
        DxcBuffer buffer{};
        buffer.Encoding = DXC_CP_ACP;
        buffer.Ptr = sourceBlob->GetBufferPointer();
        buffer.Size = sourceBlob->GetBufferSize();


#if _DEBUG
        std::string finalArguments = "";

        for (auto arg : arguments)
        {
            finalArguments += String::WStringToString(std::wstring(arg));
            finalArguments += " ";
        }
        LOG_DEBUG("[ShaderCompilerAPI::CompileShaderFromFile] arguments : " + finalArguments);
#endif
        
        
        CComPtr<IDxcResult> result{ nullptr };
        hres = s_DXCompiler->Compile(
            &buffer,
            arguments.data(),
            static_cast<UINT32>(arguments.size()),
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
                output.msgOut = std::string((const char*)errorBlob->GetBufferPointer());
                LOG_ERROR("[Platforms::CompileShaderFromFile]: Shader compilation failed : " + output.msgOut);
            }

            return false;
        }


        // Get compilation result
        CComPtr<IDxcBlob> shaderCode;
        result->GetResult(&shaderCode);

        output.codePointer = std::malloc(shaderCode->GetBufferSize());
        std::memcpy(output.codePointer, shaderCode->GetBufferPointer(), shaderCode->GetBufferSize());
        output.codeSize = shaderCode->GetBufferSize();

        if (params.output.has_value())
        {
            // Write the bytecode to a file
            std::ofstream outFile(params.output.value(), std::ios::binary);
            if (outFile.is_open())
            {
                outFile.write(reinterpret_cast<const char*>(shaderCode->GetBufferPointer()), shaderCode->GetBufferSize());
                outFile.close();
                LOG_DEBUG("[ShaderCompilerAPI::CompileShaderFromFile]: Shader bytecode successfully written to :" + String::WStringToString(params.output.value()));
            }
            else
            {
                LOG_ERROR("[ShaderCompilerAPI::CompileShaderFromFile]: Shader bytecode falied to written to :" + String::WStringToString(params.output.value()));
                return false;
            }
        }
       
        return true;
    }

    
    
    
}
