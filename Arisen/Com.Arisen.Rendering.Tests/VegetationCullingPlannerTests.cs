using Arisen.Native.RHI;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using System.Numerics;
using System.Security.Cryptography;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationCullingPlannerTests
{
    private static readonly Guid s_ClusterA =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid s_ClusterB =
        Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly Guid s_PageA =
        Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid s_PageB =
        Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly Guid s_ClusterC =
        Guid.Parse("71000000-0000-0000-0000-000000000003");
    private static readonly Guid s_PageC =
        Guid.Parse("72000000-0000-0000-0000-000000000003");
    private static readonly Guid s_Species =
        Guid.Parse("73000000-0000-0000-0000-000000000001");
    private static readonly Guid s_Biome =
        Guid.Parse("74000000-0000-0000-0000-000000000001");
    private const string PackageId = "com.arisen.vegetation.tests";

    [Fact]
    public void PlannerSelectsDistanceLodAndPreservesStableOrdering()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput near = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 2.0,
            generation: 1);
        VegetationClusterCullingInput far = fixture.CreateInput(
            s_ClusterB,
            s_PageB,
            x: 50.0,
            generation: 2);
        var inputs = new[] { far, near };
        var planner = new VegetationCullingPlanner();
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 100.0
        };
        VegetationCullingView view = CreateView(
            cameraWorldPosition: new WorldPosition(0.0, 0.0, 0.0),
            renderOrigin: new WorldPosition(0.0, 0.0, 0.0));

        ReadOnlySpan<VegetationCullingSelection> first = planner.Plan(inputs, view, settings);
        Assert.True(first.Length == 2, planner.Metrics.ToString());
        Assert.Equal(s_ClusterA, first[0].ClusterGuid);
        Assert.Equal(0, first[0].LodLevel);
        Assert.Equal(s_ClusterB, first[1].ClusterGuid);
        Assert.Equal(1, first[1].LodLevel);
        Assert.Equal(2, planner.Metrics.SelectedSpeciesCount);

        ReadOnlySpan<VegetationCullingSelection> second = planner.Plan(
            new[] { near, far },
            view,
            settings);
        Assert.Equal(
            first.ToArray().Select(selection => selection.ClusterGuid),
            second.ToArray().Select(selection => selection.ClusterGuid));
        Assert.Equal(
            first.ToArray().Select(selection => selection.LodLevel),
            second.ToArray().Select(selection => selection.LodLevel));
    }

    [Fact]
    public void PlannerRejectsFrustumOutsideAndHonorsNearestBudget()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput inside = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 0.0,
            generation: 1);
        VegetationClusterCullingInput outside = fixture.CreateInput(
            s_ClusterB,
            s_PageB,
            x: 5.0,
            generation: 2);
        var planner = new VegetationCullingPlanner();
        VegetationCullingSettings settings = VegetationCullingSettings.Default;
        VegetationCullingView view = CreateView(
            cameraWorldPosition: new WorldPosition(0.0, 0.0, 0.0),
            renderOrigin: new WorldPosition(0.0, 0.0, 0.0));

        ReadOnlySpan<VegetationCullingSelection> result = planner.Plan(
            new[] { outside, inside },
            view,
            settings);

        Assert.Single(result.ToArray());
        Assert.True(result[0].Accepted);
        Assert.Equal(s_ClusterA, result[0].ClusterGuid);
        Assert.Equal(1, planner.Metrics.CulledClusterCount);
        Assert.Equal(0, planner.Metrics.DroppedSpeciesCount);

        VegetationClusterCullingInput budgetPeer = fixture.CreateInput(
            s_ClusterC,
            s_PageC,
            x: 1.0,
            generation: 3);
        ReadOnlySpan<VegetationCullingSelection> budgeted = planner.Plan(
            new[] { budgetPeer, inside },
            CreateView(
                new WorldPosition(0.0, 0.0, 0.0),
                new WorldPosition(0.0, 0.0, 0.0)),
            settings with
            {
                EnableFrustumCulling = false,
                MaximumInstanceCount = 1,
                MaximumBatchCount = 1
            });
        Assert.Equal(2, budgeted.Length);
        Assert.True(budgeted[0].Accepted);
        Assert.False(budgeted[1].Accepted);
        Assert.Equal(1, planner.Metrics.DroppedSpeciesCount);
        Assert.True(planner.Metrics.Overflowed);
    }

    [Fact]
    public void PlannerHoldsLodInsideHysteresisBandAndPreservesRebaseIdentity()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput input = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 0.0,
            generation: 1);
        var planner = new VegetationCullingPlanner();
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 200.0
        };

        VegetationCullingSelection near = Assert.Single(
            planner.Plan(
                new[] { input },
                CreateView(
                    new WorldPosition(0.0, 0.0, 0.0),
                    new WorldPosition(0.0, 0.0, 0.0)),
                settings).ToArray());
        Assert.Equal(0, near.LodLevel);

        VegetationCullingSelection far = Assert.Single(
            planner.Plan(
                new[] { input },
                CreateView(
                    new WorldPosition(-12.0, 0.0, 0.0),
                    new WorldPosition(0.0, 0.0, 0.0)),
                settings).ToArray());
        Assert.Equal(1, far.LodLevel);

        VegetationCullingSelection boundary = Assert.Single(
            planner.Plan(
                new[] { input },
                CreateView(
                    new WorldPosition(-10.5, 0.0, 0.0),
                    new WorldPosition(0.0, 0.0, 0.0)),
                settings).ToArray());
        Assert.Equal(1, boundary.LodLevel);

        VegetationCullingSelection rebased = Assert.Single(
            planner.Plan(
                new[] { input },
                CreateView(
                    new WorldPosition(-10.5, 0.0, 0.0),
                    new WorldPosition(100_000.0, 0.0, 0.0)),
                settings).ToArray());
        Assert.Equal(boundary.LodLevel, rebased.LodLevel);
        Assert.Equal(boundary.ClusterGuid, rebased.ClusterGuid);

        VegetationCullingSelection refined = Assert.Single(
            planner.Plan(
                new[] { input },
                CreateView(
                    new WorldPosition(-8.0, 0.0, 0.0),
                    new WorldPosition(100_000.0, 0.0, 0.0)),
                settings).ToArray());
        Assert.Equal(0, refined.LodLevel);
    }

    [Fact]
    public void PlannerAppliesDeterministicDensityQuality()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput input = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 0.0,
            generation: 1);
        var planner = new VegetationCullingPlanner();
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 200.0,
            DensityMultiplier = 0.0
        };
        VegetationCullingView view = CreateView(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0));

        ReadOnlySpan<VegetationCullingSelection> first = planner.Plan(
            new[] { input },
            view,
            settings);
        ReadOnlySpan<VegetationCullingSelection> second = planner.Plan(
            new[] { input },
            view,
            settings);

        Assert.Single(first.ToArray());
        Assert.False(first[0].Accepted);
        Assert.Equal(1, planner.Metrics.DroppedSpeciesCount);
        Assert.Equal(first[0].ClusterGuid, second[0].ClusterGuid);
        Assert.Equal(first[0].LodLevel, second[0].LodLevel);
        Assert.False(second[0].Accepted);
    }

    [Fact]
    public void PlannerHasZeroSteadyStateAllocationAfterWarmup()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput input = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 0.0,
            generation: 1);
        var planner = new VegetationCullingPlanner();
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 200.0
        };
        VegetationCullingView view = CreateView(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0));
        VegetationClusterCullingInput[] inputs = [input];

        _ = planner.Plan(inputs, view, settings);
        _ = planner.Plan(inputs, view, settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = planner.Plan(inputs, view, settings);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void PlannerInvalidViewAndSettingsProduceEmptyOutput()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput input = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 0.0,
            generation: 1);
        var planner = new VegetationCullingPlanner();
        VegetationCullingView invalidView = default;

        Assert.Empty(planner.Plan(
            new[] { input },
            invalidView,
            VegetationCullingSettings.Default).ToArray());
        Assert.Empty(planner.Plan(
            new[] { input },
            CreateView(
                new WorldPosition(0.0, 0.0, 0.0),
                new WorldPosition(0.0, 0.0, 0.0)),
            VegetationCullingSettings.Default with { MaximumBatchCount = 0 }).ToArray());
        Assert.Equal(1, planner.Metrics.SourceClusterCount);
        Assert.Equal(1, planner.Metrics.DroppedSpeciesCount);
    }

    private static VegetationCullingView CreateView(
        WorldPosition cameraWorldPosition,
        WorldPosition renderOrigin) => new(
            cameraWorldPosition,
            renderOrigin,
            Matrix4x4.Identity,
            VegetationCullingProjection.Perspective,
            Math.PI / 3.0,
            2.0,
            1080);

    private sealed class Fixture : IDisposable
    {
        private readonly VegetationRuntimeDataStore m_Store = new();
        private readonly CookedVegetationSpecies m_Species = CreateSpecies();

        public VegetationClusterCullingInput CreateInput(
            Guid clusterGuid,
            Guid pageGuid,
            double x,
            ulong generation)
        {
            WorldPosition origin = new(x, 0.0, 0.0);
            CookedVegetationInstancePage page = CreatePage(
                clusterGuid,
                pageGuid,
                origin);
            byte[] payload = VegetationInstancePageAssetCooker.WritePayload(page);
            var pageReference = new CookedVegetationInstancePageReference(
                pageGuid,
                PackageId,
                page.Instances.Count,
                origin,
                page.Bounds,
                payload.LongLength,
                SHA256.HashData(payload));
            var cluster = new CookedVegetationCluster(
                clusterGuid,
                PackageId,
                1,
                new CookedVegetationBiomeReference(s_Biome, PackageId),
                page.Bounds,
                [new CookedVegetationSpeciesReference(s_Species, PackageId)],
                [pageReference],
                page.Instances.Count);
            m_Store.PublishCluster(cluster);
            m_Store.PublishPage(page);
            VegetationResidentClusterData resident =
                Assert.Single(
                    m_Store.GetSnapshot().Clusters.ToArray(),
                    candidate => candidate.Guid == clusterGuid);
            CookedVegetationClusterAcceleration acceleration =
                new(
                    page.Bounds,
                    [new CookedVegetationSpatialNode(page.Bounds, -1, -1, 0, 1)],
                    [new CookedVegetationSpeciesAcceleration(
                        new CookedVegetationSpeciesReference(s_Species, PackageId),
                        0,
                        0,
                        1,
                        page.Instances.Count,
                        page.Bounds,
                        1.0f,
                        100.0f,
                        4.0f)]);
            VegetationPreparedBatch[] batches =
            [
                CreateBatch(lodLevel: 0, maximumDistance: 10.0f, maximumScreenError: 1.0f),
                CreateBatch(lodLevel: 1, maximumDistance: 100.0f, maximumScreenError: 4.0f)
            ];
            var prepared = new VegetationPreparedClusterView(
                clusterGuid,
                resident.Generation,
                origin,
                batches,
                page.Instances.Count,
                acceleration,
                [m_Species]);
            VegetationClusterComponent component = new()
            {
                ClusterGuid = clusterGuid,
                BiomeGuid = s_Biome,
                SpeciesGuid = s_Species,
                OriginX = origin.X,
                OriginY = origin.Y,
                OriginZ = origin.Z,
                PageCount = 1,
                InstanceCount = page.Instances.Count,
                Flags = VegetationClusterFlags.Visible
            };
            return new VegetationClusterCullingInput(component, resident, prepared);
        }

        public void Dispose()
        {
        }

        private static CookedVegetationSpecies CreateSpecies() => new(
            s_Species,
            PackageId,
            1,
            "Planner Species",
            [
                    new CookedVegetationSpeciesLod(
                    new CookedVegetationMeshReference(Guid.Parse("75000000-0000-0000-0000-000000000001"), PackageId),
                    new CookedVegetationMaterialReference(Guid.Parse("76000000-0000-0000-0000-000000000001"), PackageId),
                    10.0f,
                    0.0f),
                new CookedVegetationSpeciesLod(
                    new CookedVegetationMeshReference(Guid.Parse("75000000-0000-0000-0000-000000000002"), PackageId),
                    new CookedVegetationMaterialReference(Guid.Parse("76000000-0000-0000-0000-000000000002"), PackageId),
                    100.0f,
                    1.0f)
            ],
            VegetationShadowPolicy.Cast,
            new VegetationValueRange(1.0f, 1.0f),
            new VegetationValueRange(0.0f, 0.0f),
            new VegetationValueRange(0.0f, 0.0f),
            new VegetationCollisionPromotionDescriptor(
                VegetationCollisionPromotionMode.None,
                0.0f,
                0.0f,
                0.0f),
            0.0f);

        private static CookedVegetationInstancePage CreatePage(
            Guid clusterGuid,
            Guid pageGuid,
            WorldPosition origin)
        {
            var page = new CookedVegetationInstancePage(
                pageGuid,
                clusterGuid,
                PackageId,
                1,
                origin,
                new WorldBounds(
                    new WorldPosition(origin.X - 0.5, -0.5, -0.5),
                    new WorldPosition(origin.X + 0.5, 0.5, 0.5)),
                [new CookedVegetationSpeciesReference(s_Species, PackageId)],
                [new CookedVegetationInstance(
                    1,
                    0,
                    Vector3.Zero,
                    Quaternion.Identity,
                    1.0f,
                    0.5f)]);
            return page with
            {
                Acceleration = VegetationInstancePageAssetCooker.BuildAcceleration(page)
            };
        }

        private static VegetationPreparedBatch CreateBatch(
            int lodLevel,
            float maximumDistance,
            float maximumScreenError) => new(
                s_Species,
                Guid.Parse($"75000000-0000-0000-0000-{lodLevel + 1:D12}"),
                Guid.Parse($"76000000-0000-0000-0000-{lodLevel + 1:D12}"),
                new RHIBufferHandle { Index = 1, Generation = 1 },
                new RHIBufferHandle { Index = 2, Generation = 1 },
                EIndexType.INDEX_TYPE_UINT32,
                3,
                0,
                0,
                7,
                0,
                1,
                lodLevel,
                maximumDistance,
                maximumScreenError,
                VegetationShadowPolicy.Cast,
                new VegetationPreparedMaterialData(
                    Vector4.One,
                    0.0f,
                    1.0f,
                    0,
                    0));
    }
}
