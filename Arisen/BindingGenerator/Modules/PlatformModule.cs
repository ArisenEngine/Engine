using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using BindingGenerator.Passes;

namespace BindingGenerator.Modules;

public class PlatformModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.Platform";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        options.OutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName, "NativePlatform");
        
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativePlatform");
        
        module.Headers.Add(@"Windowing/RenderWindowAPI.h");
        module.Headers.Add(@"ShaderCompiler/ShaderCompilerAPI.h");
        
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.IsSystemHeader) continue;
            unit.Ignore = false;
        }
    }

    public override void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.AddPass(new RenderSurfacePass());
    }
}
