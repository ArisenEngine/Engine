using System.IO;
using System.Threading.Tasks;
using ArisenEditorFramework.Attributes;
using ArisenEditor.GameDev;
using ArisenEditor.Utilities;
using ArisenEngine;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEditor.Internal.MenuItemEntries;

internal partial class HeaderMenuEntries
{
    #region Assets

    [MenuItem("Header/Content/Open C# project")]
    internal static void OpenProjectSolution()
    {
        Task.Run(()=> {

            ArisenApplication.RequestExit();
            var env = EngineKernel.Instance.GetSubsystem<EnvironmentSubsystem>();
            string root = env?.ProjectRoot ?? string.Empty;
            string name = env?.ProjectName ?? string.Empty;
            ProjectSolution.OpenVisualStudio(Path.Combine(root, name + @".sln"));

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