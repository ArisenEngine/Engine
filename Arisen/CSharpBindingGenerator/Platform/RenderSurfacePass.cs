using CppSharp.AST;
using CppSharp.Passes;

namespace CSharpBindingGenerator;

public class RenderSurfacePass : TranslationUnitPass
{
    // 重写 VisitFunctionDecl 来修改函数签名
    public override bool VisitFunctionDecl(Function function)
    {
        // 确保函数没有被忽略
        if (function.Ignore)
        {
            function.Ignore = false;
        }
        
        // 检查并修改 HWND 类型
        foreach (var param in function.Parameters)
        {
            var paramType = param.Type.ToString();
            if (paramType.Contains("HWND") 
                || paramType.Contains("WindowProc")
                || paramType.Contains("WindowExitResize"))
            {
                var intPtrType = new QualifiedType(new BuiltinType(PrimitiveType.IntPtr));
                param.QualifiedType = intPtrType;  // 设置新的类型
            }
        }
        
        return base.VisitFunctionDecl(function);
    }

    public override bool VisitDeclaration(Declaration decl, TypeQualifiers quals)
    {
        return base.VisitDeclaration(decl, quals);
    }
}