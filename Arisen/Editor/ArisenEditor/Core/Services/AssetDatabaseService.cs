using System;
using System.Collections.Generic;
using System.IO;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEditor.Core.Services;

/// <summary>
/// Manages the project's asset database, providing GUID-based tracking and metadata generation.
/// </summary>
public class AssetDatabaseService
{
    private static AssetDatabaseService? _instance;
    public static AssetDatabaseService Instance => _instance ??= new AssetDatabaseService();

    private readonly Dictionary<Guid, string> m_GuidToPath = new();
    private readonly Dictionary<string, Guid> m_PathToGuid = new();
    private string m_AssetsRoot = string.Empty;

    private AssetDatabaseService() { }

    public void Initialize()
    {
        m_AssetsRoot = Path.Combine(ArisenApplication.s_ProjectRoot, "Content");
        if (!Directory.Exists(m_AssetsRoot))
        {
            Directory.CreateDirectory(m_AssetsRoot);
        }

        Refresh();
    }

    public void Refresh()
    {
        using var _ = Profiler.Zone("AssetDatabaseService.Refresh");
        
        m_GuidToPath.Clear();
        m_PathToGuid.Clear();

        if (string.IsNullOrEmpty(m_AssetsRoot) || !Directory.Exists(m_AssetsRoot))
            return;

        ScanDirectory(m_AssetsRoot);
        Logger.Log($"[AssetDatabase] Refresh complete. Indexed {m_GuidToPath.Count} assets.");
    }

    private void ScanDirectory(string path)
    {
        var files = Directory.GetFiles(path);
        foreach (var file in files)
        {
            if (file.EndsWith(".meta")) continue;

            ProcessAsset(file);
        }

        var subDirs = Directory.GetDirectories(path);
        foreach (var dir in subDirs)
        {
            ScanDirectory(dir);
        }
    }

    private void ProcessAsset(string assetPath)
    {
        string metaPath = assetPath + ".meta";
        AssetMetadata metadata;

        if (File.Exists(metaPath))
        {
            try
            {
                metadata = SerializationUtil.Deserialize<AssetMetadata>(metaPath);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[AssetDatabase] Failed to deserialize meta for {assetPath}: {ex.Message}. Regenerating.");
                metadata = CreateNewMetadata(assetPath, metaPath);
            }
        }
        else
        {
            metadata = CreateNewMetadata(assetPath, metaPath);
        }

        RegisterAsset(assetPath, metadata.Guid);
    }

    private AssetMetadata CreateNewMetadata(string assetPath, string metaPath)
    {
        var metadata = new AssetMetadata
        {
            Guid = Guid.NewGuid(),
            AssetType = Path.GetExtension(assetPath)
        };
        SerializationUtil.Serialize(metadata, metaPath);
        return metadata;
    }

    private void RegisterAsset(string path, Guid guid)
    {
        m_GuidToPath[guid] = path;
        m_PathToGuid[path] = guid;
    }

    public string? GetPathFromGuid(Guid guid)
    {
        return m_GuidToPath.GetValueOrDefault(guid);
    }

    public Guid GetGuidFromPath(string path)
    {
        return m_PathToGuid.GetValueOrDefault(path, Guid.Empty);
    }

    public string GetAssetsRoot() => m_AssetsRoot;
}
