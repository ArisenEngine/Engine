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
        module.Headers.Add(@"RHI/Loader/RHILoader.h");
        module.Headers.Add(@"RHI/Core/RHIDevice.h");
        module.Headers.Add(@"RHI/Core/RHIInstance.h");
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
            var fileName = Path.GetFileName(unit.FileName);
            if (fileName == "Assertion.h" || 
                fileName == "Logger.h" || 
                fileName == "EngineInit.h" || 
                fileName == "RenderWindowAPI.h" || 
                fileName == "RHIHandle.h" || 
                fileName == "RHILoader.h" ||
                fileName == "RHIDevice.h" ||
                fileName == "RHIInstance.h" ||
                fileName == "ShaderCompilerAPI.h")
            {
                unit.Ignore = false;
            }
            else if (!unit.IsSystemHeader)
            {
                // Aggressively ignore anything else that's not our primary headers
                unit.Ignore = true;
            }

            foreach (var klass in unit.Classes)
            {
                // 1. Ignore empty "Tag" structs (C++ template markers)
                if (klass.Name.EndsWith("Tag") || klass.Name.Contains("Helper") || klass.Name.Contains("Internal"))
                {
                    klass.Ignore = true;
                    continue;
                }

                // 2. Hide common messy C++ fields
                foreach (var field in klass.Fields)
                {
                    if (field.Name.StartsWith("vfptr_") || 
                        field.Name.Contains("_Internal") || 
                        field.Access == AccessSpecifier.Private || 
                        field.Access == AccessSpecifier.Protected)
                    {
                        field.Ignore = true;
                    }
                }
                
                // 3. Cleanup constructors
                foreach (var ctor in klass.Constructors)
                {
                    if (ctor.Access != AccessSpecifier.Public)
                        ctor.Ignore = true;
                }
            }

            // 4. Cleanup Functions
            foreach (var func in unit.Functions)
            {
                if (SkipCheckUtils.ShouldIgnoreFunction(func) || func.Access != AccessSpecifier.Public)
                {
                    func.Ignore = true;
                }
            }

            // 5. Cleanup Namespaces
            foreach (var ns in unit.Namespaces)
            {
                if (ns.Name == "std" || ns.Name == "stdext" || ns.Name == "__msvc_all_allocator")
                {
                    ns.Ignore = true;
                }
            }
        }
        
        if (!driver.Context.TypeMaps.TypeMaps.ContainsKey("RHIHandle"))
            driver.Context.TypeMaps.TypeMaps.Add("RHIHandle", new RHIHandleTypeMap());
        
        // CppSharp usually has a default std::string map. We only add if we want to override.
        if (!driver.Context.TypeMaps.TypeMaps.ContainsKey("std::string"))
            driver.Context.TypeMaps.TypeMaps.Add("std::string", new StdStringTypeMap());
    }

    public override void SetupPasses(Driver driver)
    {
        driver.Context.TranslationUnitPasses.AddPass(new Passes.RenderSurfacePass());
        driver.Context.TranslationUnitPasses.AddPass(new Passes.PruningPass());
    }
}
