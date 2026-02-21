using CppSharp.AST;
using CppSharp.Passes;

namespace BindingGenerator.Passes;

public class PruningPass : TranslationUnitPass
{
    public override bool VisitTranslationUnit(TranslationUnit unit)
    {
        // Recursively visit all namespaces and classes
        return base.VisitTranslationUnit(unit);
    }

    /*
    public override bool VisitNamespace(Namespace ns)
    {
        // Handled in Preprocess for now
        return base.VisitNamespace(ns);
    }
    */

    public override bool VisitClassDecl(Class klass)
    {
        // 2. Ignore internal helper classes
        if (klass.Name.Contains("_Compressed_pair") || 
            klass.Name.Contains("_Vector_val") || 
            klass.Name.Contains("_Optional_destruct_base"))
        {
            klass.Ignore = true;
            return false;
        }

        // 3. Mark as specialized if we don't need C# inheritance/vtables
        if (klass.Name == "Logger" || klass.Name == "RHIDevice" || klass.Name == "RHILoader")
        {
            // For now, let's just ignore vtables if possible (this is version dependent)
            // klass.IsSealed = true; 
        }

        return base.VisitClassDecl(klass);
    }
}
