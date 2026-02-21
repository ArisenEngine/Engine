using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace BindingGenerator.Modules;

public class FoundationModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.Foundation";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeFoundation");
        
        // Target Headers
        module.Headers.Add(@"Base/Assertion.h");
        
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.FileName.Contains("Assertion.h"))
                unit.Ignore = false;
        }
    }
}
