using System.Threading.Tasks;
using NebulaEditor.Attributes;
using NebulaEditor.GameDev;
using NebulaEditor.Utilities;
using NebulaEngine;

namespace NebulaEditor.Internal.MenuItemEntries;

internal static class HeaderMenuEntries
{
    [MenuItem("Header/Assets/Open C# project")]
    public static void OpenProjectSolution()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "Assets/Open C# project");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Cut/Cut2")]
    public static void OpenProjectSolution2()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "Assets/2");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Custom/Custom22")]
    public static void OpenProj22ectSolution2()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "Custom/22");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Custom/Custom22")]
    public static void OpenProj22ectSoluddtion2()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "Help/22");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Delete/Delete232/Delete333")]
    public static void OpenProjectSolution24()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "Assets/232/333");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Custom/Custom2")]
    public static void OpenProjectSolution3(object? sender, object?[] objects)
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "File/2");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Custom/Custom1")]
    public static void OpenProjectSolution33()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "File/1");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
    
    [MenuItem("Project/Custom/Custom666")]
    public static void OpenProjectSolution4()
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Test", "Assets/1");
        return;
        Task.Run(()=> {

            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

        });
    }
}