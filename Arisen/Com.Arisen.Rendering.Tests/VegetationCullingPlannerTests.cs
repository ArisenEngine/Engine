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

    [Fact]
    public void PlannerCullsAndSelectsAcrossMultiKilometerDistances()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput near = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            x: 500.25,
            generation: 1);
        VegetationClusterCullingInput mid = fixture.CreateInput(
            s_ClusterB,
            s_PageB,
            x: 8_000.5,
            generation: 2);
        VegetationClusterCullingInput far = fixture.CreateInput(
            s_ClusterC,
            s_PageC,
            x: 19_999.75,
            generation: 3);
        VegetationClusterCullingInput beyond = fixture.CreateInput(
            Guid.Parse("71000000-0000-0000-0000-000000000004"),
            Guid.Parse("72000000-0000-0000-0000-000000000004"),
            x: 20_001.0,
            generation: 4);
        var planner = new VegetationCullingPlanner();
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 20_000.0
        };
        VegetationCullingView view = CreateView(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0));

        VegetationCullingSelection[] selections = planner.Plan(
            new[] { far, beyond, near, mid },
            view,
            settings).ToArray();

        Assert.Equal(4, planner.Metrics.SourceClusterCount);
        Assert.Equal(1, planner.Metrics.CulledClusterCount);
        Assert.Equal(3, selections.Length);
        Assert.Equal(s_ClusterA, selections[0].ClusterGuid);
        Assert.Equal(499.75 * 499.75, selections[0].DistanceSquared);
        Assert.Equal(s_ClusterB, selections[1].ClusterGuid);
        Assert.Equal(8_000.0 * 8_000.0, selections[1].DistanceSquared);
        Assert.Equal(s_ClusterC, selections[2].ClusterGuid);
        Assert.Equal(19_999.25 * 19_999.25, selections[2].DistanceSquared);
        Assert.All(selections, selection => Assert.Equal(1, selection.LodLevel));

        VegetationCullingSelection[] budgeted = planner.Plan(
            new[] { far, beyond, near, mid },
            view,
            settings with
            {
                MaximumInstanceCount = 1,
                MaximumBatchCount = 1
            }).ToArray();

        Assert.Equal(3, budgeted.Length);
        Assert.Equal(s_ClusterA, budgeted[0].ClusterGuid);
        Assert.True(budgeted[0].Accepted);
        Assert.False(budgeted[1].Accepted);
        Assert.False(budgeted[2].Accepted);
        Assert.Equal(1, planner.Metrics.SelectedInstanceCount);
        Assert.Equal(2, planner.Metrics.DroppedSpeciesCount);
        Assert.True(planner.Metrics.Overflowed);
    }

    [Fact]
    public void PlannerKeepsSubMeterRankingAtTenThousandKilometers()
    {
        using var fixture = new Fixture();
        const double cameraX = 10_000_000.0;
        VegetationClusterCullingInput near = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            new WorldPosition(cameraX + 1_000.25, 0.0, 0.0),
            generation: 1);
        VegetationClusterCullingInput far = fixture.CreateInput(
            s_ClusterB,
            s_PageB,
            new WorldPosition(cameraX + 1_000.75, 0.0, 0.0),
            generation: 2);
        var camera = new WorldPosition(cameraX, 0.0, 0.0);
        WorldPosition snappedOrigin = new(
            Math.Floor(cameraX / 512.0) * 512.0,
            0.0,
            0.0);
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 1_000_000.0,
            MaximumInstanceCount = 1,
            MaximumBatchCount = 1
        };

        var exactPlanner = new VegetationCullingPlanner();
        VegetationCullingSelection[] exact = exactPlanner.Plan(
            new[] { far, near },
            CreateView(camera, camera),
            settings).ToArray();
        var rebasedPlanner = new VegetationCullingPlanner();
        VegetationCullingSelection[] rebased = rebasedPlanner.Plan(
            new[] { far, near },
            CreateView(camera, snappedOrigin),
            settings).ToArray();

        Assert.Equal(2, exact.Length);
        Assert.Equal(2, rebased.Length);
        Assert.Equal(s_ClusterA, exact[0].ClusterGuid);
        Assert.Equal(999.75 * 999.75, exact[0].DistanceSquared);
        Assert.Equal(s_ClusterB, exact[1].ClusterGuid);
        Assert.Equal(1_000.25 * 1_000.25, exact[1].DistanceSquared);
        Assert.NotEqual(exact[0].DistanceSquared, exact[1].DistanceSquared);
        Assert.True(exact[0].Accepted);
        Assert.False(exact[1].Accepted);
        Assert.Equal(1, exactPlanner.Metrics.SelectedInstanceCount);
        Assert.True(exactPlanner.Metrics.Overflowed);

        for (int index = 0; index < exact.Length; index++)
        {
            Assert.Equal(exact[index].ClusterGuid, rebased[index].ClusterGuid);
            Assert.Equal(exact[index].LodLevel, rebased[index].LodLevel);
            Assert.Equal(exact[index].Accepted, rebased[index].Accepted);
            Assert.Equal(exact[index].DistanceSquared, rebased[index].DistanceSquared);
            Assert.Equal(exact[index].ScreenSpaceError, rebased[index].ScreenSpaceError);
        }
    }

    [Fact]
    public void PlannerTreatsNegativeCoordinatesSymmetrically()
    {
        using var fixture = new Fixture();
        var camera = new WorldPosition(-123.5, 4.25, -777.25);
        (double X, double Y, double Z)[] offsets =
        [
            (-2.0, 0.0, 0.0),
            (-500.0, 0.0, 0.0),
            (-999.5, 0.0, 0.0),
            (-1_000.0, 0.0, 0.0),
            (-1_001.0, 0.0, 0.0)
        ];
        var negativeInputs = new VegetationClusterCullingInput[offsets.Length];
        var positiveInputs = new VegetationClusterCullingInput[offsets.Length];
        for (int index = 0; index < offsets.Length; index++)
        {
            negativeInputs[index] = fixture.CreateInput(
                Guid.Parse($"7100{index + 1:D4}-0000-0000-0000-000000000001"),
                Guid.Parse($"7200{index + 1:D4}-0000-0000-0000-000000000001"),
                new WorldPosition(
                    camera.X + offsets[index].X,
                    camera.Y + offsets[index].Y,
                    camera.Z + offsets[index].Z),
                generation: (ulong)(index + 1));
            positiveInputs[index] = fixture.CreateInput(
                Guid.Parse($"7101{index + 1:D4}-0000-0000-0000-000000000001"),
                Guid.Parse($"7201{index + 1:D4}-0000-0000-0000-000000000001"),
                new WorldPosition(
                    -camera.X - offsets[index].X,
                    -camera.Y - offsets[index].Y,
                    -camera.Z - offsets[index].Z),
                generation: (ulong)(index + 1));
        }

        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 1_000.0
        };
        VegetationCullingView negativeView = CreateView(camera, camera);
        var positiveCamera = new WorldPosition(-camera.X, -camera.Y, -camera.Z);
        VegetationCullingView positiveView = CreateView(positiveCamera, positiveCamera);

        var negativePlanner = new VegetationCullingPlanner();
        VegetationCullingSelection[] negative = negativePlanner.Plan(
            negativeInputs,
            negativeView,
            settings).ToArray();
        var positivePlanner = new VegetationCullingPlanner();
        VegetationCullingSelection[] positive = positivePlanner.Plan(
            positiveInputs,
            positiveView,
            settings).ToArray();

        Assert.Equal(4, negative.Length);
        Assert.Equal(4, positive.Length);
        Assert.Equal(1, negativePlanner.Metrics.CulledClusterCount);
        Assert.Equal(1, positivePlanner.Metrics.CulledClusterCount);
        Assert.Equal(1.5 * 1.5, negative[0].DistanceSquared);
        Assert.Equal(499.5 * 499.5, negative[1].DistanceSquared);
        Assert.Equal(999.0 * 999.0, negative[2].DistanceSquared);
        Assert.Equal(999.5 * 999.5, negative[3].DistanceSquared);

        for (int index = 0; index < negative.Length; index++)
        {
            Assert.Equal(negative[index].LodLevel, positive[index].LodLevel);
            Assert.Equal(negative[index].Accepted, positive[index].Accepted);
            Assert.Equal(negative[index].VisiblePageCount, positive[index].VisiblePageCount);
            Assert.Equal(negative[index].VisibleInstanceCount, positive[index].VisibleInstanceCount);
            Assert.Equal(negative[index].DistanceSquared, positive[index].DistanceSquared);
            Assert.Equal(negative[index].ScreenSpaceError, positive[index].ScreenSpaceError);
        }
    }

    [Fact]
    public void PlannerSkipsCandidatesThatDoNotFitTheInstanceBudget()
    {
        using var fixture = new Fixture();
        VegetationClusterCullingInput near = fixture.CreateInput(
            s_ClusterA,
            s_PageA,
            new WorldPosition(1.25, 0.0, 0.0),
            generation: 1);
        VegetationClusterCullingInput wide = fixture.CreateInput(
            s_ClusterB,
            s_PageB,
            new WorldPosition(2.25, 0.0, 0.0),
            generation: 2,
            instanceCount: 100);
        VegetationClusterCullingInput tail = fixture.CreateInput(
            s_ClusterC,
            s_PageC,
            new WorldPosition(3.25, 0.0, 0.0),
            generation: 3);
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 100.0,
            MaximumInstanceCount = 2,
            MaximumBatchCount = 2
        };
        VegetationCullingView view = CreateView(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0));

        VegetationCullingSelection[] selections = new VegetationCullingPlanner()
            .Plan(new[] { tail, wide, near }, view, settings)
            .ToArray();
        var replayPlanner = new VegetationCullingPlanner();
        VegetationCullingSelection[] replay = replayPlanner.Plan(
            new[] { tail, wide, near },
            view,
            settings).ToArray();

        Assert.Equal(3, selections.Length);
        VegetationCullingSelection nearSelection = Assert.Single(
            selections,
            selection => selection.ClusterGuid == s_ClusterA);
        VegetationCullingSelection wideSelection = Assert.Single(
            selections,
            selection => selection.ClusterGuid == s_ClusterB);
        VegetationCullingSelection tailSelection = Assert.Single(
            selections,
            selection => selection.ClusterGuid == s_ClusterC);
        Assert.True(nearSelection.Accepted);
        Assert.False(wideSelection.Accepted);
        Assert.True(tailSelection.Accepted);
        Assert.Equal(1, nearSelection.VisibleInstanceCount);
        Assert.Equal(100, wideSelection.VisibleInstanceCount);
        Assert.Equal(100, wideSelection.BudgetInstanceCount);
        Assert.Equal(0.75 * 0.75, nearSelection.DistanceSquared);
        Assert.Equal(1.75 * 1.75, wideSelection.DistanceSquared);
        Assert.Equal(2.75 * 2.75, tailSelection.DistanceSquared);
        Assert.Equal(2, replayPlanner.Metrics.SelectedSpeciesCount);
        Assert.Equal(2, replayPlanner.Metrics.SelectedInstanceCount);
        Assert.Equal(1, replayPlanner.Metrics.DroppedSpeciesCount);
        Assert.True(replayPlanner.Metrics.Overflowed);
        for (int index = 0; index < selections.Length; index++)
        {
            Assert.Equal(selections[index].ClusterGuid, replay[index].ClusterGuid);
            Assert.Equal(selections[index].Accepted, replay[index].Accepted);
            Assert.Equal(selections[index].BudgetInstanceCount, replay[index].BudgetInstanceCount);
        }
    }

    [Fact]
    public void PlannerKeepsDenseOverflowNearestFirstAndDeterministic()
    {
        using var fixture = new Fixture();
        const int ClusterCount = 256;
        const int Budget = 64;
        var inputs = new VegetationClusterCullingInput[ClusterCount];
        for (int index = 0; index < ClusterCount; index++)
        {
            inputs[index] = fixture.CreateInput(
                Guid.Parse($"71A0{index:D4}-0000-0000-0000-000000000001"),
                Guid.Parse($"71B0{index:D4}-0000-0000-0000-000000000001"),
                new WorldPosition((index * 4.0) + 0.25, 0.0, 0.0),
                generation: 1);
        }

        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            EnableFrustumCulling = false,
            MaximumDistance = 100_000.0,
            MaximumInstanceCount = Budget
        };
        VegetationCullingView view = CreateView(
            new WorldPosition(0.0, 0.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0));
        var planner = new VegetationCullingPlanner();

        VegetationCullingSelection[] first = planner.Plan(inputs, view, settings).ToArray();
        Assert.Equal(ClusterCount, first.Length);
        Assert.Equal(ClusterCount, planner.Metrics.SourceClusterCount);
        Assert.Equal(0, planner.Metrics.CulledClusterCount);
        Assert.True(planner.Metrics.Overflowed);
        Assert.Equal(Budget, planner.Metrics.SelectedSpeciesCount);
        Assert.Equal(Budget, planner.Metrics.SelectedInstanceCount);
        Assert.Equal(ClusterCount - Budget, planner.Metrics.DroppedSpeciesCount);

        int acceptedCount = 0;
        double maximumAcceptedDistance = double.NegativeInfinity;
        double minimumDroppedDistance = double.PositiveInfinity;
        for (int index = 0; index < first.Length; index++)
        {
            if (index > 0)
            {
                Assert.True(
                    first[index - 1].ClusterGuid.CompareTo(first[index].ClusterGuid) < 0);
            }

            if (first[index].Accepted)
            {
                acceptedCount++;
                maximumAcceptedDistance = Math.Max(
                    maximumAcceptedDistance,
                    first[index].DistanceSquared);
            }
            else
            {
                minimumDroppedDistance = Math.Min(
                    minimumDroppedDistance,
                    first[index].DistanceSquared);
            }
        }

        Assert.Equal(Budget, acceptedCount);
        Assert.True(maximumAcceptedDistance < minimumDroppedDistance);

        VegetationCullingSelection[] second = new VegetationCullingPlanner()
            .Plan(inputs, view, settings)
            .ToArray();
        Assert.Equal(first.Length, second.Length);
        for (int index = 0; index < first.Length; index++)
        {
            Assert.Equal(first[index].ClusterGuid, second[index].ClusterGuid);
            Assert.Equal(first[index].LodLevel, second[index].LodLevel);
            Assert.Equal(first[index].Accepted, second[index].Accepted);
            Assert.Equal(first[index].DistanceSquared, second[index].DistanceSquared);
            Assert.Equal(first[index].BudgetInstanceCount, second[index].BudgetInstanceCount);
        }

        _ = planner.Plan(inputs, view, settings);
        _ = planner.Plan(inputs, view, settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = planner.Plan(inputs, view, settings);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void PlannerIsDeterministicAlongRebasedCameraPath()
    {
        using var fixture = new Fixture();
        (double X, double Z)[] placements =
        [
            (4_000.0, 0.0),
            (1_500.0, 40.0),
            (400.0, -60.0),
            (-400.0, 80.0),
            (-1_500.0, -30.0),
            (-3_000.0, 0.0),
            (2_500.0, 0.0),
            (-700.0, 120.0)
        ];
        var clusterGuids = new Guid[placements.Length];
        var inputs = new VegetationClusterCullingInput[placements.Length];
        for (int index = 0; index < placements.Length; index++)
        {
            clusterGuids[index] = Guid.Parse($"7100100{index}-0000-0000-0000-000000000001");
            inputs[index] = fixture.CreateInput(
                clusterGuids[index],
                Guid.Parse($"7200100{index}-0000-0000-0000-000000000001"),
                new WorldPosition(placements[index].X, 0.0, placements[index].Z),
                generation: 1);
        }

        const int FrameCount = 65;
        VegetationCullingSettings settings = VegetationCullingSettings.Default with
        {
            MaximumDistance = 12_000.0,
            MaximumInstanceCount = 4
        };

        FrameSelection[][] snapped = RunCameraPath(
            new VegetationCullingPlanner(),
            inputs,
            settings,
            FrameCount,
            snapRenderOrigin: true,
            validate: true,
            record: true)!;
        FrameSelection[][] replay = RunCameraPath(
            new VegetationCullingPlanner(),
            inputs,
            settings,
            FrameCount,
            snapRenderOrigin: true,
            validate: true,
            record: true)!;
        FrameSelection[][] unsnapped = RunCameraPath(
            new VegetationCullingPlanner(),
            inputs,
            settings,
            FrameCount,
            snapRenderOrigin: false,
            validate: true,
            record: true)!;

        for (int frame = 0; frame < FrameCount; frame++)
        {
            Assert.Equal(snapped[frame].Length, replay[frame].Length);
            Assert.Equal(snapped[frame].Length, unsnapped[frame].Length);
            for (int index = 0; index < snapped[frame].Length; index++)
            {
                Assert.Equal(snapped[frame][index], replay[frame][index]);
                Assert.Equal(snapped[frame][index], unsnapped[frame][index]);
            }
        }

        int previousLod = int.MaxValue;
        bool reachedFinestLod = false;
        for (int frame = 0; frame < FrameCount; frame++)
        {
            FrameSelection approach = Assert.Single(
                snapped[frame],
                entry => entry.ClusterGuid == clusterGuids[0]);
            Assert.True(approach.LodLevel <= previousLod);
            previousLod = approach.LodLevel;
            reachedFinestLod |= approach.LodLevel == 0;
        }

        Assert.True(reachedFinestLod);

        var warmed = new VegetationCullingPlanner();
        _ = RunCameraPath(
            warmed,
            inputs,
            settings,
            FrameCount,
            snapRenderOrigin: true,
            validate: false,
            record: false);
        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = RunCameraPath(
            warmed,
            inputs,
            settings,
            FrameCount,
            snapRenderOrigin: true,
            validate: false,
            record: false);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private readonly record struct FrameSelection(
        Guid ClusterGuid,
        ulong Generation,
        int LodLevel,
        bool Accepted,
        double DistanceSquared,
        double ScreenSpaceError);

    private static FrameSelection[][]? RunCameraPath(
        VegetationCullingPlanner planner,
        VegetationClusterCullingInput[] inputs,
        VegetationCullingSettings settings,
        int frameCount,
        bool snapRenderOrigin,
        bool validate,
        bool record)
    {
        FrameSelection[][]? frames = record ? new FrameSelection[frameCount][] : null;
        for (int frame = 0; frame < frameCount; frame++)
        {
            double cameraX = -4_000.0 + (8_000.0 * frame / (frameCount - 1));
            var camera = new WorldPosition(cameraX, 0.0, 0.0);
            WorldPosition renderOrigin = snapRenderOrigin
                ? new WorldPosition(Math.Floor(cameraX / 512.0) * 512.0, 0.0, 0.0)
                : camera;
            ReadOnlySpan<VegetationCullingSelection> selections = planner.Plan(
                inputs,
                CreateForwardView(camera, renderOrigin),
                settings);
            if (validate)
            {
                ValidateCameraPathFrame(planner.Metrics, selections, settings);
            }

            if (frames != null)
            {
                var recorded = new FrameSelection[selections.Length];
                for (int index = 0; index < selections.Length; index++)
                {
                    recorded[index] = new FrameSelection(
                        selections[index].ClusterGuid,
                        selections[index].Generation,
                        selections[index].LodLevel,
                        selections[index].Accepted,
                        selections[index].DistanceSquared,
                        selections[index].ScreenSpaceError);
                }

                frames[frame] = recorded;
            }
        }

        return frames;
    }

    private static void ValidateCameraPathFrame(
        in VegetationCullingMetrics metrics,
        ReadOnlySpan<VegetationCullingSelection> selections,
        in VegetationCullingSettings settings)
    {
        Assert.Equal(metrics.CandidateSpeciesCount, selections.Length);
        int acceptedCount = 0;
        int acceptedInstances = 0;
        double maximumAcceptedDistance = double.NegativeInfinity;
        double minimumDroppedDistance = double.PositiveInfinity;
        for (int index = 0; index < selections.Length; index++)
        {
            ref readonly VegetationCullingSelection selection = ref selections[index];
            if (index > 0)
            {
                Assert.True(
                    selections[index - 1].ClusterGuid.CompareTo(selection.ClusterGuid) < 0);
            }

            if (selection.Accepted)
            {
                acceptedCount++;
                acceptedInstances += selection.BudgetInstanceCount;
                maximumAcceptedDistance = Math.Max(
                    maximumAcceptedDistance,
                    selection.DistanceSquared);
            }
            else
            {
                minimumDroppedDistance = Math.Min(
                    minimumDroppedDistance,
                    selection.DistanceSquared);
            }
        }

        Assert.True(acceptedCount <= settings.MaximumBatchCount);
        Assert.True(acceptedInstances <= settings.MaximumInstanceCount);
        Assert.Equal(metrics.SelectedSpeciesCount, acceptedCount);
        Assert.Equal(metrics.SelectedInstanceCount, acceptedInstances);
        if (acceptedCount > 0 && acceptedCount < selections.Length)
        {
            Assert.True(maximumAcceptedDistance <= minimumDroppedDistance);
        }
    }

    private static VegetationCullingView CreateForwardView(
        WorldPosition cameraWorldPosition,
        WorldPosition renderOrigin)
    {
        Vector3 eye = new(
            (float)(cameraWorldPosition.X - renderOrigin.X),
            (float)(cameraWorldPosition.Y - renderOrigin.Y),
            (float)(cameraWorldPosition.Z - renderOrigin.Z));
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, eye + Vector3.UnitX, Vector3.UnitY);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3.0f,
            16.0f / 9.0f,
            0.25f,
            20_000.0f);
        return new VegetationCullingView(
            cameraWorldPosition,
            renderOrigin,
            view * projection,
            VegetationCullingProjection.Perspective,
            Math.PI / 3.0,
            2.0,
            1080);
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
            ulong generation) =>
            CreateInput(
                clusterGuid,
                pageGuid,
                new WorldPosition(x, 0.0, 0.0),
                generation);

        public VegetationClusterCullingInput CreateInput(
            Guid clusterGuid,
            Guid pageGuid,
            WorldPosition origin,
            ulong generation,
            int instanceCount = 1)
        {
            CookedVegetationInstancePage page = CreatePage(
                clusterGuid,
                pageGuid,
                origin,
                instanceCount);
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
            WorldPosition origin,
            int instanceCount)
        {
            var instances = new CookedVegetationInstance[instanceCount];
            for (int index = 0; index < instanceCount; index++)
            {
                instances[index] = new CookedVegetationInstance(
                    (ulong)(index + 1),
                    0,
                    new Vector3(index * 0.1f, 0.0f, 0.0f),
                    Quaternion.Identity,
                    1.0f,
                    0.5f);
            }

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            double maxZ = double.NegativeInfinity;
            foreach (CookedVegetationInstance instance in instances)
            {
                double radius = instance.ConservativeRadius;
                double x = origin.X + instance.LocalPosition.X;
                double y = origin.Y + instance.LocalPosition.Y;
                double z = origin.Z + instance.LocalPosition.Z;
                minX = Math.Min(minX, x - radius);
                minY = Math.Min(minY, y - radius);
                minZ = Math.Min(minZ, z - radius);
                maxX = Math.Max(maxX, x + radius);
                maxY = Math.Max(maxY, y + radius);
                maxZ = Math.Max(maxZ, z + radius);
            }

            var page = new CookedVegetationInstancePage(
                pageGuid,
                clusterGuid,
                PackageId,
                1,
                origin,
                new WorldBounds(
                    new WorldPosition(minX, minY, minZ),
                    new WorldPosition(maxX, maxY, maxZ)),
                [new CookedVegetationSpeciesReference(s_Species, PackageId)],
                instances);
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
