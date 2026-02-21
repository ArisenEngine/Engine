using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace BindingGenerator;

public abstract class ArisenLibrary : ILibrary
{
    public virtual void Setup(Driver driver)
    {
        var options = driver.Options;
        options.GeneratorKind = GeneratorKind.CSharp;
        options.GenerationOutputMode = GenerationOutputMode.FilePerUnit;
        options.Verbose = true;
        options.CheckSymbols = false;
        options.Compilation.DebugMode = true;
        
        // All cleanup will be handled manually in Preprocess due to CppSharp version differences.

        var parserOptions = driver.ParserOptions;
        parserOptions.Setup(TargetPlatform.Windows);
        parserOptions.LanguageVersion = CppSharp.Parser.LanguageVersion.CPP20;
        parserOptions.MicrosoftMode = true;

        // C++ Standard Consistency
        parserOptions.AddDefines("_HAS_CXX17=1");
        parserOptions.AddDefines("_HAS_CXX20=1");
        parserOptions.AddDefines("_HAS_CXX23=1");
        parserOptions.AddDefines("_MSVC_LANG=202302L");
        parserOptions.AddDefines("_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH");
        parserOptions.AddDefines("ARISEN_AUTOBINDING=1");
        parserOptions.AddDefines("_XM_NO_INTRINSICS_");
        
        // Define exports to make symbols bindable
        parserOptions.AddDefines("COREFOUNDATION_EXPORTS");
        parserOptions.AddDefines("COREDIAGNOSTIC_EXPORTS");
        parserOptions.AddDefines("COREHAL_EXPORTS");
        parserOptions.AddDefines("CORE_RHI_EXPORTS");
        parserOptions.AddDefines("CORE_SHADERCOMPILER_EXPORTS");

        parserOptions.AddDefines("FOUNDATION_DLL=__declspec(dllexport)");
        parserOptions.AddDefines("DIAGNOSTIC_DLL=__declspec(dllexport)");
        parserOptions.AddDefines("HAL_DLL=__declspec(dllexport)");
        parserOptions.AddDefines("RHI_DLL=__declspec(dllexport)");
        parserOptions.AddDefines("SHADER_COMPILER_DLL=__declspec(dllexport)");

        try { parserOptions.AddArguments("-std=c++23"); } catch { }

        // Core Include Directories
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Foundation"));
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.HAL"));
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.HAL", "Windowing"));
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Diagnostic"));
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.RHI"));
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.RHI", "RHI"));
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.ShaderCompiler"));
        
        // 3rdparty
        parserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "3rdparty", "dxc", "inc"));

        // MSVC / SDK Search
        var vcTools = System.Environment.GetEnvironmentVariable("VCToolsInstallDir");
        if (!string.IsNullOrEmpty(vcTools))
        {
            parserOptions.AddIncludeDirs(Path.Combine(vcTools, "include"));
        }

        var sdkPath = System.Environment.GetEnvironmentVariable("WindowsSdkDir");
        var sdkVer = System.Environment.GetEnvironmentVariable("WindowsSDKVersion");
        if (!string.IsNullOrEmpty(sdkPath) && !string.IsNullOrEmpty(sdkVer))
        {
            parserOptions.AddIncludeDirs(Path.Combine(sdkPath, "Include", sdkVer, "um"));
            parserOptions.AddIncludeDirs(Path.Combine(sdkPath, "Include", sdkVer, "shared"));
            parserOptions.AddIncludeDirs(Path.Combine(sdkPath, "Include", sdkVer, "ucrt"));
        }
        
        SetupModule(driver);
    }

    public virtual void Preprocess(Driver driver, ASTContext ctx) { }

    public virtual void Postprocess(Driver driver, ASTContext ctx) { }

    public virtual void SetupPasses(Driver driver) { }

    public abstract string GetLibraryName();
    public abstract void SetupModule(Driver driver);
}
