using System.Collections.Generic;
using ArisenBuildTool.Models;
using ArisenBuildTool.Utils;

namespace ArisenBuildTool.Services;

public static class NativeDeploymentService
{
    public static void Deploy(
        List<PackageInfo> allPackages,
        List<string> outputDirs,
        string profile,
        bool? enableProfiler = null)
    {
        Logger.Info(
            $"Deploying native payloads for {allPackages.Count} packages to {outputDirs.Count} directories for profile '{profile}'...");
        NativePayloadIntegrityService.DeployStaticPayloads(allPackages, outputDirs, enableProfiler);
    }
}
