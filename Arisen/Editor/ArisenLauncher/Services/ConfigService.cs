using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "ArisenEngine", "launcher_settings.json");

    public LauncherSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (File.Exists(AppDataPath))
        {
            try
            {
                string json = File.ReadAllText(AppDataPath);
                Settings = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new();
            }
            catch
            {
                Settings = new();
            }
        }
    }

    public void Save()
    {
        string dir = Path.GetDirectoryName(AppDataPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppDataPath, json);
    }
}
