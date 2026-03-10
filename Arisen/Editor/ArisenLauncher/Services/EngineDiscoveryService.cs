using System;
using System.IO;
using System.Linq;

namespace ArisenLauncher.Services;

public class EngineDiscoveryService
{
    private readonly ConfigService m_ConfigService;
    private readonly LogService m_LogService;

    public EngineDiscoveryService(ConfigService configService, LogService logService)
    {
        m_ConfigService = configService;
        m_LogService = logService;
    }

    public void Discover()
    {
        m_LogService.Info("Starting engine discovery...");
        // 1. Check current directory (for development)
        string baseDir = AppContext.BaseDirectory;
        ValidateAndAdd(baseDir, "Dev Local", isManual: false);

        // 2. Check environment variable
        string? envPath = Environment.GetEnvironmentVariable("ARISEN_INSTALL_ROOT");
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

        // Basic validation: Check for key DLLs or EXEs
        return File.Exists(Path.Combine(path, "ArisenEngine.dll")) || 
               File.Exists(Path.Combine(path, "ArisenEditor.dll")) ||
               File.Exists(Path.Combine(path, "ArisenEditor.Desktop.exe"));
    }
}
