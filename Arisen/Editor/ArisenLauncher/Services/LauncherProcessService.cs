using System;
using System.Diagnostics;
using System.IO;

namespace ArisenLauncher.Services;

public class LauncherProcessService
{
    private readonly LogService _logService;

    public LauncherProcessService(LogService logService)
    {
        _logService = logService;
    }

    public void LaunchEditor(EngineInstance engine, string? projectPath = null)
    {
        string editorExe = Path.Combine(engine.InstallPath, "ArisenEditor.Desktop.exe");
        if (!File.Exists(editorExe))
        {
            _logService.Error($"Editor executable not found: {editorExe}");
            return;
        }

        string arguments = projectPath != null ? $"-project \"{projectPath}\"" : "";
        _logService.Info($"Launching Editor: {editorExe} {arguments}");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = editorExe,
                Arguments = arguments,
                UseShellExecute = true,
                WorkingDirectory = engine.InstallPath
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                _logService.Info($"Editor launched successfully (PID: {process.Id})");
                
                // Monitor process in background
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    if (process.ExitCode != 0)
                    {
                        _logService.Critical($"Editor process exited with error code: {process.ExitCode}");
                    }
                    else
                    {
                        _logService.Info("Editor process exited normally.");
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to launch Editor process.", ex);
        }
    }
}
