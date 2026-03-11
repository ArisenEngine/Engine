using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ArisenEditorFramework.Services;

namespace ArisenLauncher.Services;

public class LauncherSettings
{
    public List<EngineInstance> EngineVersions { get; set; } = new();
    public List<string> RecentProjects { get; set; } = new();
    public string LastUsedEngineId { get; set; } = string.Empty;
}

public class EngineInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Version { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public bool IsManual { get; set; }
}

public class ConfigService
{
    private readonly ILogService _logService;
    private readonly string _settingsPath;

    public LauncherSettings Settings { get; private set; } = new();

    public ConfigService(ILogService logService)
    {
        _logService = logService;
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "launcher_settings.json");
        Load();
    }

    public void Load()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                string json = File.ReadAllText(_settingsPath);
                Settings = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new();
                _logService.Info($"Configuration loaded from {_settingsPath}");
            }
            catch (Exception ex)
            {
                _logService.Error("Failed to load launcher settings.", ex);
                Settings = new();
            }
        }
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(_settingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            _logService.Info($"Configuration saved to {_settingsPath}");
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to save launcher settings.", ex);
        }
    }
}
