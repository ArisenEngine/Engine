using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace BindingGenerator.Debugger;

public class DebuggerLibrary : ILibrary
{
    
    public void Preprocess(Driver driver, ASTContext ctx)
    {
        // 遍历所有翻译单元并忽略特定的头文件
        foreach (var unit in ctx.TranslationUnits)
        {
            // 忽略整个 std 命名空间映射，避免与平台模块的 std 投影冲突
            foreach (var ns in unit.Namespaces)
            {
                if (ns.Name == "std")
                {
                    ns.Ignore = true;
                }
            }
            if (unit.FileName.Contains("PrimitiveTypes.h"))
            {
                unit.Ignore = true;
            }
            
            foreach (var func in unit.Functions)
            {
                if (SkipCheckUtils.ShouldIgnoreFunction(func))
                {
                    func.GenerationKind = GenerationKind.None;
                }
            }
        }
    }

    public void Postprocess(Driver driver, ASTContext ctx)
    {
    }

    public void Setup(Driver driver)
    {
        driver.ParserOptions.Setup(TargetPlatform.Windows);
        // Bypass MSVC STL's strict compiler-version check (STL1000) when using bundled Clang
        driver.ParserOptions.AddDefines("_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH");
        driver.ParserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Infra"));
        driver.ParserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Debugger"));
        var options = driver.Options;
        options.GenerationOutputMode = GenerationOutputMode.FilePerUnit;
        options.OutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName, "NativeDebugger");
        options.GeneratorKind = GeneratorKind.CSharp;
        options.Verbose = true;
        options.Compilation.DebugMode = true;
        // 设置 C# 输出的 namespace
        // options.CheckSymbols = true;
        var module = options.AddModule("Core.Debugger");
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeDebugger");
        module.Headers.Clear();
        module.Headers.Add(@"Logger/Logger.h");
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
        module.Libraries.Add(@"Core.Debugger");
    
    }

    public void SetupPasses(Driver driver)
    {
        // throw new NotImplementedException();
    }
}