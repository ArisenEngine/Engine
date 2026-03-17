using System;
using System.IO;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEditor.Core.Services;

/// <summary>
/// A compatibility wrapper that interfaces with the new SQLite AssetDatabase and AssetImporter.
/// This replaces the old memory-dictionary based AssetDatabaseService.
/// </summary>
public class AssetDatabaseService : IDisposable
{
    private static AssetDatabaseService? _instance;
    public static AssetDatabaseService Instance => _instance ??= new AssetDatabaseService();

    private ArisenEditor.Core.Assets.AssetImporter? _importer;
    private string m_AssetsRoot = string.Empty;

    private AssetDatabaseService() { }

    public void Initialize(string projectRoot)
    {
        m_AssetsRoot = Path.Combine(projectRoot, "Content");
        
        // Initialize SQLite DB in the project's Library folder
        string libraryPath = Path.Combine(projectRoot, "Library");
        string dbPath = Path.Combine(libraryPath, "AssetRegistry.db");
        
        ArisenEditor.Core.Assets.AssetDatabase.Initialize(dbPath);

        // Start the importer to scan and watch for changes
        _importer = new ArisenEditor.Core.Assets.AssetImporter(m_AssetsRoot);
        _importer.Start();
    }

    public string? GetPathFromGuid(Guid guid)
    {
        return ArisenEditor.Core.Assets.AssetDatabase.Instance.GetPath(guid);
    }

    public Guid GetGuidFromPath(string path)
    {
        string relativePath = Path.GetRelativePath(m_AssetsRoot, path).Replace('\\', '/');
        if (ArisenEditor.Core.Assets.AssetDatabase.Instance.TryGetGuid(relativePath, out var guid))
        {
            return guid;
        }
        return Guid.Empty;
    }

    public string GetAssetsRoot() => m_AssetsRoot;

    public void Dispose()
    {
        _importer?.Dispose();
        ArisenEditor.Core.Assets.AssetDatabase.Instance?.Dispose();
    }
}
