using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ArisenEditor.Core.Assets;

/// <summary>
/// A lightweight SQLite database used to index source assets and their Guids.
/// This prevents the editor from needing to parse all .meta files on startup.
/// </summary>
public class AssetDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    public static AssetDatabase Instance { get; private set; } = null!;

    public static void Initialize(string dbPath)
    {
        Instance = new AssetDatabase(dbPath);
    }

    private AssetDatabase(string dbPath)
    {
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();

        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Assets (
                Guid TEXT PRIMARY KEY,
                Path TEXT NOT NULL UNIQUE,
                Type TEXT,
                LastModified INTEGER NOT NULL
            );
            
            -- Index for quick path lookups
            CREATE INDEX IF NOT EXISTS idx_assets_path ON Assets(Path);
        ";
        command.ExecuteNonQuery();
    }

    public void RegisterAsset(Guid guid, string path, string type, long lastModifiedTimeUtc)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Assets (Guid, Path, Type, LastModified)
            VALUES ($guid, $path, $type, $lastModified)
            ON CONFLICT(Guid) DO UPDATE SET
                Path=excluded.Path,
                Type=excluded.Type,
                LastModified=excluded.LastModified;
        ";
        command.Parameters.AddWithValue("$guid", guid.ToString());
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$lastModified", lastModifiedTimeUtc);
        
        command.ExecuteNonQuery();
    }

    public bool TryGetGuid(string path, out Guid guid)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Guid FROM Assets WHERE Path = $path LIMIT 1";
        command.Parameters.AddWithValue("$path", path);

        var result = command.ExecuteScalar();
        if (result != null && Guid.TryParse(result.ToString(), out var g))
        {
            guid = g;
            return true;
        }

        guid = Guid.Empty;
        return false;
    }

    public string? GetPath(Guid guid)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Path FROM Assets WHERE Guid = $guid LIMIT 1";
        command.Parameters.AddWithValue("$guid", guid.ToString());

        var result = command.ExecuteScalar();
        return result?.ToString();
    }
    
    public void RemoveAsset(Guid guid)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM Assets WHERE Guid = $guid";
        command.Parameters.AddWithValue("$guid", guid.ToString());
        command.ExecuteNonQuery();
    }
    
    public void RemoveAssetByPath(string path)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM Assets WHERE Path = $path";
        command.Parameters.AddWithValue("$path", path);
        command.ExecuteNonQuery();
    }

    public IEnumerable<(Guid Guid, string Path, string Type)> GetAllAssets()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT Guid, Path, Type FROM Assets";
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (Guid.TryParse(reader.GetString(0), out var g))
            {
                 yield return (g, reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2));
            }
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
