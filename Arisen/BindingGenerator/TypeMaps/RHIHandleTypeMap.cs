using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Generators.CSharp;
using CppSharp.Types;

namespace BindingGenerator.TypeMaps;

[TypeMap("ArisenEngine::RHI::RHIHandle")]
public class RHIHandleTypeMap : TypeMap
{
    public override CppSharp.AST.Type CSharpSignatureType(TypePrinterContext ctx)
    {
        // Map all RHIHandle<T> to our shared RHIHandle struct in C#
        return new CustomType("global::ArisenBinding.RHI.RHIHandle");
    }

    public override void CSharpMarshalToNative(CSharpMarshalContext ctx)
    {
        ctx.Return.Write(ctx.Parameter.Name);
    }

    public override void CSharpMarshalToManaged(CSharpMarshalContext ctx)
    {
        ctx.Return.Write(ctx.ReturnVarName);
    }
}
