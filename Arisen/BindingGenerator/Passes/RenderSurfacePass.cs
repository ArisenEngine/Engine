using CppSharp.AST;
using CppSharp.Passes;

namespace BindingGenerator.Passes;

public class RenderSurfacePass : TranslationUnitPass
{
    public override bool VisitFunctionDecl(Function function)
    {
        if (function.Ignore) function.Ignore = false;
        
        foreach (var param in function.Parameters)
        {
            var type = param.Type;
            while (type is TypedefType alias) type = alias.Declaration.QualifiedType.Type;
            
            bool shouldReplace = false;
            try {
                var name = type.ToString();
                if (name.Contains("HWND") || name.Contains("WindowProc") || name.Contains("WindowExitResize"))
                    shouldReplace = true;
            } catch { }

            if (shouldReplace)
            {
                param.QualifiedType = new QualifiedType(new BuiltinType(PrimitiveType.IntPtr));
            }
        }
        
        return base.VisitFunctionDecl(function);
    }
}
