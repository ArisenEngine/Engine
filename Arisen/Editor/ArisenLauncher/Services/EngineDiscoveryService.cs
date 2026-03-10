using System;
using System.IO;
using System.Linq;

namespace ArisenLauncher.Services;

public class EngineDiscoveryService
{
    private readonly ConfigService m_ConfigService;

    public EngineDiscoveryService(ConfigService configService)
    {
        m_ConfigService = configService;
    }

    public void Discover()
    {
        // 1. Check current directory (for development)
        string baseDir = AppContext.BaseDirectory;
        ValidateAndAdd(baseDir, "Dev Local");

        // 2. Check environment variable
        string? envPath = Environment.GetEnvironmentVariable("ARISEN_INSTALL_ROOT");
        if (!string.IsNullOrEmpty(envPath))
        {
            ValidateAndAdd(envPath, "Env Var");
        }

        // 3. Check common install locations (TODO)
    }

    public bool ValidateAndAdd(string path, string versionLabel)
    {
        if (IsValidEngineFolder(path))
        {
            if (!m_ConfigService.Settings.EngineVersions.Any(e => e.InstallPath == path))
            {
                m_ConfigService.Settings.EngineVersions.Add(new EngineInstance
                {
                    Version = versionLabel,
                    InstallPath = path,
                    IsManual = false
                });
                m_ConfigService.Save();
                return true;
            }
        }
        return false;
    }

    public bool IsValidEngineFolder(string path)
    {
        // Basic validation: Check for key DLLs
        return File.Exists(Path.Combine(path, "ArisenEngine.dll")) || 
               File.Exists(Path.Combine(path, "ArisenEditor.dll"));
    }
}
