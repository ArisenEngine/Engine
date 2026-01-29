#include "ShaderCompilerAPI.h"

namespace ArisenEngine::HAL
{
    static ComPtr<IDxcLibrary> s_DXCLibrary = nullptr;
    static ComPtr<IDxcCompiler3> s_DXCompiler = nullptr;
    static ComPtr<IDxcUtils> s_DXCUtils = nullptr;

    extern "C" SHADERCOMPILER_DLL void InitDXC()
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

    extern "C" SHADERCOMPILER_DLL void ReleaseDXC()
    {
        s_DXCompiler.Reset();
        s_DXCLibrary.Reset();
        s_DXCUtils.Reset();

        LOG_DEBUG("[HAL::ReleaseDXC]: DXC released.");
    }

    extern "C" SHADERCOMPILER_DLL bool CompileShaderFromFile(ShaderCompileParams&& params, ShaderCompilerOutput& output)
    {
        ASSERT(s_DXCompiler != nullptr && s_DXCLibrary != nullptr && s_DXCUtils != nullptr);

        HRESULT hres = S_OK;

        // Load shader file with UTF8 encoding
        uint32_t codePage = DXC_CP_UTF8;
        ComPtr<IDxcBlobEncoding> sourceBlob;
        hres = s_DXCUtils->LoadFile(params.input.ToWString().c_str(), &codePage, &sourceBlob);
        if (FAILED(hres))
        {
            output.msgOut = "Could not load shader file.";
            LOG_ERROR("[HAL::CompileShaderFromFile]: Failed to load shader: " + params.input);
            return false;
        }

        // Assemble compilation arguments
        String stage = STAGE_PREFIX_ENUM(params.stage);
        stage += params.shaderModel;

        String env = L"-fspv-target-env=" + params.targetEnv;
        String optimize = L"-O" + params.optimizeLevel;

        std::vector<std::wstring> wArguments;
        wArguments.push_back(params.input.ToWString());
        wArguments.push_back(L"-E"); wArguments.push_back(params.entry.ToWString());
        wArguments.push_back(L"-T"); wArguments.push_back(stage.ToWString());
        wArguments.push_back(params.target.ToWString());
        wArguments.push_back(env.ToWString());
        wArguments.push_back(optimize.ToWString());

        for (const auto& inc : params.includes)
        {
            wArguments.push_back(L"-I");
            wArguments.push_back(inc.ToWString());
        }

        for (const auto& def : params.defines)
        {
            wArguments.push_back(L"-D");
            wArguments.push_back(def.ToWString());
        }

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
                output.msgOut = String(reinterpret_cast<const char*>(errorBlob->GetBufferPointer()));
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
}


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