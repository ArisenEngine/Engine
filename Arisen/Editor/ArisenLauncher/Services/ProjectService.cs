using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading;

using ArisenLauncher.Models;
using System.Diagnostics;

namespace ArisenLauncher.Services;

public class ProjectService
{
    private readonly ILogService _logService;
    private readonly ConfigService _configService;

    public ProjectService(ILogService logService, ConfigService configService)
    {
        _logService = logService;
        _configService = configService;
    }

    public LauncherProjectMetadata? LoadProject(string projectPath)
    {
        if (!File.Exists(projectPath)) return null;

        try
        {
            string json = File.ReadAllText(projectPath);
            var metadata = JsonSerializer.Deserialize<LauncherProjectMetadata>(json);
            if (metadata != null)
            {
                metadata.ProjectPath = projectPath;
                metadata.LastModified = File.GetLastWriteTime(projectPath);

                // Populate Profiles from manifest.json
                string projectDir = Path.GetDirectoryName(projectPath)!;
                string manifestPath = Path.Combine(projectDir, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifest = ManifestJson.DeserializeFile<ProjectManifest>(manifestPath);
                        if (manifest?.Profiles != null)
                        {
                            metadata.AvailableProfiles.Clear();
                            foreach (var profile in manifest.Profiles.Keys)
                            {
                                metadata.AvailableProfiles.Add(profile);
                            }

                            if (metadata.AvailableProfiles.Contains("Development"))
                                metadata.SelectedProfile = "Development";
                            else if (metadata.AvailableProfiles.Count > 0)
                                metadata.SelectedProfile = metadata.AvailableProfiles[0];
                        }
                        else
                        {
                            // Fallback if no profiles defined
                            metadata.AvailableProfiles.Clear();
                            metadata.AvailableProfiles.Add("Development");
                            metadata.SelectedProfile = "Development";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.Warning($"Failed to parse profiles for {projectPath}: {ex.Message}");
                    }
                }

                return metadata;
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to load project at {projectPath}", ex);
        }
                return null;
    }

    public bool CreateProject(string folderPath, string name, EngineInstance engine, string? templateName = null, string? defaultPackageId = null)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (!string.IsNullOrEmpty(templateName))
            {
                string templatePath = Path.Combine(engine.InstallPath, "Templates", templateName);
                if (Directory.Exists(templatePath))
                {
                    _logService.Info($"Copying template '{templateName}' to '{folderPath}'");
                    CopyDirectory(templatePath, folderPath, true);
                }
                else
                {
                    _logService.Warning($"Template '{templateName}' not found at {templatePath}");
                }
            }

            string projectFile = Path.Combine(folderPath, $"{name}.arisenproj");
            var metadata = new LauncherProjectMetadata
            {
                Name = name,
                ProjectId = Guid.NewGuid(),
                EngineVersionId = engine.Id,
                ProjectPath = projectFile,
                LastModified = DateTime.Now
            };

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(projectFile, json);

            Directory.CreateDirectory(Path.Combine(folderPath, "Local"));
            Directory.CreateDirectory(Path.Combine(folderPath, ".Cache"));
            Directory.CreateDirectory(Path.Combine(folderPath, "Assets"));
            Directory.CreateDirectory(Path.Combine(folderPath, "Logs"));

            string userPkgId = string.IsNullOrWhiteSpace(defaultPackageId) ? $"com.user.{SanitizePackageSegment(name)}" : defaultPackageId;
            string userPkgPath = Path.Combine(folderPath, "Local", userPkgId);
            Directory.CreateDirectory(userPkgPath);

            string packageJson = $$"""
            {
              // Human-authored package identity. Keep id/version stable because workspace manifests and locks refer to them.
              "id": {{ToJsonString(userPkgId)}},
              "name": {{ToJsonString($"{name} Logic")}},
              "version": "1.0.0",
              "layer": "user",
              "type": "managed",
              "description": "Default project game assembly",

              // Add explicit package dependencies here as this project starts using engine packages.
              "dependencies": {}
            }
            """;
            File.WriteAllText(Path.Combine(userPkgPath, "package.json"), packageJson);

            // Provide an immediate kernel injection point for the user's game package.
            string safeNamespace = $"ArisenGame.{SanitizeCSharpIdentifier(name)}";
            File.WriteAllText(Path.Combine(userPkgPath, "GameEntry.cs"),
                $$"""
                using ArisenKernel.Diagnostics;
                using ArisenKernel.Packages;
                using ArisenKernel.Services;

                namespace {{safeNamespace}};

                public sealed class GameEntry : IPackageEntry
                {
                    public void OnLoad(IServiceRegistry services)
                    {
                        KernelLog.Info("[{{name}}] Game package loaded.");
                    }

                    public void OnUnload(IServiceRegistry services)
                    {
                        KernelLog.Info("[{{name}}] Game package unloaded.");
                    }
                }
                """);

            string manifestFile = Path.Combine(folderPath, "manifest.json");
            string manifestJson = $$"""
            {
              // Workspace display name and engine compatibility selector.
              "Name": {{ToJsonString(name)}},
              "EngineVersion": "Current",

              // Base packages are loaded for every profile.
              "Packages": [
                {
                  "Id": {{ToJsonString(userPkgId)}},
                  "Url": {{ToJsonString($"file://Local/{userPkgId}")}},
                  "Version": "1.0.0"
                }
              ],

              // Profiles append packages for a launch mode. Development usually includes editor tooling.
              "Profiles": {
                "Development": {
                  "IsEditor": true,
                  "Packages": []
                },
                "Production": {
                  "IsEditor": false,
                  "Packages": []
                }
              }
            }
            """;
            File.WriteAllText(manifestFile, manifestJson);

            string gitignoreFile = Path.Combine(folderPath, ".gitignore");
            string gitignoreContent = """
            # Arisen Engine Generated Folders
            .Cache/
            .arisen/
            Logs/
            Outputs/
            *.user
            """;
            File.WriteAllText(gitignoreFile, gitignoreContent);

            if (!_configService.Settings.RecentProjects.Contains(projectFile))
            {
                _configService.Settings.RecentProjects.Insert(0, projectFile);
                _configService.Save();
            }

            _logService.Info($"Project created successfully: {projectFile}");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to create project.", ex);
            return false;
        }
    }

    private static string SanitizePackageSegment(string value)
    {
        var chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "project" : new string(chars);
    }

    private static string SanitizeCSharpIdentifier(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        string result = chars.Length == 0 ? "Project" : new string(chars);
        return char.IsDigit(result[0]) ? $"Project{result}" : result;
    }

    private static string ToJsonString(string value)
    {
        return JsonSerializer.Serialize(value);
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        // If recursive and subdirectories exist, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }

    private async Task<bool> RunBuildToolAsync(string buildToolExecutable, string buildToolProject, string commandArgs, string workingDirectory, TimeSpan timeout)
    {
        string toolArgs;
        if (File.Exists(buildToolExecutable))
        {
            toolArgs = $"\"{buildToolExecutable}\" {commandArgs}";
        }
        else if (File.Exists(buildToolProject))
        {
            toolArgs = $"run --project \"{buildToolProject}\" -- {commandArgs}";
        }
        else
        {
            _logService.Error("ArisenBuildTool not found. Neither binary nor project exists.");
            return false;
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = toolArgs,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
        {
            _logService.Error("Failed to start ArisenBuildTool process.");
            return false;
        }

        process.OutputDataReceived += (s, e) => { if (e.Data != null) _logService.Info($"[BuildTool] {e.Data}"); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) _logService.Error($"[BuildTool] {e.Data}"); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new System.Threading.CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logService.Critical($"ArisenBuildTool timed out after {timeout.TotalSeconds:0} seconds.");
            process.Kill(entireProcessTree: true);
            return false;
        }

        if (process.ExitCode != 0)
        {
            _logService.Error($"ArisenBuildTool failed with exit code: {process.ExitCode}");
            return false;
        }

        return true;
    }

    public async Task<bool> LaunchProjectAsync(LauncherProjectMetadata project, EngineInstance engine)
    {
        var result = await LaunchProjectWithResultAsync(project, engine, ProjectLaunchOptions.Interactive);
        return result.Succeeded;
    }

    public async Task<ProjectLaunchResult> LaunchProjectWithResultAsync(
        LauncherProjectMetadata project,
        EngineInstance engine,
        ProjectLaunchOptions options)
    {
        try
        {
            _logService.Info($"Preparing to launch project: {project.Name} in {project.SelectedProfile} mode...");
            string projectDir = Path.GetDirectoryName(project.ProjectPath)!;
            string manifestPath = Path.Combine(projectDir, "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                _logService.Error("manifest.json is missing.");
                return ProjectLaunchResult.Failed("manifest.json is missing.");
            }

            string profile = string.IsNullOrWhiteSpace(project.SelectedProfile) ? "Development" : project.SelectedProfile;
            string config = string.IsNullOrWhiteSpace(project.SelectedConfiguration) ? "Debug" : project.SelectedConfiguration;

            _logService.Info("Using workspace manifest.json for package resolution.");
            var manifest = ManifestJson.DeserializeFile<ProjectManifest>(manifestPath);
            if (manifest == null)
            {
                _logService.Error("Failed to deserialize manifest.json");
                return ProjectLaunchResult.Failed("Failed to deserialize manifest.json.");
            }

            _logService.Info("Restoring remote packages into workspace cache if needed...");
            var resolver = new PackageResolver(_logService);
            await resolver.RestoreManifestPackagesAsync(manifest, profile, projectDir);

            string buildToolExecutable = Path.Combine(engine.InstallPath, "External", "ArisenBuildTool", "bin", "Debug", "net9.0", "ArisenBuildTool.dll");
            if (!File.Exists(buildToolExecutable))
            {
                // Fallback: Check if it's in the engine root (for binary engines)
                string rootExe = Path.Combine(engine.InstallPath, "ArisenBuildTool.dll");
                                if (File.Exists(rootExe)) buildToolExecutable = rootExe;
            }

            string buildToolProject = Path.Combine(engine.InstallPath, "External", "ArisenBuildTool", "ArisenBuildTool.csproj");

            if (!options.BuildBeforeLaunch)
            {
                // Interactive launch keeps the historical quick path: validate/generate, then run an existing build output.
                _logService.Info("Validating workspace package graph with ArisenBuildTool...");
                string validateArgs = $"validate --manifest \"{manifestPath}\" --engine \"{engine.InstallPath}\" --profile \"{profile}\"";
                if (!await RunBuildToolAsync(buildToolExecutable, buildToolProject, validateArgs, engine.InstallPath, TimeSpan.FromSeconds(60)))
                {
                    return ProjectLaunchResult.Failed("ArisenBuildTool validation failed.");
                }

                _logService.Info("Generating workspace files with ArisenBuildTool...");
                string generateArgs = $"generate --manifest \"{manifestPath}\" --engine \"{engine.InstallPath}\" --profile \"{profile}\"";
                if (!await RunBuildToolAsync(buildToolExecutable, buildToolProject, generateArgs, engine.InstallPath, TimeSpan.FromSeconds(60)))
                {
                    return ProjectLaunchResult.Failed("ArisenBuildTool generation failed.");
                }
            }

            if (options.BuildBeforeLaunch)
            {
                string buildScript = Path.Combine(engine.InstallPath, "Scripts", "Windows", "build_workspace.bat");
                if (!File.Exists(buildScript))
                {
                    string message = $"Build script not found: {buildScript}";
                    _logService.Error(message);
                    return ProjectLaunchResult.Failed(message);
                }

                _logService.Info($"Building workspace before launch: {profile} [{config}]...");
                string buildArgs = $"--manifest \"{manifestPath}\" --config \"{config}\" --profile \"{profile}\"";
                var buildResult = await RunProcessCaptureAsync(
                    "cmd.exe",
                    $"/c \"\"{buildScript}\" {buildArgs}\"",
                    engine.InstallPath,
                    options.BuildTimeout ?? TimeSpan.FromMinutes(10));

                LogCapturedProcess("Build", buildResult);
                if (!buildResult.Succeeded)
                {
                    var buildArtifacts = WriteSmokeArtifacts(
                        projectDir,
                        profile,
                        config,
                        options,
                        string.Empty,
                        buildResult.ExitCode,
                        buildResult.StandardOutput,
                        buildResult.StandardError);

                    return ProjectLaunchResult.Failed(
                        $"Workspace build failed with exit code {buildResult.ExitCode}.",
                        buildResult.StandardOutput,
                        buildResult.StandardError,
                        buildResult.ExitCode,
                        artifacts: buildArtifacts);
                }
            }

            // 2. Launch the Workspace Stub natively from generated isolated bin folder
            _logService.Info($"Bootstrapping target: {profile} [{config}]...");

            string binDir = Path.Combine(projectDir, ".arisen", "bin", profile, config);
            string projectExe = Path.Combine(binDir, $"{project.Name}.exe");

            if (!File.Exists(projectExe))
            {
                _logService.Error($"Project executable not found: {projectExe}. Did you build the workspace for this profile/configuration?");
                return ProjectLaunchResult.Failed($"Project executable not found: {projectExe}", executablePath: projectExe);
            }

            string bootArgs = $"--workspace \"{projectDir}\" --profile \"{profile}\"";
            if (options.SmokeMode)
            {
                bootArgs += $" --smoke-mode \"{options.SmokeKind}\" --frames {Math.Max(1, options.SmokeFrames)}";
            }

            var bootPsi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = projectExe,
                Arguments = bootArgs,
                WorkingDirectory = binDir,
                UseShellExecute = false,
                RedirectStandardOutput = options.CaptureOutput,
                RedirectStandardError = options.CaptureOutput,
                CreateNoWindow = options.CaptureOutput
            };

            if (!options.CaptureOutput)
            {
                System.Diagnostics.Process.Start(bootPsi);
                return ProjectLaunchResult.Started(projectExe);
            }

            var launchResult = await RunProcessCaptureAsync(
                bootPsi.FileName,
                bootPsi.Arguments,
                bootPsi.WorkingDirectory ?? binDir,
                options.LaunchTimeout ?? TimeSpan.FromMinutes(2));

            LogCapturedProcess("Runtime", launchResult);
            var runtimeArtifacts = WriteSmokeArtifacts(
                projectDir,
                profile,
                config,
                options,
                projectExe,
                launchResult.ExitCode,
                launchResult.StandardOutput,
                launchResult.StandardError);

            return new ProjectLaunchResult(
                launchResult.Succeeded,
                launchResult.ExitCode,
                launchResult.StandardOutput,
                launchResult.StandardError,
                projectExe,
                false,
                runtimeArtifacts);
        }
        catch (Exception ex)
        {
            _logService.Error($"Critical failure launching project {project.Name}", ex);
            return ProjectLaunchResult.Failed($"Critical failure launching project {project.Name}: {ex.Message}");
        }
    }

    private async Task<CapturedProcessResult> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return new CapturedProcessResult(false, -1, string.Empty, "Failed to start process.");
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CapturedProcessResult(false, -1, await outputTask, $"Process timed out after {timeout.TotalSeconds:0} seconds.");
        }

        string output = await outputTask;
        string error = await errorTask;
        return new CapturedProcessResult(process.ExitCode == 0, process.ExitCode, output, error);
    }

    private void LogCapturedProcess(string label, CapturedProcessResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            _logService.Info($"[{label}:stdout]\n{TrimForLog(result.StandardOutput)}");
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            _logService.Error($"[{label}:stderr]\n{TrimForLog(result.StandardError)}");
        }
    }

    private static string TrimForLog(string text)
    {
        const int maxChars = 12000;
        if (text.Length <= maxChars)
        {
            return text;
        }

        return text.Substring(text.Length - maxChars);
    }

    private static SmokeArtifactPaths WriteSmokeArtifacts(
        string projectDir,
        string profile,
        string config,
        ProjectLaunchOptions options,
        string executablePath,
        int exitCode,
        string standardOutput,
        string standardError)
    {
        string logDir = Path.Combine(projectDir, ".arisen", "Logs");
        Directory.CreateDirectory(logDir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string profileSegment = SanitizeLogFileSegment(profile);
        string configSegment = SanitizeLogFileSegment(config);
        string timestampLogPath = Path.Combine(logDir, $"smoke-{profileSegment}-{configSegment}-{timestamp}.log");
        string latestLogPath = Path.Combine(logDir, $"smoke-{profileSegment}-{configSegment}-latest.log");
        string summaryPath = Path.Combine(logDir, $"smoke-{profileSegment}-{configSegment}-latest.json");

        string logText = BuildSmokeLogText(
            profile,
            config,
            options,
            executablePath,
            exitCode,
            standardOutput,
            standardError);

        File.WriteAllText(timestampLogPath, logText);
        File.WriteAllText(latestLogPath, logText);

        var summary = SmokeRunSummary.Create(
            profile,
            config,
            options,
            executablePath,
            exitCode,
            timestampLogPath,
            latestLogPath,
            standardOutput,
            standardError);
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, SmokeSummaryJsonOptions));

        return new SmokeArtifactPaths(timestampLogPath, latestLogPath, summaryPath);
    }

    private static string BuildSmokeLogText(
        string profile,
        string config,
        ProjectLaunchOptions options,
        string executablePath,
        int exitCode,
        string standardOutput,
        string standardError)
    {
        using var text = new StringWriter();
        text.WriteLine($"Profile: {profile}");
        text.WriteLine($"Configuration: {config}");
        text.WriteLine($"SmokeMode: {options.SmokeMode}");
        text.WriteLine($"SmokeKind: {options.SmokeKind}");
        text.WriteLine($"SmokeFrames: {Math.Max(1, options.SmokeFrames)}");
        text.WriteLine($"Executable: {executablePath}");
        text.WriteLine($"ExitCode: {exitCode}");
        text.WriteLine();

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            text.WriteLine("[stderr]");
            text.WriteLine(standardError.TrimEnd());
            text.WriteLine();
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            text.WriteLine("[stdout]");
            text.WriteLine(standardOutput.TrimEnd());
        }

        return text.ToString();
    }

    private static string SanitizeLogFileSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static readonly JsonSerializerOptions SmokeSummaryJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed record ProjectLaunchOptions(
    bool SmokeMode,
    int SmokeFrames,
    string SmokeKind,
    bool CaptureOutput,
    bool BuildBeforeLaunch,
    TimeSpan? BuildTimeout,
    TimeSpan? LaunchTimeout)
{
    public static ProjectLaunchOptions Interactive { get; } = new(false, 0, "boot", false, false, null, null);

    public static ProjectLaunchOptions Smoke(int frames) => new(
        SmokeMode: true,
        SmokeFrames: Math.Max(1, frames),
        SmokeKind: "scene",
        CaptureOutput: true,
        BuildBeforeLaunch: true,
        BuildTimeout: TimeSpan.FromMinutes(10),
        LaunchTimeout: TimeSpan.FromMinutes(2));
}

