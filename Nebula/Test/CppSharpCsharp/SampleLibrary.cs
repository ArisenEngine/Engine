using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;

namespace CppSharpCsharp;

public class SampleLibrary : ILibrary
{
    public void Preprocess(Driver driver, ASTContext ctx)
    {
        throw new NotImplementedException();
    }

    public void Postprocess(Driver driver, ASTContext ctx)
    {
        throw new NotImplementedException();
    }

    public void Setup(Driver driver)
    {
        var options = driver.Options;
        options.GeneratorKind = GeneratorKind.CSharp;
        var module = options.AddModule("Sample");
        module.IncludeDirs.Add(@"C:\Sample\include");
        module.Headers.Add("Foo.h");
        module.LibraryDirs.Add(@"D:\EngineSource\Nebula\Engine\Nebula\x64\Debug");
        module.Libraries.Add("Sample.lib");
    }

    public void SetupPasses(Driver driver)
    {
        throw new NotImplementedException();
    }
}