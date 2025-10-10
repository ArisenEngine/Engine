using BindingGenerator.Platform;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using Char = CppSharp.Types.Std.Char;

namespace BindingGenerator;

public class PlatformLibrary : ILibrary
{
    public void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            var fileName = unit.FileName ?? string.Empty;
            var fileNameLower = fileName.ToLowerInvariant();
            if (fileNameLower.Contains("dxcapi.h"))
            {
                unit.Ignore = true;
                continue;
            }
            if (unit.IsSystemHeader)
                continue;
            unit.Ignore = false; // 不忽略任何翻译单元
        }
        
        // 查找 WindowInitInfo 类
        var windowInitInfoClass = ctx.FindCompleteClass("WindowInitInfo");

        if (windowInitInfoClass != null)
        {
            // 查找并忽略 operator= 重载
            foreach (var op in windowInitInfoClass.Operators)
            {
                if (op.Name == "operator=")
                {
                    op.Ignore = true;
                }
            }
        }
        
        var windowClass = ctx.FindCompleteClass("Window");

        if (windowClass != null)
        {
            // 查找并忽略 operator= 重载
            foreach (var op in windowClass.Operators)
            {
                if (op.Name == "operator=")
                {
                    op.Ignore = true;
                }
            }
        }
        
        // 遍历所有命名空间并忽略 std / Debugger
        foreach (var unit in ctx.TranslationUnits)
        {
            // 直接检查翻译单元级别的全局变量
            foreach (var variable in unit.Variables)
            {
                if (variable.Name == "s_Stages")
                {
                    variable.Ignore = true;
                    variable.GenerationKind = GenerationKind.None;
                }
            }

            // 忽略不需要导出的全局变量，避免生成非法 C# 代码（如 L".." 宽字符串字面量）
            void IgnoreProblematicVariables(DeclarationContext ctx2)
            {
                foreach (var variable in ctx2.Variables)
                {
                    if (variable.Name == "s_Stages")
                    {
                        variable.Ignore = true;
                        variable.GenerationKind = GenerationKind.None;
                    }
                }
                foreach (var childNs in ctx2.Namespaces)
                    IgnoreProblematicVariables(childNs);
            }
            // 遍历根命名空间
            var root = unit.Namespace;
            if (root != null)
                IgnoreProblematicVariables(root);

            foreach (var ns in unit.Namespaces)
            {
                void MarkNsRecursive(Namespace n)
                {
                    if (n.Name == "std" || n.Name == "Debugger")
                        n.Ignore = true;
                    foreach (var child in n.Namespaces)
                        MarkNsRecursive(child);
                }
                MarkNsRecursive(ns);
            }

            // 已在开头按文件名忽略 DXC 头文件
            
            foreach (var func in unit.Functions)
            {
                if (SkipCheckUtils.ShouldIgnoreFunction(func))
                {
                    func.GenerationKind = GenerationKind.None;
                }
            }
        }
        
        driver.Context.TypeMaps.TypeMaps.Add("HWND", new HWNDTypeMap());
    }

    public void Postprocess(Driver driver, ASTContext ctx)
    {
        // throw new NotImplementedException();
    }

    public void Setup(Driver driver)
    {
        driver.ParserOptions.Setup(TargetPlatform.Windows);
        driver.ParserOptions.LanguageVersion = CppSharp.Parser.LanguageVersion.CPP20;
        driver.ParserOptions.MicrosoftMode = true;
        driver.ParserOptions.AddDefines("_HAS_CXX17=1");
        driver.ParserOptions.AddDefines("_HAS_CXX20=1");
        driver.ParserOptions.AddDefines("_MSVC_LANG=202002L");
        // Bypass MSVC STL's strict compiler-version check (STL1000) when using bundled Clang
        driver.ParserOptions.AddDefines("_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH");
        driver.ParserOptions.AddDefines("ARISEN_AUTOBINDING=1");
        driver.ParserOptions.NoBuiltinIncludes = false;
        driver.ParserOptions.NoStandardIncludes = false;
        // Help clang choose the correct C++ standard
        try
        {
            driver.ParserOptions.AddArguments("-std=c++23");
        }
        catch { /* Some versions of CppSharp may not expose AddArguments; ignore */ }
        driver.ParserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Infra"));
        driver.ParserOptions.AddIncludeDirs( Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Debugger"));
        driver.ParserOptions.AddIncludeDirs( Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Platform"));
        driver.ParserOptions.AddIncludeDirs( Path.Combine(GlobalConfig.s_SourceCode, "3rdparty", "dxc", "inc"));

        // Try to locate MSVC and Windows SDK system includes from environment
        var vcTools = Environment.GetEnvironmentVariable("VCToolsInstallDir");
        if (!string.IsNullOrEmpty(vcTools))
        {
            var msvcInc = Path.Combine(vcTools, "include");
            if (Directory.Exists(msvcInc))
                driver.ParserOptions.AddSystemIncludeDirs(msvcInc);
        }

        var winSdkDir = Environment.GetEnvironmentVariable("WindowsSdkDir");
        var winSdkVer = Environment.GetEnvironmentVariable("WindowsSdkVersion");
        if (!string.IsNullOrEmpty(winSdkDir))
        {
            string includeBase = Path.Combine(winSdkDir, "Include");
            string? version = null;
            if (!string.IsNullOrEmpty(winSdkVer))
            {
                version = winSdkVer.TrimEnd('/', '\\');
            }
            else if (Directory.Exists(includeBase))
            {
                // Pick the latest version folder if version env var is not set
                string latest = "";
                foreach (var dir in Directory.GetDirectories(includeBase))
                {
                    var name = Path.GetFileName(dir) ?? "";
                    if (string.CompareOrdinal(name, latest) > 0)
                        latest = name;
                }
                if (!string.IsNullOrEmpty(latest))
                    version = latest;
            }

            if (!string.IsNullOrEmpty(version))
            {
                void AddIfExists(string sub)
                {
                    var p = Path.Combine(includeBase, version!, sub);
                    if (Directory.Exists(p))
                        driver.ParserOptions.AddSystemIncludeDirs(p);
                }

                AddIfExists("ucrt");
                AddIfExists("shared");
                AddIfExists("um");
                AddIfExists("winrt");
                AddIfExists("cppwinrt");
            }
        }

        // Fallback: parse INCLUDE env var (VS Developer Prompt) for system includes
        var includeEnv = Environment.GetEnvironmentVariable("INCLUDE");
        if (!string.IsNullOrEmpty(includeEnv))
        {
            var parts = includeEnv.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (Directory.Exists(p))
                    driver.ParserOptions.AddSystemIncludeDirs(p);
            }
        }
       
        var options = driver.Options;
        
        options.GenerationOutputMode = GenerationOutputMode.FilePerUnit;
        options.OutputDir =  Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName, "NativePlatform");;
        options.GeneratorKind = GeneratorKind.CSharp;
        options.Verbose = true;
        options.Compilation.DebugMode = true;
        // options.CheckSymbols = true;
        var module = options.AddModule("Core.Platform");
        module.OutputNamespace = GlobalConfig.GetNamespace("NativePlatform");
        module.Headers.Add(@"Windows/RenderWindowAPI.h");
        // module.Headers.Add(@"ShaderCompiler/ShaderCompilerAPI.h");
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
        module.Libraries.Add(@"Core.Platform");
    
    }

    public void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.AddPass(new RenderSurfacePass());
    }
}