using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RuntimeAssetResidencyTests : IDisposable
{
    private const string PackageId = "com.arisen.test";
    private static readonly Guid s_WorldGuid =
        Guid.Parse("84000000-0000-0000-0000-000000000001");
    private readonly string m_Root;
    private readonly TestAssetDatabase m_Database;

    public RuntimeAssetResidencyTests()
    {
        m_Root = Path.Combine(
            Path.GetTempPath(),
            "ArisenRuntimeAssetResidencyTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(m_Root);
        m_Database = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(m_Root, "Cooked"));
    }

    [Fact]
    public void SharedOwnersRetainOneCpuAndGpuResourceUntilFinalRelease()
    {
        Guid meshGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 256);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 1024);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        CookedSceneDependency[] dependencies = [Dependency(meshGuid, "Mesh", required: true)];

        using RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1), dependencies, pinned: false);
        using RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, generation: 1), dependencies, pinned: false);
        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(RuntimePreparedAssetState.Ready, second.State);
        Assert.Equal(1, provider.PrepareCount);
        Assert.Single(m_Database.GetLoadedCookedAssetDiagnostics());
        Assert.Equal(2, residency.GetResources().Single().OwnerCount);

        first.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Single(residency.GetResources());
        Assert.Equal(1, residency.GetResources().Single().OwnerCount);
        Assert.Equal(0, provider.ReleaseCount);

        second.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Empty(residency.GetResources());
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
        Assert.Equal(1, provider.ReleaseCount);
    }

    [Fact]
    public void SetupBudgetKeepsOwnerWaitingUntilEveryRequiredResourceIsReady()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 128);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 512);
        using var residency = CreateResidency(maxSetupsPerFrame: 1, maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, generation: 1),
            [Dependency(firstGuid, "Mesh", true), Dependency(secondGuid, "Mesh", true)],
            pinned: false);

        residency.ProcessAtFrameBoundary();
        Assert.Equal(RuntimePreparedAssetState.Waiting, lease.State);
        Assert.Equal(1, residency.GetMetrics().WaitingAssetCount);
        Assert.Equal(1, residency.GetMetrics().ReadyAssetCount);

        residency.ProcessAtFrameBoundary();
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(2, provider.PrepareCount);
    }

    [Fact]
    public void MissingRequiredCookedDependencyFailsBeforeOwnerIsPublished()
    {
        Guid meshGuid = Guid.Parse("84000000-0000-0000-0000-000000000101");
        string source = Path.Combine(m_Root, "Missing.obj");
        File.WriteAllText(source, "# missing cooked artifact");
        m_Database.AddAsset(meshGuid, "Mesh", source, PackageId);
        m_Database.UseReadOnlyRuntime();
        using var residency = CreateResidency();

        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(1, generation: 1),
                [Dependency(meshGuid, "Mesh", required: true)],
                pinned: false));

        Assert.Contains("source fallback is disabled", failure.Message);
        Assert.Equal(0, residency.GetMetrics().ActiveOwnerCount);
        Assert.Empty(residency.GetResources());
    }

    [Fact]
    public void InactiveEvictionUsesDeterministicLeastRecentlyNeededOrder()
    {
        Guid firstGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        Guid secondGuid = AddCookedAsset("Mesh", RuntimeAssetVariantPolicy.StaticMesh, 64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakePreparedProvider("Mesh", gpuBytes: 64);
        using var residency = CreateResidency(maxInactiveResources: 1);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            CellOwner(1, 1), [Dependency(firstGuid, "Mesh", true)], pinned: false);
        RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            CellOwner(2, 1), [Dependency(secondGuid, "Mesh", true)], pinned: false);
        residency.ProcessAtFrameBoundary();
        first.Dispose();
        second.Dispose();

        residency.ProcessAtFrameBoundary();

        RuntimeAssetResidencySnapshot remaining = Assert.Single(residency.GetResources());
        Assert.Equal(secondGuid, remaining.Key.Guid);
        Assert.Equal(1, provider.ReleaseCount);
        Assert.Equal(1, residency.GetMetrics().EvictionCount);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(m_Root)) Directory.Delete(m_Root, recursive: true);
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private RuntimeAssetResidencyService CreateResidency(
        int maxSetupsPerFrame = 4,
        int maxInactiveResources = 0)
    {
        return new RuntimeAssetResidencyService(
            m_Database,
            new RuntimeAssetResidencyBudgets(
                MaxCpuCookedBytes: 1024 * 1024,
                MaxPreparedGpuBytes: 1024 * 1024,
                MaxSetupsPerFrame: maxSetupsPerFrame,
                MaxSetupMilliseconds: 100,
                MaxInactiveResources: maxInactiveResources));
    }

    private Guid AddCookedAsset(string assetType, string variant, int byteCount)
    {
        Guid guid = Guid.NewGuid();
        string source = Path.Combine(m_Root, guid.ToString("N") + ".source");
        string cooked = Path.Combine(m_Root, guid.ToString("N") + ".cooked");
        File.WriteAllText(source, "source");
        File.WriteAllBytes(cooked, Enumerable.Range(0, byteCount).Select(index => (byte)index).ToArray());
        m_Database.AddAsset(guid, assetType, source, PackageId);
        m_Database.RegisterCookedArtifact(new CookedAssetRecord(
            guid,
            assetType,
            variant,
            cooked,
            byteCount,
            File.GetLastWriteTimeUtc(cooked)));
        return guid;
    }

    private static CookedSceneDependency Dependency(Guid guid, string assetType, bool required) =>
        new(guid, PackageId, assetType, required);

    private static RuntimeAssetResidencyOwnerId CellOwner(int cell, long generation) =>
        RuntimeAssetResidencyOwnerId.Cell(
            s_WorldGuid,
            new WorldCellId(new Guid($"84100000-0000-0000-0000-{cell:D12}")),
            generation);

    private sealed class FakePreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly string m_AssetType;
        private readonly long m_GpuBytes;
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public FakePreparedProvider(string assetType, long gpuBytes)
        {
            m_AssetType = assetType;
            m_GpuBytes = gpuBytes;
        }

        public string ProviderId => "test.prepared-assets";
        public int PrepareCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public bool Supports(string assetType) => assetType == m_AssetType;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            PrepareCount++;
            m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(m_GpuBytes);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            if (m_Prepared.Remove(key)) ReleaseCount++;
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() =>
            new(m_Prepared.Count, m_Prepared.Count * m_GpuBytes, PendingDisposalCount: 0);
    }
}
