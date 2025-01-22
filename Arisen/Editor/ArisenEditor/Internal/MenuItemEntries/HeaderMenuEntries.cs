using System.IO;
using System.Threading.Tasks;
using ArisenEditor.Attributes;
using ArisenEditor.GameDev;
using ArisenEditor.Utilities;
using ArisenEngine;

namespace ArisenEditor.Internal.MenuItemEntries;

internal static class HeaderMenuEntries
{
    #region Assets

    [MenuItem("Header/Assets/Open C# project")]
    public static void OpenProjectSolution()
    {
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(Path.Combine(ArisenApplication.s_ProjectRoot, ArisenApplication.s_ProjectName + @".sln"));

        });
    }

    #endregion

    #region File

    [MenuItem("Header/File/New Scene")]
    public static void NewScene()
    {
        
    }
    
    [MenuItem("Header/File/Open Scene", true)]
    public static void OpenScene()
    {
        
    }
    
    [MenuItem("Header/File/Save")]
    public static void Save()
    {
        
    }

    #endregion
    
}