using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using BindingGenerator.Passes;

namespace BindingGenerator.Modules;

public class HALModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.HAL";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeHAL");
        
        module.Headers.Add(@"Common/EngineInit.h");
        module.Headers.Add(@"Windowing/RenderWindowAPI.h");
        
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.FileName.Contains("EngineInit.h") || unit.FileName.Contains("RenderWindowAPI.h"))
                unit.Ignore = false;
        }
    }

    public override void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.AddPass(new RenderSurfacePass());
    }
}