public sealed record ProjectLaunchResult(
    bool Succeeded,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    string ExecutablePath,
    bool ProcessStarted,
    SmokeArtifactPaths? Artifacts)
{
    public string LogPath => Artifacts?.TimestampLogPath ?? string.Empty;
    public string LatestLogPath => Artifacts?.LatestLogPath ?? string.Empty;
    public string SummaryPath => Artifacts?.SummaryPath ?? string.Empty;

    public static ProjectLaunchResult Started(string executablePath) => new(true, null, string.Empty, string.Empty, executablePath, true, null);

    public static ProjectLaunchResult Failed(
        string message,
        string standardOutput = "",
        string standardError = "",
        int? exitCode = null,
        string executablePath = "",
        SmokeArtifactPaths? artifacts = null) => new(false, exitCode, standardOutput, string.IsNullOrWhiteSpace(standardError) ? message : standardError, executablePath, false, artifacts);
}

public sealed record SmokeArtifactPaths(string TimestampLogPath, string LatestLogPath, string SummaryPath);

public sealed record SmokeRunSummary(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    string Profile,
    string Configuration,
    bool SmokeMode,
    string SmokeKind,
    int SmokeFrames,
    bool Succeeded,
    int ExitCode,
    string ExecutablePath,
    string TimestampLogPath,
    string LatestLogPath,
    string? RhiBackend,
    IReadOnlyList<string> PackageLoadOrder,
    string? RenderGraphSummary,
    string? RenderEvidence,
    string? Failure)
{
    public static SmokeRunSummary Create(
        string profile,
        string config,
        ProjectLaunchOptions options,
        string executablePath,
        int exitCode,
        string timestampLogPath,
        string latestLogPath,
        string standardOutput,
        string standardError)
    {
        string combined = string.Join(Environment.NewLine, new[] { standardError, standardOutput }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var lines = combined.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var packageIds = new List<string>();
        var renderLines = new List<string>();
        string? rhiBackend = null;
        string? renderGraphSummary = null;

        foreach (string line in lines)
        {
            if (line.Contains("[PackageSubsystem] Loading Package:", StringComparison.Ordinal))
            {
                string packageId = ExtractPackageId(line);
                if (!string.IsNullOrWhiteSpace(packageId))
                {
                    packageIds.Add(packageId);
                }
            }
            else if (line.Contains("[RuntimeRHIWarmupSubsystem] RHI backend initialized:", StringComparison.Ordinal))
            {
                rhiBackend = TrimAfter(line, "RHI backend initialized:");
            }
            else if (line.Contains("[RuntimeRHIWarmupSubsystem] Initializing selected RHI backend:", StringComparison.Ordinal))
            {
                rhiBackend = TrimAfter(line, "Initializing selected RHI backend:");
            }
            else if (line.Contains("[RenderGraph]", StringComparison.Ordinal) &&
                     (line.Contains("Compiled", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("Pass", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("Submit", StringComparison.OrdinalIgnoreCase)))
            {
                renderGraphSummary = StripLogPrefix(line);
            }
            else if (line.Contains("[StaticMeshPass]", StringComparison.Ordinal) ||
                     line.Contains("[GenericRenderPipeline]", StringComparison.Ordinal))
            {
                renderLines.Add(StripLogPrefix(line));
            }
        }

        return new SmokeRunSummary(
            SchemaVersion: 2,
            CapturedAtUtc: DateTimeOffset.UtcNow,
            Profile: profile,
            Configuration: config,
            SmokeMode: options.SmokeMode,
            SmokeKind: options.SmokeKind,
            SmokeFrames: Math.Max(1, options.SmokeFrames),
            Succeeded: exitCode == 0,
            ExitCode: exitCode,
            ExecutablePath: executablePath,
            TimestampLogPath: timestampLogPath,
            LatestLogPath: latestLogPath,
            RhiBackend: string.IsNullOrWhiteSpace(rhiBackend) ? null : rhiBackend,
            PackageLoadOrder: packageIds,
            RenderGraphSummary: string.IsNullOrWhiteSpace(renderGraphSummary) ? null : renderGraphSummary,
            RenderEvidence: renderLines.Count == 0 ? null : renderLines.Last(),
            Failure: exitCode == 0 ? null : FirstFailureLine(lines));
    }

    private static string ExtractPackageId(string line)
    {
        int open = line.LastIndexOf('(');
        int close = line.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return string.Empty;
        }

        return line.Substring(open + 1, close - open - 1).Trim();
    }

    private static string TrimAfter(string line, string marker)
    {
        int index = line.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return string.Empty;
        }

        return line.Substring(index + marker.Length).Trim();
    }

    private static string StripLogPrefix(string line)
    {
        int bracket = line.IndexOf("] ", StringComparison.Ordinal);
        if (bracket >= 0 && bracket + 2 < line.Length)
        {
            return line.Substring(bracket + 2).Trim();
        }

        return line.Trim();
    }

    private static string? FirstFailureLine(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            if (line.Contains("[FATAL]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("exception", StringComparison.OrdinalIgnoreCase))
            {
                return StripLogPrefix(line);
            }
        }

        return null;
    }
}

internal sealed record CapturedProcessResult(bool Succeeded, int ExitCode, string StandardOutput, string StandardError);
