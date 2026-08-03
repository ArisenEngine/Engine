using ArisenEngine.Core.Assets;

namespace Com.Arisen.Rendering.Tests;

internal static class SceneTestSource
{
    public static string MigrateLegacy(Guid sceneGuid, string sourcePath, string source)
    {
        var result = ArisenEngine.Resources.Serialization.SceneAssetLoader.MigrateLegacySceneSource(
            sceneGuid,
            sourcePath,
            source);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Diagnostic);
        }

        return result.UpdatedSource;
    }
}

internal sealed class TestAssetDatabase : IAssetDatabase, ICookedArtifactWriteOwner
{
    private readonly Dictionary<Guid, AssetRecord> m_Assets = new();
    private readonly Dictionary<Guid, AssetDescriptor> m_AssetDescriptors = new();
    private readonly Dictionary<(Guid Guid, string Variant), CookedAssetRecord> m_Artifacts = new();
    private readonly Dictionary<int, LoadedCookedAsset> m_Loaded = new();
    private int m_NextHandleIndex;
    private long m_CookedPublicationGeneration;

    public TestAssetDatabase(
        AssetSourceAccessMode sourceAccessMode,
        string cookedRoot)
    {
        SourceAccessMode = sourceAccessMode;
        CookedRoot = cookedRoot;
    }

    public string CookedRoot { get; }
    public AssetDatabaseMode Mode { get; private set; } = AssetDatabaseMode.Workspace;
    public bool IsReadOnlyRuntime => Mode == AssetDatabaseMode.ReadOnlyRuntime;
    public AssetSourceAccessMode SourceAccessMode { get; private set; }
    public bool CanReadSourceAssets => SourceAccessMode != AssetSourceAccessMode.Disabled;
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
        m_AssetDescriptors[guid] = new AssetDescriptor(guid, assetType, packageId);
    }

    public void UseReadOnlyRuntime()
    {
        ReleaseAllLoadedCookedAssets();
        m_Assets.Clear();
        Mode = AssetDatabaseMode.ReadOnlyRuntime;
        SourceAccessMode = AssetSourceAccessMode.Disabled;
    }

    public void UseSourceAccess(AssetSourceAccessMode sourceAccessMode)
    {
        if (Mode == AssetDatabaseMode.ReadOnlyRuntime)
        {
            throw new InvalidOperationException(
                "The test asset database cannot enable source access after a runtime catalog mount.");
        }

        SourceAccessMode = sourceAccessMode;
    }

    public bool TryGetAsset(Guid guid, out AssetRecord asset)
    {
        if (CanReadSourceAssets)
        {
            return m_Assets.TryGetValue(guid, out asset!);
        }

        asset = null!;
        return false;
    }

    public bool TryGetAssetDescriptor(Guid guid, out AssetDescriptor asset)
    {
        return m_AssetDescriptors.TryGetValue(guid, out asset);
    }

    public bool TryGetCookedArtifact(Guid guid, string variant, out CookedAssetRecord artifact)
    {
        return m_Artifacts.TryGetValue((guid, variant), out artifact!);
    }

    public CookedArtifactWrite BeginCookedArtifactWrite(
        Guid guid,
        string variant,
        string extension)
    {
        EnsureMutable();
        if (guid == Guid.Empty || string.IsNullOrWhiteSpace(variant))
        {
            throw new ArgumentException("Cooked artifact identity cannot be empty.");
        }

        string normalizedExtension = extension.StartsWith('.') ? extension : "." + extension;
        Guid transactionId = Guid.NewGuid();
        string cookedRoot = Path.GetFullPath(CookedRoot);
        string transactionRoot = Path.Combine(
            cookedRoot,
            ".staging",
            transactionId.ToString("N"));
        Directory.CreateDirectory(transactionRoot);
        return new CookedArtifactWrite(
            this,
            transactionId,
            guid,
            variant,
            normalizedExtension,
            cookedRoot,
            Path.Combine(transactionRoot, "artifact" + normalizedExtension));
    }

    CookedAssetRecord ICookedArtifactWriteOwner.CommitCookedArtifactWrite(
        CookedArtifactWrite write,
        string assetType)
    {
        EnsureMutable();
        if (!File.Exists(write.OutputPath))
        {
            throw new FileNotFoundException("The staged cooked artifact is missing.", write.OutputPath);
        }

        long generation = ++m_CookedPublicationGeneration;
        string finalDirectory = Path.Combine(CookedRoot, write.Guid.ToString("N"));
        Directory.CreateDirectory(finalDirectory);
        string finalPath = Path.Combine(
            finalDirectory,
            $"{write.Variant}.g{generation:D20}.{write.TransactionId:N}{write.Extension}");
        File.Move(write.OutputPath, finalPath);
        var output = new FileInfo(finalPath);
        var artifact = new CookedAssetRecord(
            write.Guid,
            assetType,
            write.Variant,
            output.FullName,
            output.Length,
            output.LastWriteTimeUtc);
        RegisterCookedArtifact(artifact);
        return artifact;
    }

    void ICookedArtifactWriteOwner.DiscardCookedArtifactWrite(CookedArtifactWrite write)
    {
        if (File.Exists(write.OutputPath))
        {
            File.Delete(write.OutputPath);
        }

        string? transactionRoot = Path.GetDirectoryName(write.OutputPath);
        if (!string.IsNullOrWhiteSpace(transactionRoot) && Directory.Exists(transactionRoot))
        {
            Directory.Delete(transactionRoot, recursive: true);
        }
    }

    public void RegisterCookedArtifact(CookedAssetRecord artifact)
    {
        EnsureMutable();
        m_Artifacts[(artifact.Guid, artifact.Variant)] = artifact;
    }

    public bool TryLoadCookedAsset(Guid guid, string variant, string expectedAssetType, out CookedAssetHandle handle)
    {
        handle = CookedAssetHandle.Invalid;
        if (!m_AssetDescriptors.TryGetValue(guid, out AssetDescriptor descriptor) ||
            !m_Artifacts.TryGetValue((guid, variant), out var artifact) ||
            !string.Equals(descriptor.AssetType, expectedAssetType, StringComparison.OrdinalIgnoreCase) ||
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
        EnsureMutable();
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

    public int RemoveCookedArtifacts(IReadOnlyCollection<CookedAssetIdentity> identities)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(identities);
        int removedCount = 0;
        foreach (CookedAssetIdentity identity in identities.Distinct())
        {
            if (!m_Artifacts.Remove(
                    (identity.Guid, identity.Variant),
                    out CookedAssetRecord? artifact))
            {
                continue;
            }

            int[] loadedHandles = m_Loaded
                .Where(pair => pair.Value.Artifact.Guid == identity.Guid &&
                    string.Equals(
                        pair.Value.Artifact.Variant,
                        identity.Variant,
                        StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();
            foreach (int handleIndex in loadedHandles)
            {
                m_Loaded.Remove(handleIndex);
            }

            if (File.Exists(artifact.Path))
            {
                File.Delete(artifact.Path);
            }

            removedCount++;
        }

        return removedCount;
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

    private void EnsureMutable()
    {
        if (IsReadOnlyRuntime)
        {
            throw new InvalidOperationException("The test asset database is mounted read-only.");
        }
    }

    private sealed record LoadedCookedAsset(CookedAssetRecord Artifact, byte[] Bytes);
}
