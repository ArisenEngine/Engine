namespace BindingGenerator;

public static class GlobalConfig
{
    public static string s_ProjectName = "AutoBinding";
    public static string s_Output = "";
    public static string s_SourceCode = "";
    public static string s_LibraryPath = "";
    
    private const string BaseNamespace = "ArisenBinding";

    public static string GetNamespace(string moduleName)
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            return BaseNamespace;
        }
        return $"{BaseNamespace}.{moduleName}";
    }
}