using ArisenEngine.Core.Assets;

namespace Com.Arisen.Rendering.Tests;

internal sealed class TestAssetDatabase : IAssetDatabase
{
    private readonly Dictionary<Guid, AssetRecord> m_Assets = new();
    private readonly Dictionary<(Guid Guid, string Variant), CookedAssetRecord> m_Artifacts = new();
    private readonly Dictionary<int, LoadedCookedAsset> m_Loaded = new();
    private int m_NextHandleIndex;

    public TestAssetDatabase(string cookedRoot)
    {
        CookedRoot = cookedRoot;
    }

    public string CookedRoot { get; }
    public IReadOnlyCollection<AssetRecord> Assets => m_Assets.Values;
    public event Action<AssetChangeEvent>? AssetChanged;

    public void AddAsset(Guid guid, string assetType, string sourcePath, string packageId = "com.arisen.test")
    {
        var metaPath = sourcePath + ".meta";
        if (!File.Exists(metaPath))
        {
            File.WriteAllText(metaPath, $"guid: {guid:D}");
        }

        m_Assets[guid] = new AssetRecord(guid, assetType, sourcePath, metaPath, packageId);
    }

    public bool TryGetAsset(Guid guid, out AssetRecord asset)
    {
        return m_Assets.TryGetValue(guid, out asset!);
    }

    public bool TryGetCookedArtifact(Guid guid, string variant, out CookedAssetRecord artifact)
    {
        return m_Artifacts.TryGetValue((guid, variant), out artifact!);
    }

    public string GetCookedArtifactPath(Guid guid, string variant, string extension)
    {
        Directory.CreateDirectory(CookedRoot);
        var safeVariant = string.Join("_", variant.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(CookedRoot, $"{guid:N}.{safeVariant}{extension}");
    }

    public void RegisterCookedArtifact(CookedAssetRecord artifact)
    {
        m_Artifacts[(artifact.Guid, artifact.Variant)] = artifact;
    }

    public bool TryLoadCookedAsset(Guid guid, string variant, string expectedAssetType, out CookedAssetHandle handle)
    {
        handle = CookedAssetHandle.Invalid;
        if (!m_Artifacts.TryGetValue((guid, variant), out var artifact) ||
            !string.Equals(artifact.AssetType, expectedAssetType, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(artifact.Path))
        {
            return false;
        }

        handle = new CookedAssetHandle(++m_NextHandleIndex, 1, guid, variant);
        m_Loaded[handle.Index] = new LoadedCookedAsset(artifact, File.ReadAllBytes(artifact.Path));
        return true;
    }

    public bool TryGetCookedAssetBytes(CookedAssetHandle handle, out ReadOnlyMemory<byte> bytes)
    {
        if (handle.IsValid && m_Loaded.TryGetValue(handle.Index, out var loaded))
        {
            bytes = loaded.Bytes;
            return true;
        }

        bytes = default;
        return false;
    }

    public ReadOnlyMemory<byte> GetCookedAssetBytes(CookedAssetHandle handle)
    {
        if (TryGetCookedAssetBytes(handle, out var bytes))
        {
            return bytes;
        }

        throw new InvalidOperationException($"Cooked asset handle '{handle}' is not loaded.");
    }

    public void Release(CookedAssetHandle handle)
    {
        if (handle.IsValid)
        {
            m_Loaded.Remove(handle.Index);
        }
    }

    public void ReleaseAllLoadedCookedAssets()
    {
        m_Loaded.Clear();
    }

    public int InvalidateCookedAssets(Guid guid, string? variant = null)
    {
        var loadedHandles = m_Loaded
            .Where(pair => pair.Value.Artifact.Guid == guid &&
                (variant == null || string.Equals(pair.Value.Artifact.Variant, variant, StringComparison.Ordinal)))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var handleIndex in loadedHandles)
        {
            m_Loaded.Remove(handleIndex);
        }

        var keys = m_Artifacts.Keys
            .Where(key => key.Guid == guid && (variant == null || string.Equals(key.Variant, variant, StringComparison.Ordinal)))
            .ToArray();
        foreach (var key in keys)
        {
            m_Artifacts.Remove(key);
        }

        return loadedHandles.Length;
    }

    public void NotifyAssetChanged(AssetChangeEvent change)
    {
        AssetChanged?.Invoke(change);
    }

    public IReadOnlyList<LoadedCookedAssetDiagnostic> GetLoadedCookedAssetDiagnostics()
    {
        return m_Loaded.Values
            .Select(asset => new LoadedCookedAssetDiagnostic(
                asset.Artifact.Guid,
                asset.Artifact.AssetType,
                asset.Artifact.Variant,
                asset.Artifact.Path,
                RefCount: 1,
                asset.Bytes.Length))
            .ToArray();
    }

    private sealed record LoadedCookedAsset(CookedAssetRecord Artifact, byte[] Bytes);
}
