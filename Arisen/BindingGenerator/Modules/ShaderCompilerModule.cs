using System.IO;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace BindingGenerator.Modules;

public class ShaderCompilerModule : ArisenLibrary
{
    public override string GetLibraryName() => "Core.ShaderCompiler";

    public override void SetupModule(Driver driver)
    {
        var options = driver.Options;
        var module = options.AddModule(GetLibraryName());
        module.OutputNamespace = GlobalConfig.GetNamespace("NativeShaderCompiler");
        
        module.Headers.Add(@"ShaderCompiler/ShaderCompilerAPI.h");
        
        module.LibraryDirs.Add(GlobalConfig.s_LibraryPath);
    }

    public override void Preprocess(Driver driver, ASTContext ctx)
    {
        foreach (var unit in ctx.TranslationUnits)
        {
            if (unit.FileName.Contains("ShaderCompilerAPI.h"))
                unit.Ignore = false;
        }
    }
}
