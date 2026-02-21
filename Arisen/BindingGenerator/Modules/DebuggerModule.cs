using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace BindingGenerator.Modules;

public class DebuggerModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.Debugger";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        options.OutputDir = Path.Combine(GlobalConfig.s_Output, GlobalConfig.s_ProjectName, "NativeDebugger");
        
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeDebugger");
        
        module.Headers.Add(@"Logger/Logger.h");
        
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
}
