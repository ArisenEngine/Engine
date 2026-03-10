using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using ArisenEditorFramework.Core;

namespace ArisenLauncher.Services;

public class ProjectService
{
    private readonly LogService _logService;
    private readonly ConfigService _configService;

    public ProjectService(LogService logService, ConfigService configService)
    {
        _logService = logService;
        _configService = configService;
    }

    public ProjectMetadata? LoadProject(string projectPath)
    {
        if (!File.Exists(projectPath)) return null;

        try
        {
            string json = File.ReadAllText(projectPath);
            var metadata = JsonSerializer.Deserialize<ProjectMetadata>(json);
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
            var metadata = new ProjectMetadata
            {
                Name = name,
                EngineVersionId = engine.Id,
                ProjectPath = projectFile,
                LastModified = DateTime.Now
            };

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(projectFile, json);
            
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
}
