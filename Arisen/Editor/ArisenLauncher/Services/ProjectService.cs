using System;
using System.Collections.Generic;
using System.IO;
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
                return metadata;
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to load project at {projectPath}", ex);
        }
        return null;
    }

    public bool CreateProject(string folderPath, string name, EngineInstance engine, string? templateName = null)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Copy template files if specified
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
                EngineVersionId = engine.Id,
                ProjectPath = projectFile,
                LastModified = DateTime.Now
            };

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(projectFile, json);
            
            // Generate Project constraints
            Directory.CreateDirectory(Path.Combine(folderPath, "Local"));
            Directory.CreateDirectory(Path.Combine(folderPath, ".Cache"));
            Directory.CreateDirectory(Path.Combine(folderPath, "Assets"));

            // Generate manifest.json
            string manifestFile = Path.Combine(folderPath, "manifest.json");
            var manifest = new ProjectManifest
            {
                Name = name,
                Packages = new List<PackageRequirement>()
            };
            string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestFile, manifestJson);
            
            // Generate .gitignore
            string gitignoreFile = Path.Combine(folderPath, ".gitignore");
            string gitignoreContent = """
            # Arisen Engine Generated Folders
            .Cache/
            Projects/
            Logs/
            Outputs/
            *.user
            """;
            File.WriteAllText(gitignoreFile, gitignoreContent);

            // Add to recent projects in config
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

    public async Task<bool> LaunchProjectAsync(LauncherProjectMetadata project, EngineInstance engine, string entryMode = "development")
    {
        try
        {
            _logService.Info($"Preparing to launch project: {project.Name} in {entryMode} mode...");
            string projectDir = Path.GetDirectoryName(project.ProjectPath)!;
            string manifestPath = Path.Combine(projectDir, "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                _logService.Error("manifest.json is missing.");
                return false;
            }

            var localDir = Path.Combine(projectDir, "Local");
            var cacheDir = Path.Combine(projectDir, ".Cache");
            var projectsDir = Path.Combine(projectDir, "Projects");
            Directory.CreateDirectory(localDir);
            Directory.CreateDirectory(cacheDir);

            // 1. Package Management Resolution
            _logService.Info("Resolving packages via manifest.json...");
            string json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<ProjectManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest == null) 
            {
                _logService.Error("Failed to deserialize manifest.json");
                return false;
            }

            var resolver = new PackageResolver(_logService);

            foreach (var req in manifest.Packages)
            {
                if (string.IsNullOrEmpty(req.Url)) continue;

                if (req.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    string sourcePath = Uri.UnescapeDataString(new Uri(req.Url).LocalPath);
                    string targetPath = Path.Combine(localDir, req.Id);
                    
                    // Import if it doesn't exist locally
                    if (!Directory.Exists(targetPath))
                    {
                        if (Directory.Exists(sourcePath))
                        {
                            _logService.Info($"Importing local package '{req.Id}' from {sourcePath} to {targetPath}");
                            CopyDirectory(sourcePath, targetPath, true);
                        }
                        else 
                        {
                            _logService.Warning($"Package '{req.Id}' could not be imported. Directory not found: {sourcePath}");
                        }
                    }

                    // Rewrite URL to target the now imported/existent directory
                    req.Url = new Uri(targetPath).AbsoluteUri;
                }
                else if (req.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    _logService.Info($"Downloading remote package '{req.Id}' into .Cache...");
                    string resolvedPath = await resolver.ResolveAsync(req.Id, req.Url, cacheDir);
                    req.Url = new Uri(resolvedPath).AbsoluteUri;
                }
            }

            // Generate synthetic resolved manifest for ArisenBuildTool mapping
            string resolvedManifestPath = Path.Combine(projectDir, "manifest.resolved.json");
            File.WriteAllText(resolvedManifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            // 2. Execute ArisenBuildTool Out-of-Source Generation
            string buildToolExecutable = Path.Combine(engine.InstallPath, "External", "ArisenBuildTool", "bin", "Debug", "net9.0", "ArisenBuildTool.dll");
            string toolArgs = "";
            string buildToolProject = Path.Combine(engine.InstallPath, "External", "ArisenBuildTool", "ArisenBuildTool.csproj");
            if (File.Exists(buildToolExecutable))
            {
                toolArgs = $"\"{buildToolExecutable}\" --manifest \"{resolvedManifestPath}\" --engine \"{engine.InstallPath}\"";
            }
            else
            {
                toolArgs = $"run --project \"{buildToolProject}\" -- --manifest \"{resolvedManifestPath}\" --engine \"{engine.InstallPath}\"";
            }

            _logService.Info($"Running ArisenBuildTool...");
            
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = toolArgs,
                WorkingDirectory = engine.InstallPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            
            using (var process = System.Diagnostics.Process.Start(psi))
            {
                if (process != null)
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) _logService.Info($"[BuildTool] {e.Data}"); };
                    process.BeginOutputReadLine();
                    await process.WaitForExitAsync();
                    if (process.ExitCode != 0)
                    {
                        _logService.Error($"ArisenBuildTool failed with exit code: {process.ExitCode}");
                        return false;
                    }
                }
            }

            // 3. Launch Editor or Application Host natively from generated Out-Of-Source Solution
            _logService.Info($"Bootstrapping target: {entryMode}...");

            System.Diagnostics.ProcessStartInfo bootPsi;

            if (entryMode == "standalone")
            {
                string hostProject = Path.Combine(projectsDir, $"{project.Name}.csproj");
                bootPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{hostProject}\"",
                    WorkingDirectory = projectsDir, // Run from projects dir
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = false
                };
            }
            else // "development"
            {
                string editorProject = Path.Combine(engine.InstallPath, "Editor", "ArisenEditor", "ArisenEditor.csproj");
                bootPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{editorProject}\" --project-path \"{projectDir}\" --entry {entryMode}",
                    WorkingDirectory = engine.InstallPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = false
                };
            }
            
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
