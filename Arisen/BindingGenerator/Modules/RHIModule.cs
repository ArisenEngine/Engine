using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using BindingGenerator.TypeMaps;

namespace BindingGenerator.Modules;

public class RHIModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.RHI";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeRHI");
        
        module.Headers.Add(@"RHI/Handles/RHIHandle.h");
        
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.FileName.Contains("RHIHandle.h"))
                unit.Ignore = false;
        }
        
        driver.Context.TypeMaps.TypeMaps.Add("RHIHandle", new RHIHandleTypeMap());
    }
}
