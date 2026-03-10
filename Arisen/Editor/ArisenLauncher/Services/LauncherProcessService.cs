using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ArisenLauncher.Services;

public class LauncherProcessService
{
    private readonly LogService _logService;
    private readonly List<Process> _activeProcesses = new();
    private readonly object _lock = new();

    public event Action? AllInstancesClosed;
    public event Action? ProcessStarted;

    public LauncherProcessService(LogService logService)
    {
        _logService = logService;
    }

    public int ActiveInstanceCount
    {
        get
        {
            lock (_lock) return _activeProcesses.Count;
        }
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
                lock (_lock)
                {
                    _activeProcesses.Add(process);
                }
                
                _logService.Info($"Editor launched successfully (PID: {process.Id}). Active instances: {ActiveInstanceCount}");
                ProcessStarted?.Invoke();

                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                {
                    bool lastOne = false;
                    lock (_lock)
                    {
                        if (_activeProcesses.Remove(process))
                        {
                            if (process.ExitCode != 0)
                            {
                                _logService.Critical($"Editor (PID: {process.Id}) exited with error code: {process.ExitCode}");
                            }
                            else
                            {
                                _logService.Info($"Editor (PID: {process.Id}) exited normally.");
                            }
                            
                            if (_activeProcesses.Count == 0)
                            {
                                lastOne = true;
                            }
                        }
                    }

                    if (lastOne)
                    {
                        _logService.Info("All Editor instances closed. Triggering restore.");
                        AllInstancesClosed?.Invoke();
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
