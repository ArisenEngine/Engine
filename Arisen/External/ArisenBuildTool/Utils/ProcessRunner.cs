using System;
using System.Diagnostics;

namespace ArisenBuildTool.Utils;

public static class ProcessRunner
{
    public static void Run(string fileName, string args, string workingDir)
    {
        Logger.Info($"Executing: {fileName} {args} (in {workingDir})");
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            Logger.Error($"Failed to start process: {fileName}");
            return;
        }

        proc.OutputDataReceived += (sender, e) => { if (e.Data != null) Logger.Info($"  {e.Data}"); };
        proc.ErrorDataReceived += (sender, e) => { if (e.Data != null) Logger.Error($"  {e.Data}"); };

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            Logger.Warning($"Process '{fileName}' exited with code {proc.ExitCode}");
        }
    }
}
