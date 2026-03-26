using System;
using System.IO;
using System.Linq;

namespace ArisenLauncher.Services;

public class EngineDiscoveryService
{
    private readonly ConfigService m_ConfigService;
    private readonly ILogService m_LogService;

    public EngineDiscoveryService(ConfigService configService, ILogService logService)
    {
        m_ConfigService = configService;
        m_LogService = logService;
    }

    public void Discover()
    {
        m_LogService.Info("Starting engine discovery...");
        // 1. Check current directory (for development)
        string baseDir = AppContext.BaseDirectory;
        if (!ValidateAndAdd(baseDir, "Dev Local", isManual: false))
        {
            // If we are in a build output folder, climb up to find the engine root
            DirectoryInfo? dir = new DirectoryInfo(baseDir);
            while (dir != null)
            {
                if (IsValidEngineFolder(dir.FullName))
                {
                    ValidateAndAdd(dir.FullName, "Dev Local (Root)", isManual: false);
                    break;
                }
                
                // Special check for repo-style layout: Engine/Arisen
                string engineRootCandidate = Path.Combine(dir.FullName, "Engine", "Arisen");
                if (IsValidEngineFolder(engineRootCandidate))
                {
                    ValidateAndAdd(engineRootCandidate, "Dev Local (Source)", isManual: false);
                    break;
                }

                dir = dir.Parent;
            }
        }

        // 2. Check environment variable
        string? envPath = Environment.GetEnvironmentVariable("ARISEN_ENGINE_INSTALL_ROOT");
        if (!string.IsNullOrEmpty(envPath))
        {
            ValidateAndAdd(envPath, "Env Var", isManual: false);
        }
    }

    public bool ValidateAndAdd(string path, string versionLabel, bool isManual)
    {
        try
        {
            string normalizedPath = Path.GetFullPath(path).TrimEnd('\\', '/');
            
            if (IsValidEngineFolder(normalizedPath))
            {
                var existing = m_ConfigService.Settings.EngineVersions.FirstOrDefault(e => 
                    string.Equals(Path.GetFullPath(e.InstallPath).TrimEnd('\\', '/'), normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    m_LogService.Info($"Adding new engine: {versionLabel} at {normalizedPath}");
                    m_ConfigService.Settings.EngineVersions.Add(new EngineInstance
                    {
                        Version = versionLabel,
                        InstallPath = normalizedPath,
                        IsManual = isManual
                    });
                    m_ConfigService.Save();
                    return true;
                }
                else
                {
                    // If it already exists, update its status if necessary
                    if (isManual && !existing.IsManual)
                    {
                        m_LogService.Info($"Upgrading discovered engine to manual: {normalizedPath}");
                        existing.IsManual = true;
                        existing.Version = versionLabel; // Update "Dev Local" to "Manual" or etc.
                        m_ConfigService.Save();
                        return true;
                    }
                    return false; // Already exists and is already manual or just discovered
                }
            }
        }
        catch (Exception ex)
        {
            m_LogService.Error($"Error validating engine path: {path}", ex);
        }
        return false;
    }

    public bool IsValidEngineFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return false;

        var validationConfig = m_ConfigService.Settings.EngineValidation;
        if (validationConfig == null || validationConfig.RequiredFiles == null) 
            return false;

        foreach (var file in validationConfig.RequiredFiles)
        {
            if (!File.Exists(Path.Combine(path, file)))
            {
                return false;
            }
        }

        return true;
    }
}
