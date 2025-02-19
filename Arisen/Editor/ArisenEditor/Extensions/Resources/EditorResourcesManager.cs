using Avalonia;
using Avalonia.Controls;

namespace ArisenEditor.Public.Resources;

/// <summary>
/// 
/// </summary>
public static partial class EditorResourcesManager
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="resourceDictionary"></param>
    public static void Merge(ResourceDictionary resourceDictionary)
    {
        Application.Current?.Resources.MergedDictionaries.Add(resourceDictionary);
    }
}