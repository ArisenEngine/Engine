using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Generators.CSharp;
using CppSharp.Types;

namespace BindingGenerator.TypeMaps;

[TypeMap("std::basic_string<char, std::char_traits<char>, std::allocator<char>>")]
[TypeMap("std::string")]
[TypeMap("ArisenEngine::String")]
public class StdStringTypeMap : TypeMap
{
    public override CppSharp.AST.Type CSharpSignatureType(TypePrinterContext ctx)
    {
        return new CustomType("string");
    }

    public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
    {
        // Use UTF8Marshaller for std::string
        ctx.Return.Write($"CppSharp.Runtime.UTF8Marshaller.UTF8ToNative({ctx.Parameter.Name})");
    }

    public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
    {
        ctx.Return.Write($"CppSharp.Runtime.UTF8Marshaller.NativeToUTF8({ctx.ReturnVarName})");
    }
}
