using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class TerrainResidencyCoordinationTests : IDisposable
{
    private const string PackageId = "com.arisen.test";
    private static readonly Guid s_WorldGuid =
        Guid.Parse("8a000000-0000-0000-0000-000000000001");
    private readonly string m_Root;
    private readonly TestAssetDatabase m_Database;

    public TerrainResidencyCoordinationTests()
    {
        m_Root = Path.Combine(
            Path.GetTempPath(),
            "ArisenTerrainResidencyTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(m_Root);
        m_Database = new TestAssetDatabase(
            AssetSourceAccessMode.Diagnostic,
            Path.Combine(m_Root, "Cooked"));
    }

    [Fact]
    public void SharedRootSurvivesIndependentTileEvictionAndRetainsOwnerAttribution()
    {
        Guid rootGuid = AddCookedAsset(
            "8a100000-0000-0000-0000-000000000001",
            TerrainAssetTypes.Root,
            TerrainRootAssetCooker.RuntimeVariant,
            256);
        Guid firstTileGuid = AddCookedAsset(
            "8a200000-0000-0000-0000-000000000001",
            TerrainAssetTypes.Tile,
            TerrainTileAssetCooker.RuntimeVariant,
            128);
        Guid secondTileGuid = AddCookedAsset(
            "8a200000-0000-0000-0000-000000000002",
            TerrainAssetTypes.Tile,
            TerrainTileAssetCooker.RuntimeVariant,
            128);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakeTerrainPreparedProvider();
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyOwnerId firstOwner = CellOwner(1, 1);
        RuntimeAssetResidencyOwnerId secondOwner = CellOwner(2, 1);
        RuntimeAssetResidencyLease first = residency.AcquireSceneDependencies(
            firstOwner,
            [RootDependency(rootGuid), TileDependency(firstTileGuid)],
            pinned: false);
        RuntimeAssetResidencyLease second = residency.AcquireSceneDependencies(
            secondOwner,
            [RootDependency(rootGuid), TileDependency(secondTileGuid)],
            pinned: false);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, first.State);
        Assert.Equal(RuntimePreparedAssetState.Ready, second.State);
        RuntimeAssetResidencySnapshot root = residency.GetResources().Single(
            resource => resource.Key.Guid == rootGuid);
        Assert.Equal(2, root.OwnerCount);
        Assert.Equal([firstOwner, secondOwner], root.Owners);
        Assert.Equal(1, provider.GetPrepareCount(rootGuid));

        first.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.DoesNotContain(
            residency.GetResources(),
            resource => resource.Key.Guid == firstTileGuid);
        Assert.Contains(
            residency.GetResources(),
            resource => resource.Key.Guid == rootGuid && resource.OwnerCount == 1);
        Assert.Contains(
            residency.GetResources(),
            resource => resource.Key.Guid == secondTileGuid && resource.OwnerCount == 1);
        Assert.Equal(1, provider.GetReleaseCount(firstTileGuid));
        Assert.Equal(0, provider.GetReleaseCount(rootGuid));

        second.Dispose();
        residency.ProcessAtFrameBoundary();

        Assert.Empty(residency.GetResources());
        Assert.Equal(1, provider.GetReleaseCount(rootGuid));
        Assert.Equal(1, provider.GetReleaseCount(secondTileGuid));
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void FailedTilePreparationRequiresFreshGenerationBeforeRetry()
    {
        Guid rootGuid = AddCookedAsset(
            "8a300000-0000-0000-0000-000000000001",
            TerrainAssetTypes.Root,
            TerrainRootAssetCooker.RuntimeVariant,
            64);
        Guid tileGuid = AddCookedAsset(
            "8a300000-0000-0000-0000-000000000002",
            TerrainAssetTypes.Tile,
            TerrainTileAssetCooker.RuntimeVariant,
            64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakeTerrainPreparedProvider(tileGuid);
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        RuntimeAssetResidencyLease failed = residency.AcquireSceneDependencies(
            CellOwner(1, 1),
            [RootDependency(rootGuid), TileDependency(tileGuid)],
            pinned: false);

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Failed, failed.State);
        Assert.Contains(tileGuid.ToString("D"), failed.Diagnostic, StringComparison.Ordinal);
        failed.Dispose();
        residency.ProcessAtFrameBoundary();
        Assert.Empty(residency.GetResources());

        using RuntimeAssetResidencyLease retry = residency.AcquireSceneDependencies(
            CellOwner(1, 2),
            [RootDependency(rootGuid), TileDependency(tileGuid)],
            pinned: false);
        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, retry.State);
        Assert.Equal(2, provider.GetPrepareCount(tileGuid));
    }

    [Fact]
    public void CancelledAcquisitionAndServiceShutdownLeaveNoTerrainOwnership()
    {
        Guid rootGuid = AddCookedAsset(
            "8a400000-0000-0000-0000-000000000001",
            TerrainAssetTypes.Root,
            TerrainRootAssetCooker.RuntimeVariant,
            64);
        Guid tileGuid = AddCookedAsset(
            "8a400000-0000-0000-0000-000000000002",
            TerrainAssetTypes.Tile,
            TerrainTileAssetCooker.RuntimeVariant,
            64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakeTerrainPreparedProvider();
        var residency = CreateResidency(maxInactiveResources: 4);
        residency.RegisterPreparedProvider(provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            residency.AcquireSceneDependencies(
                CellOwner(1, 1),
                [RootDependency(rootGuid), TileDependency(tileGuid)],
                pinned: false,
                cancellation.Token));
        Assert.Empty(residency.GetResources());

        RuntimeAssetResidencyLease active = residency.AcquireSceneDependencies(
            CellOwner(1, 2),
            [RootDependency(rootGuid), TileDependency(tileGuid)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        Assert.Equal(RuntimePreparedAssetState.Ready, active.State);

        residency.Dispose();
        active.Dispose();

        Assert.Equal(1, provider.GetReleaseCount(rootGuid));
        Assert.Equal(1, provider.GetReleaseCount(tileGuid));
        Assert.Empty(m_Database.GetLoadedCookedAssetDiagnostics());
    }

    [Fact]
    public void ProviderInvalidationReturnsOwnedTerrainToWaitingBeforeReprepare()
    {
        Guid rootGuid = AddCookedAsset(
            "8a600000-0000-0000-0000-000000000001",
            TerrainAssetTypes.Root,
            TerrainRootAssetCooker.RuntimeVariant,
            64);
        Guid tileGuid = AddCookedAsset(
            "8a600000-0000-0000-0000-000000000002",
            TerrainAssetTypes.Tile,
            TerrainTileAssetCooker.RuntimeVariant,
            64);
        m_Database.UseReadOnlyRuntime();
        var provider = new FakeTerrainPreparedProvider();
        using var residency = CreateResidency(maxInactiveResources: 0);
        residency.RegisterPreparedProvider(provider);
        using RuntimeAssetResidencyLease lease = residency.AcquireSceneDependencies(
            CellOwner(1, 1),
            [RootDependency(rootGuid), TileDependency(tileGuid)],
            pinned: false);
        residency.ProcessAtFrameBoundary();
        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);

        Assert.True(residency.InvalidatePreparedProvider(
            provider.ProviderId,
            "Test device resources were released."));

        Assert.Equal(RuntimePreparedAssetState.Waiting, lease.State);
        Assert.All(
            residency.GetResources(),
            resource =>
            {
                Assert.Equal(RuntimePreparedAssetState.Waiting, resource.PreparedState);
                Assert.Equal("Test device resources were released.", resource.Diagnostic);
            });
        Assert.Equal(1, provider.GetReleaseCount(rootGuid));
        Assert.Equal(1, provider.GetReleaseCount(tileGuid));

        residency.ProcessAtFrameBoundary();

        Assert.Equal(RuntimePreparedAssetState.Ready, lease.State);
        Assert.Equal(2, provider.GetPrepareCount(rootGuid));
        Assert.Equal(2, provider.GetPrepareCount(tileGuid));
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

    private RuntimeAssetResidencyService CreateResidency(int maxInactiveResources) =>
        new(
            m_Database,
            new RuntimeAssetResidencyBudgets(
                MaxCpuCookedBytes: 1024 * 1024,
                MaxPreparedGpuBytes: 1024 * 1024,
                MaxSetupsPerFrame: 8,
                MaxSetupMilliseconds: 100,
                MaxInactiveResources: maxInactiveResources));

    private Guid AddCookedAsset(
        string guidText,
        string assetType,
        string variant,
        int byteCount)
    {
        Guid guid = Guid.Parse(guidText);
        string source = Path.Combine(m_Root, guid.ToString("N") + ".source");
        string cooked = Path.Combine(m_Root, guid.ToString("N") + ".cooked");
        File.WriteAllText(source, "source");
        File.WriteAllBytes(cooked, Enumerable.Repeat((byte)0x5a, byteCount).ToArray());
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

    private static CookedSceneDependency RootDependency(Guid guid) =>
        new(
            guid,
            PackageId,
            TerrainAssetTypes.Root,
            Required: true,
            Variant: TerrainRootAssetCooker.RuntimeVariant);

    private static CookedSceneDependency TileDependency(Guid guid) =>
        new(
            guid,
            PackageId,
            TerrainAssetTypes.Tile,
            Required: true,
            Variant: TerrainTileAssetCooker.RuntimeVariant);

    private static RuntimeAssetResidencyOwnerId CellOwner(int cell, long generation) =>
        RuntimeAssetResidencyOwnerId.Cell(
            s_WorldGuid,
            new WorldCellId(new Guid($"8a500000-0000-0000-0000-{cell:D12}")),
            generation);

    private sealed class FakeTerrainPreparedProvider : IRuntimePreparedAssetProvider
    {
        private readonly Guid m_FailFirstTile;
        private readonly Dictionary<Guid, int> m_PrepareCounts = new();
        private readonly Dictionary<Guid, int> m_ReleaseCounts = new();
        private readonly HashSet<RuntimeAssetResidencyKey> m_Prepared = new();

        public FakeTerrainPreparedProvider(Guid failFirstTile = default)
        {
            m_FailFirstTile = failFirstTile;
        }

        public string ProviderId => "test.terrain-prepared";

        public bool Supports(string assetType) =>
            assetType is TerrainAssetTypes.Root or TerrainAssetTypes.Tile;

        public RuntimePreparedAssetResult Prepare(RuntimeAssetResidencyKey key)
        {
            int count = GetPrepareCount(key.Guid) + 1;
            m_PrepareCounts[key.Guid] = count;
            if (key.Guid == m_FailFirstTile && count == 1)
            {
                return RuntimePreparedAssetResult.Failed(
                    $"Terrain tile '{key.Guid:D}' failed its first prepared-resource attempt.");
            }

            m_Prepared.Add(key);
            return RuntimePreparedAssetResult.Ready(
                key.AssetType == TerrainAssetTypes.Tile ? 64 : 0);
        }

        public void Release(RuntimeAssetResidencyKey key)
        {
            if (!m_Prepared.Remove(key)) return;
            m_ReleaseCounts[key.Guid] = GetReleaseCount(key.Guid) + 1;
        }

        public RuntimePreparedAssetProviderMetrics GetMetrics() => new(
            m_Prepared.Count,
            m_Prepared.Count(key => key.AssetType == TerrainAssetTypes.Tile) * 64L,
            PendingDisposalCount: 0);

        public int GetPrepareCount(Guid guid) =>
            m_PrepareCounts.TryGetValue(guid, out int count) ? count : 0;

        public int GetReleaseCount(Guid guid) =>
            m_ReleaseCounts.TryGetValue(guid, out int count) ? count : 0;
    }
}
