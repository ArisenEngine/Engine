using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArisenLauncher.Services;

public class ProjectMetadata
{
    public string Name { get; set; } = "New Project";
    public string EngineVersionId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public string ProjectPath { get; set; } = string.Empty; // Full path to .arisenproj
}

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

    public bool CreateProject(string folderPath, string name, EngineInstance engine)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
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
}
