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
        
        // Include directories for both CppSharp module header lookup and Clang parsing
        var includeDirs = new[]
        {
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Foundation")),
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.HAL")),
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.HAL", "Windowing")),
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.Diagnostic")),
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.RHI")),
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.RHI", "RHI")),
            Path.GetFullPath(Path.Combine(GlobalConfig.s_SourceCode, "Core", "Core.ShaderCompiler")),
        };

        foreach (var dir in includeDirs)
        {
            module.IncludeDirs.Add(dir);
            Console.WriteLine($"  [IncludeDir] {dir} (exists: {Directory.Exists(dir)})");
        }

        // Map each header to the include dir it belongs to, using relative paths
        var headerEntries = new (string relPath, string includeDir)[]
        {
            ("Base/Assertion.h",              includeDirs[0]), // Core.Foundation
            ("Diagnostics/ILogHandler.h",     includeDirs[0]), // Core.Foundation
            ("Logger/Logger.h",               includeDirs[3]), // Core.Diagnostic
            ("Common/EngineInit.h",           includeDirs[1]), // Core.HAL
            ("Windowing/RenderWindowAPI.h",   includeDirs[1]), // Core.HAL
            ("RHI/Handles/RHIHandle.h",       includeDirs[4]), // Core.RHI
            ("RHI/Loader/RHILoader.h",        includeDirs[4]), // Core.RHI
            ("RHI/Definitions/DeviceLimits.h",includeDirs[4]), // Core.RHI
            ("RHI/Core/RHIDevice.h",          includeDirs[4]), // Core.RHI
            ("RHI/Core/RHIInstance.h",         includeDirs[4]), // Core.RHI
            ("RHI/Enums/Pipeline/EProgramStage.h", includeDirs[4]), // Core.RHI
            ("ShaderCompiler/ShaderCompilerAPI.h", includeDirs[6]), // Core.ShaderCompiler
            ("ShaderCompiler/CoreShaderCompilerCommon.h", includeDirs[6]), // Core.ShaderCompiler
        };

        Console.WriteLine("  Header file verification:");
        foreach (var (relPath, includeDir) in headerEntries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(includeDir, relPath));
            var exists = File.Exists(fullPath);
            Console.WriteLine($"    {relPath} -> {fullPath} (exists: {exists})");
            module.Headers.Add(relPath);
        }

        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void SetupModule(Driver driver) { }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.IsSystemHeader) continue;

            var fileName = Path.GetFileName(unit.FileName);
            var isModuleUnit = string.IsNullOrEmpty(fileName) || fileName == "ArisenNative" || fileName == "ArisenNative.cs";
            
            if (isModuleUnit || 
                fileName == "Assertion.h" || 
                fileName == "ILogHandler.h" || 
                fileName == "Logger.h" || 
                fileName == "EngineInit.h" || 
                fileName == "RenderWindowAPI.h" || 
                fileName == "RHIHandle.h" || 
                fileName == "RHILoader.h" ||
                fileName == "RHIDevice.h" ||
                fileName == "RHIInstance.h" ||
                fileName == "EProgramStage.h" ||
                fileName == "DeviceLimits.h" ||
                fileName == "GraphicsAPI.h" ||
                fileName == "EPresentMode.h" ||
                fileName == "EFormat.h" ||
                fileName == "ShaderCompilerAPI.h" ||
                fileName == "CoreShaderCompilerCommon.h")
            {
                unit.Ignore = false;
            }
            else if (!unit.IsSystemHeader)
            {
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
