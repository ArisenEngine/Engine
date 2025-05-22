using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Generators.CSharp;
using CppSharp.Types;
using Type = CppSharp.AST.Type;

namespace CSharpBindingGenerator;

public class HWNDTypeMap : TypeMap
{
    public override Type CSharpSignatureType(TypePrinterContext ctx)
    {
        // 将 HWND 类型映射为 C# 的 IntPtr
        return new CILType(typeof(IntPtr));
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