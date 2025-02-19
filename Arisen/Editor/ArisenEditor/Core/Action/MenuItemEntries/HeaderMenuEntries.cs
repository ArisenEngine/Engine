using System.IO;
using System.Threading.Tasks;
using ArisenEditor.Attributes;
using ArisenEditor.GameDev;
using ArisenEditor.Utilities;
using ArisenEngine;

namespace ArisenEditor.Internal.MenuItemEntries;

internal partial class HeaderMenuEntries
{
    #region Assets

    [MenuItem("Header/Content/Open C# project")]
    internal static void OpenProjectSolution()
    {
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(Path.Combine(ArisenApplication.s_ProjectRoot, ArisenApplication.s_ProjectName + @".sln"));

        });
    }

    #endregion

    #region File

    [MenuItem("Header/File/New Level")]
    internal static void NewLevel()
    {
        
    }
    
    [MenuItem("Header/File/Open Level", true)]
    internal static void OpenLevel()
    {
        
    }
    
    [MenuItem("Header/File/Save")]
    internal static void Save()
    {
        
    }

    #endregion
    
    
    
}