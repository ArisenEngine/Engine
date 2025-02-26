using CppSharp.AST;

namespace CSharpBindingGenerator;

public static class SkipCheckUtils
{
    public static bool ShouldIgnoreFunction(Function func)
    {
        // 根据名称、参数等判断是否忽略
        return func.Name.Contains("dummy");
    }

}