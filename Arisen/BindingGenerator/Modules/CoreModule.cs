using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using BindingGenerator.TypeMaps;

namespace BindingGenerator.Modules;

public class CoreModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.Native";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        options.OutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName, "NativeCore");
        
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeCore");
        
        // Target Headers
        module.Headers.Add(@"Common/EngineInit.h");
        module.Headers.Add(@"RHI/Handles/RHIHandle.h");
        
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.IsSystemHeader) continue;
            unit.Ignore = false;
            
            foreach (var func in unit.Functions)
            {
                if (SkipCheckUtils.ShouldIgnoreFunction(func))
                {
                    func.GenerationKind = GenerationKind.None;
                }
            }
        }
        
        driver.Context.TypeMaps.TypeMaps.Add("RHIHandle", new RHIHandleTypeMap());
    }
}
