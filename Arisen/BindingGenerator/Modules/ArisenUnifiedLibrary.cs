using System.Collections.Generic;
using System.IO;
using CppSharp;
using CppSharp.AST;
using BindingGenerator.TypeMaps;

namespace BindingGenerator.Modules;

public class ArisenUnifiedLibrary : ArisenLibrary
{
    public override string GetLibraryName() => "ArisenNative"; // Universal name for generation

    public override void Setup(Driver driver)
    {
        base.Setup(driver);
        
        var options = driver.Options;
        options.OutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName);

        // One module to rule them all (avoids CppSharp multi-module crashes and duplicates)
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = "ArisenBinding"; 
        
        // Add all headers
        module.Headers.Add(@"Base/Assertion.h");
        module.Headers.Add(@"Logger/Logger.h");
        module.Headers.Add(@"Common/EngineInit.h");
        module.Headers.Add(@"Windowing/RenderWindowAPI.h");
        module.Headers.Add(@"RHI/Handles/RHIHandle.h");
        module.Headers.Add(@"ShaderCompiler/ShaderCompilerAPI.h");

        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void SetupModule(Driver driver) { }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.IsSystemHeader) continue;

            // Only generate code for our primary headers
            if (unit.FileName.EndsWith("Assertion.h") || 
                unit.FileName.EndsWith("Logger.h") || 
                unit.FileName.EndsWith("EngineInit.h") || 
                unit.FileName.EndsWith("RenderWindowAPI.h") || 
                unit.FileName.Contains("RHIHandle.h") || 
                unit.FileName.EndsWith("ShaderCompilerAPI.h"))
            {
                unit.Ignore = false;
            }

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

    public override void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.AddPass(new Passes.RenderSurfacePass());
    }
}
