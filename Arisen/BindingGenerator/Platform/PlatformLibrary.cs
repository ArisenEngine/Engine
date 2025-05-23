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
        
        // 遍历所有命名空间并忽略 std
        foreach (var unit in ctx.TranslationUnits)
        {
            foreach (var ns in unit.Namespaces)
            {
                if (ns.Name == "std")
                {
                    ns.Ignore = true;
                }
            }
            
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
        driver.ParserOptions.AddIncludeDirs(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Infra") + Path.PathSeparator);
        driver.ParserOptions.AddIncludeDirs( Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Debugger") + Path.PathSeparator);
        driver.ParserOptions.AddIncludeDirs( Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Platform") + Path.PathSeparator);
       
        var options = driver.Options;
        
        options.GenerationOutputMode = GenerationOutputMode.FilePerUnit;
        options.OutputDir =  Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName, "Platform");;
        options.GeneratorKind = GeneratorKind.CSharp;
        options.Verbose = true;
        options.Compilation.DebugMode = true;
        // options.CheckSymbols = true;
        var module = options.AddModule("libCore.Platform");
        module.OutputNamespace = "";
        module.Headers.Add(@"/Windows/RenderWindowAPI.h");
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
        module.Libraries.Add(@"libCore.Platform");
        
    }

    public void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.AddPass(new RenderSurfacePass());
    }
}