using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
        try
        {
            _logService.Info($"Preparing to launch project: {project.Name} in {project.SelectedProfile} mode...");
            string projectDir = Path.GetDirectoryName(project.ProjectPath)!;
            string manifestPath = Path.Combine(projectDir, "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                _logService.Error("manifest.json is missing.");
                return false;
            }

            string profile = string.IsNullOrWhiteSpace(project.SelectedProfile) ? "Development" : project.SelectedProfile;
            string config = string.IsNullOrWhiteSpace(project.SelectedConfiguration) ? "Debug" : project.SelectedConfiguration;

            _logService.Info("Using workspace manifest.json for package resolution.");
            var manifest = ManifestJson.DeserializeFile<ProjectManifest>(manifestPath);
            if (manifest == null)
            {
                _logService.Error("Failed to deserialize manifest.json");
                return false;
            }

            _logService.Info("Restoring remote packages into workspace cache if needed...");
            var resolver = new PackageResolver(_logService);
            await resolver.RestoreManifestPackagesAsync(manifest, profile, projectDir);

            // 1. Execute ArisenBuildTool validation + out-of-source generation
            string buildToolExecutable = Path.Combine(engine.InstallPath, "External", "ArisenBuildTool", "bin", "Debug", "net9.0", "ArisenBuildTool.dll");
            if (!File.Exists(buildToolExecutable))
            {
                // Fallback: Check if it's in the engine root (for binary engines)
                string rootExe = Path.Combine(engine.InstallPath, "ArisenBuildTool.dll");
                                if (File.Exists(rootExe)) buildToolExecutable = rootExe;
            }

            string buildToolProject = Path.Combine(engine.InstallPath, "External", "ArisenBuildTool", "ArisenBuildTool.csproj");

            _logService.Info("Validating workspace package graph with ArisenBuildTool...");
            string validateArgs = $"validate --manifest \"{manifestPath}\" --engine \"{engine.InstallPath}\" --profile \"{profile}\"";
            if (!await RunBuildToolAsync(buildToolExecutable, buildToolProject, validateArgs, engine.InstallPath, TimeSpan.FromSeconds(60)))
            {
                return false;
            }

            _logService.Info("Generating workspace files with ArisenBuildTool...");
            string generateArgs = $"generate --manifest \"{manifestPath}\" --engine \"{engine.InstallPath}\" --profile \"{profile}\"";
            if (!await RunBuildToolAsync(buildToolExecutable, buildToolProject, generateArgs, engine.InstallPath, TimeSpan.FromSeconds(60)))
            {
                return false;
            }

            // 2. Launch the Workspace Stub natively from generated isolated bin folder
            _logService.Info($"Bootstrapping target: {profile} [{config}]...");

            string binDir = Path.Combine(projectDir, ".arisen", "bin", profile, config);
            string projectExe = Path.Combine(binDir, $"{project.Name}.exe");

            if (!File.Exists(projectExe))
            {
                _logService.Error($"Project executable not found: {projectExe}. Did you build the workspace for this profile/configuration?");
                return false;
            }

            var bootPsi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = projectExe,
                Arguments = $"--workspace \"{projectDir}\" --profile \"{profile}\"",
                WorkingDirectory = binDir,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                CreateNoWindow = false
            };
            
            System.Diagnostics.Process.Start(bootPsi);
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error($"Critical failure launching project {project.Name}", ex);
            return false;
        }
    }
}
