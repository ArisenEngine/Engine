using System.Diagnostics;
using System.IO;

namespace ArisenLauncher.Services;

public class LauncherProcessService
{
    public void LaunchEditor(EngineInstance engine, string? projectPath = null)
    {
        string editorExe = Path.Combine(engine.InstallPath, "ArisenEditor.exe");
        if (!File.Exists(editorExe))
        {
            // Fallback for dev environment where it might be in the same output dir but named Desktop
            editorExe = Path.Combine(engine.InstallPath, "ArisenEditor.Desktop.exe");
        }

        if (File.Exists(editorExe))
        {
            string args = projectPath != null ? $"-project \"{projectPath}\"" : "";
            Process.Start(new ProcessStartInfo
            {
                FileName = editorExe,
                Arguments = args,
                UseShellExecute = true
            });
        }
    }
}
