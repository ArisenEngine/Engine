using System.Collections.Generic;
using System.IO;
using CppSharp;
using CppSharp.AST;
using BindingGenerator.TypeMaps;

namespace BindingGenerator.Modules;

public class ArisenCombinedLibrary : ArisenLibrary
{
    public override string GetLibraryName() => "ArisenCombined";

    public override void Setup(Driver driver)
    {
        base.Setup(driver);
        
        var options = driver.Options;
        options.OutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName);

        // Core.Foundation
        var foundation = options.AddModule("Core.Foundation");
        foundation.OutputNamespace = GlobalConfig.GetNamespace("NativeFoundation");
        foundation.Headers.Add(@"Base/Assertion.h");
        
        // Core.Diagnostic
        var diagnostic = options.AddModule("Core.Diagnostic");
        diagnostic.OutputNamespace = GlobalConfig.GetNamespace("NativeDiagnostic");
        diagnostic.Headers.Add(@"CoreDiagnosticCommon.h");
        diagnostic.Headers.Add(@"Logger/Logger.h");

        // Core.HAL
        var hal = options.AddModule("Core.HAL");
        hal.OutputNamespace = GlobalConfig.GetNamespace("NativeHAL");
        hal.Headers.Add(@"CoreHALCommon.h");
        hal.Headers.Add(@"Common/EngineInit.h");
        hal.Headers.Add(@"Windowing/RenderWindowAPI.h");

        // Core.RHI
        var rhi = options.AddModule("Core.RHI");
        rhi.OutputNamespace = GlobalConfig.GetNamespace("NativeRHI");
        rhi.Headers.Add(@"RHI/Handles/RHIHandle.h");

        // Core.ShaderCompiler
        var shader = options.AddModule("Core.ShaderCompiler");
        shader.OutputNamespace = GlobalConfig.GetNamespace("NativeShaderCompiler");
        shader.Headers.Add(@"ShaderCompiler/ShaderCompilerAPI.h");

        foreach (var module in options.Modules)
        {
            module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
        }
    }

    public override void SetupModule(Driver driver) { }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.IsSystemHeader) continue;

            // Preserve primary headers and common headers with dummy exports to prevent pruning
            if (unit.FileName.EndsWith("Assertion.h") || 
                unit.FileName.EndsWith("Logger.h") || 
                unit.FileName.EndsWith("EngineInit.h") || 
                unit.FileName.EndsWith("RenderWindowAPI.h") || 
                unit.FileName.Contains("RHIHandle.h") || 
                unit.FileName.EndsWith("ShaderCompilerAPI.h") ||
                unit.FileName.EndsWith("Common.h")) // Catch-all for dummy exports
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
