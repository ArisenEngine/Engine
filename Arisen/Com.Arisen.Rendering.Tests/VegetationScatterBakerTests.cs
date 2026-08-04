using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Core.Assets;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Terrain;
using ArisenEngine.Terrain.Assets;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationScatterBakerTests
{
    private static readonly Guid s_WorldGuid =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("92000000-0000-0000-0000-000000000001");
    private static readonly Guid s_SpeciesGuid =
        Guid.Parse("94000000-0000-0000-0000-000000000001");
    private static readonly Guid s_MeshGuid =
        Guid.Parse("95000000-0000-0000-0000-000000000001");
    private static readonly Guid s_MaterialGuid =
        Guid.Parse("96000000-0000-0000-0000-000000000001");

    private const string PackageId = "com.arisen.vegetation.scatter-test";
    private const string EntryId = "surface-rock";
    private const ulong GlobalSeed = 0x1020304050607080UL;
    private const ulong SeedSalt = 0xfedcba9876543210UL;
    private const float ConservativeRadius = 1.25f;

    private static readonly WorldPartitionSettings s_Partition = new(
        new WorldPosition(0.0, 0.0, 0.0),
        new WorldPosition(2.0, 128.0, 2.0),
        LoadRadius: 1,
        UnloadHysteresis: 1,
        MaxActiveCells: 16);

    private static readonly WorldCellKey s_NegativeCell = new(
        new WorldCellCoordinate(-1, 0, -1),
        "surface");

    [Fact]
    public void CandidateSequence_MatchesFrozenGoldenVectorsAcrossSignedSpatialCells()
    {
        CandidateGolden[] expected =
        [
            new(-2L, -3L, 0, 0x5a4447496604847dUL, 0xbfff53e60ec92646UL,
                0xc006de047268f1b0UL, 0x3fe653c6d278187dUL, 0x3fea4ce07db6d6e2UL,
                0x3fb331278988e260UL, 0x3f98836fc1e248e0UL),
            new(-1L, 0L, 1, 0xb66a62b13fd0f480UL, 0xbfc378f8eadbd504UL,
                0x3fe0005e76df6cbaUL, 0x3fe7eabbebd7ae6aUL, 0x3fdd5da2f3390bb4UL,
                0x3f9d43c7b0d5ebe0UL, 0x3fbf973c94a12780UL),
            new(0L, -1L, 2, 0xe6567a091c9e250bUL, 0x3fede285dc93a8d8UL,
                0xbfefce8be9dc21e8UL, 0x3fe259dd6bb1d82aUL, 0x3f9b5b3350405500UL,
                0x3fe4ee77fe4d270aUL, 0x3fdcd689db4db814UL),
            new(0L, 0L, 3, 0x70787b8a02572d8cUL, 0x3fc815e1849f1bccUL,
                0x3fe9edc011db5f6dUL, 0x3fead4a595a840e0UL, 0x3fd980b99419d72cUL,
                0x3fef0e423c18f403UL, 0x3fa5fb0f4d038d90UL),
            new(1L, 2L, 4, 0xa944c046ddd526f2UL, 0x3ff2e98011149518UL,
                0x4006e5eef43af5ddUL, 0x3fdc8c701805f506UL, 0x3fe327eb99743387UL,
                0x3fd81f7069b62bcaUL, 0x3fe87fa7cbfd1038UL)
        ];
        VegetationScatterCandidate[] actual = expected
            .Select(vector => CreateCandidate(vector.SpatialCellX, vector.SpatialCellZ, vector.Ordinal))
            .ToArray();

        for (int index = 0; index < actual.Length; index++)
        {
            VegetationScatterCandidate candidate = actual[index];
            Assert.InRange(candidate.WorldX, candidate.SpatialCellX, candidate.SpatialCellX + 1.0);
            Assert.InRange(candidate.WorldZ, candidate.SpatialCellZ, candidate.SpatialCellZ + 1.0);
            Assert.True(candidate.WorldX < candidate.SpatialCellX + 1.0);
            Assert.True(candidate.WorldZ < candidate.SpatialCellZ + 1.0);
            Assert.NotEqual(0UL, candidate.StableKey);
        }

        CandidateGolden[] observed = actual.Select(ToGolden).ToArray();
        Assert.True(
            expected.SequenceEqual(observed),
            "Actual golden vectors:" + Environment.NewLine +
            string.Join(Environment.NewLine, observed.Select(FormatGolden)));
    }

    [Fact]
    public void CandidateSequence_IsStatelessAcrossInterleavingAndParallelScheduling()
    {
        CandidateInput[] inputs =
        [
            new(-17, 9, 0),
            new(-1, -1, 1),
            new(0, 0, 2),
            new(1, -2, 3),
            new(71, 33, 4),
            new(long.MinValue + 1, long.MaxValue - 1, 5)
        ];
        VegetationScatterCandidate[] baseline = inputs
            .Select(input => CreateCandidate(input.X, input.Z, input.Ordinal))
            .ToArray();
        var interleaved = new VegetationScatterCandidate[inputs.Length];
        for (int index = inputs.Length - 1; index >= 0; index--)
        {
            _ = VegetationScatterCandidateSequence.Create(
                s_WorldGuid,
                s_BiomeGuid,
                TerrainRuntimeTestData.RootGuid,
                s_SpeciesGuid,
                "interleaved",
                GlobalSeed + (ulong)index + 1UL,
                SeedSalt - (ulong)index,
                index,
                -index,
                index);
            CandidateInput input = inputs[index];
            interleaved[index] = CreateCandidate(input.X, input.Z, input.Ordinal);
        }

        var parallel = new VegetationScatterCandidate[inputs.Length];
        Parallel.For(0, inputs.Length, index =>
        {
            CandidateInput input = inputs[index];
            parallel[index] = CreateCandidate(input.X, input.Z, input.Ordinal);
        });

        Assert.Equal(baseline, interleaved);
        Assert.Equal(baseline, parallel);
    }

    [Fact]
    public void CandidateSequence_RejectsNonCanonicalEntryIds()
    {
        string[] invalidEntryIds = ["", " padded", "padded ", "two words", "\u4e2d\u6587"];
        foreach (string entryId in invalidEntryIds)
        {
            Assert.Throws<ArgumentException>(() => VegetationScatterCandidateSequence.Create(
                s_WorldGuid,
                s_BiomeGuid,
                TerrainRuntimeTestData.RootGuid,
                s_SpeciesGuid,
                entryId,
                GlobalSeed,
                SeedSalt,
                0,
                0,
                0));
        }
    }

    [Fact]
    public void Bake_SignedTerrainAndReversedTileInputProduceIdenticalDescriptors()
    {
        ScatterFixture fixture = CreateFixture();
        VegetationScatterBakeResult forward = VegetationScatterBaker.Build(
            CreateDescriptor(fixture));
        VegetationScatterBakeResult reversed = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, tiles: fixture.Tiles.Reverse().ToArray()));
        var scheduled = new VegetationScatterBakeResult[8];
        Parallel.For(0, scheduled.Length, index =>
        {
            IReadOnlyList<CookedTerrainTile> tiles = (index & 1) == 0
                ? fixture.Tiles
                : fixture.Tiles.Reverse().ToArray();
            scheduled[index] = VegetationScatterBaker.Build(
                CreateDescriptor(fixture, tiles: tiles));
        });

        Assert.Equal(4, forward.Metrics.AcceptedCount);
        AssertBakeEquivalent(forward, reversed);
        Assert.All(scheduled, result => AssertBakeEquivalent(forward, result));
    }

    [Fact]
    public void Bake_TerrainAltitudeSlopeAndNormalizedLayerRulesFilterAcceptedPlacement()
    {
        ScatterFixture fixture = CreateFixture();
        VegetationScatterBakeResult baseline = VegetationScatterBaker.Build(
            CreateDescriptor(fixture));
        SurfaceObservation[] observations = Observe(fixture, baseline);
        Assert.Equal(4, observations.Length);

        float altitudeCut = ChooseInteriorCut(
            observations.Select(item => checked((float)item.Position.Y)));
        CookedVegetationBiomeEntry altitudeEntry = fixture.Entry with
        {
            AltitudeRange = new VegetationValueRange(-1000.0f, altitudeCut)
        };
        AssertFilteredKeys(
            fixture,
            altitudeEntry,
            observations.Where(item => item.Position.Y <= altitudeCut).Select(item => item.StableKey));

        float slopeCut = ChooseInteriorCut(observations.Select(item => item.SlopeDegrees));
        CookedVegetationBiomeEntry slopeEntry = fixture.Entry with
        {
            SlopeRangeDegrees = new VegetationValueRange(0.0f, slopeCut)
        };
        AssertFilteredKeys(
            fixture,
            slopeEntry,
            observations.Where(item => item.SlopeDegrees <= slopeCut).Select(item => item.StableKey));

        float weightCut = ChooseInteriorCut(observations.Select(item => item.RockWeight));
        CookedVegetationBiomeEntry layerEntry = fixture.Entry with
        {
            LayerWeightRules =
            [
                new CookedVegetationLayerWeightRule(
                    "rock",
                    new VegetationValueRange(weightCut, 1.0f))
            ]
        };
        AssertFilteredKeys(
            fixture,
            layerEntry,
            observations.Where(item => item.RockWeight >= weightCut).Select(item => item.StableKey));
        Assert.All(observations, item => Assert.InRange(item.RockWeight, 0.0f, 1.0f));
    }

    [Fact]
    public void Bake_NegativeWorldCellsUseHalfOpenPositiveBorderOwnership()
    {
        ScatterFixture fixture = CreateFixture();
        WorldCellKey[] cells =
        [
            new(new WorldCellCoordinate(-1, 0, -1), "surface"),
            new(new WorldCellCoordinate(0, 0, -1), "surface"),
            new(new WorldCellCoordinate(-1, 0, 0), "surface"),
            new(new WorldCellCoordinate(0, 0, 0), "surface")
        ];
        var allKeys = new HashSet<ulong>();

        foreach (WorldCellKey cell in cells)
        {
            VegetationScatterBakeResult result = VegetationScatterBaker.Build(
                CreateDescriptor(fixture, cell: cell));
            WorldPosition origin = WorldPartitionCoordinates.GetCellOrigin(s_Partition, cell.Coordinate);
            foreach (VegetationCookedInstanceInput instance in result.Cluster.Pages[0].Instances)
            {
                WorldPosition position = ToWorld(result.Cluster.Pages[0].Origin, instance.LocalPosition);
                Assert.Equal(
                    cell.Coordinate,
                    WorldPartitionCoordinates.GetCoordinate(s_Partition, position));
                Assert.InRange(position.X, origin.X, origin.X + s_Partition.CellSize.X);
                Assert.InRange(position.Z, origin.Z, origin.Z + s_Partition.CellSize.Z);
                Assert.True(position.X < origin.X + s_Partition.CellSize.X);
                Assert.True(position.Z < origin.Z + s_Partition.CellSize.Z);
                Assert.True(allKeys.Add(instance.StableKey), $"Duplicate stable key {instance.StableKey}.");
            }
        }

        Assert.Equal(16, allKeys.Count);
    }

    [Fact]
    public void Bake_CanonicalizesFloatRoundedPositiveBorderOwnershipBeforeStorage()
    {
        ScatterFixture fixture = CreateFixture();
        VegetationScatterCandidate target = CreateCandidate(-1, -1, 0);
        const double floatBoundaryQuantum = 1.0 / 16_777_216.0;
        double positiveBorder = Math.Ceiling(target.WorldX / floatBoundaryQuantum) *
            floatBoundaryQuantum;
        if (positiveBorder == target.WorldX)
        {
            positiveBorder += floatBoundaryQuantum;
        }
        var partition = new WorldPartitionSettings(
            new WorldPosition(positiveBorder, 0.0, 0.0),
            new WorldPosition(2.0, 128.0, 2.0),
            LoadRadius: 1,
            UnloadHysteresis: 1,
            MaxActiveCells: 16);
        var negativeCell = new WorldCellKey(new WorldCellCoordinate(-1, 0, -1), "surface");
        var positiveCell = new WorldCellKey(new WorldCellCoordinate(0, 0, -1), "surface");
        Assert.Equal(
            negativeCell.Coordinate,
            WorldPartitionCoordinates.GetCoordinate(
                partition,
                new WorldPosition(target.WorldX, 10.0, target.WorldZ)));

        VegetationScatterBakeResult negative = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, cell: negativeCell, partition: partition));
        VegetationScatterBakeResult positive = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, cell: positiveCell, partition: partition));
        Assert.DoesNotContain(
            negative.Cluster.Pages[0].Instances,
            instance => instance.StableKey == target.StableKey);
        VegetationCookedInstanceInput stored = Assert.Single(
            positive.Cluster.Pages[0].Instances,
            instance => instance.StableKey == target.StableKey);
        WorldPosition storedWorld = ToWorld(positive.Cluster.Pages[0].Origin, stored.LocalPosition);
        Assert.Equal(positiveBorder, storedWorld.X);
        Assert.Equal(
            positiveCell.Coordinate,
            WorldPartitionCoordinates.GetCoordinate(partition, storedWorld));
        AssertMetricsReconcile(negative.Metrics);
        AssertMetricsReconcile(positive.Metrics);
    }

    [Fact]
    public void Bake_HardAndSoftExclusionsFollowEntryPolicy()
    {
        ScatterFixture fixture = CreateFixture();
        VegetationScatterBakeResult baseline = VegetationScatterBaker.Build(
            CreateDescriptor(fixture));
        VegetationCookedInstanceInput target = baseline.Cluster.Pages[0].Instances[0];
        WorldPosition targetPosition = ToWorld(
            baseline.Cluster.Pages[0].Origin,
            target.LocalPosition);
        const double epsilon = 0.00001;
        WorldBounds bounds = new(
            new WorldPosition(
                targetPosition.X - epsilon,
                targetPosition.Y - epsilon,
                targetPosition.Z - epsilon),
            new WorldPosition(
                targetPosition.X + epsilon,
                targetPosition.Y + epsilon,
                targetPosition.Z + epsilon));

        VegetationScatterBakeResult respectedSoft = VegetationScatterBaker.Build(
            CreateDescriptor(
                fixture,
                exclusions: [new VegetationScatterExclusion(bounds, VegetationScatterExclusionKind.Soft)]));
        Assert.Equal(baseline.Metrics.AcceptedCount - 1, respectedSoft.Metrics.AcceptedCount);
        Assert.Equal(1, respectedSoft.Metrics.ExclusionRejectedCount);

        CookedVegetationBiomeEntry ignoreSoftEntry = fixture.Entry with
        {
            ExclusionPolicy = VegetationExclusionPolicy.IgnoreSoft
        };
        ScatterFixture ignoreSoft = WithEntry(fixture, ignoreSoftEntry);
        VegetationScatterBakeResult ignoredSoft = VegetationScatterBaker.Build(
            CreateDescriptor(
                ignoreSoft,
                exclusions: [new VegetationScatterExclusion(bounds, VegetationScatterExclusionKind.Soft)]));
        Assert.Equal(
            baseline.Cluster.Pages[0].Instances.Select(instance => instance.StableKey),
            ignoredSoft.Cluster.Pages[0].Instances.Select(instance => instance.StableKey));
        Assert.Equal(0, ignoredSoft.Metrics.ExclusionRejectedCount);

        VegetationScatterBakeResult respectedHard = VegetationScatterBaker.Build(
            CreateDescriptor(
                ignoreSoft,
                exclusions: [new VegetationScatterExclusion(bounds, VegetationScatterExclusionKind.Hard)]));
        Assert.Equal(baseline.Metrics.AcceptedCount - 1, respectedHard.Metrics.AcceptedCount);
        Assert.Equal(1, respectedHard.Metrics.ExclusionRejectedCount);
    }

    [Fact]
    public void Bake_ReportsCandidateAndOnePageAcceptedOverflowWithoutTruncation()
    {
        ScatterFixture fixture = CreateFixture();
        ScatterFixture candidateOverflow = WithEntry(
            fixture,
            fixture.Entry with { Density = 116_509.0f });

        InvalidOperationException candidateError = Assert.Throws<InvalidOperationException>(() =>
            VegetationScatterBaker.Build(CreateDescriptor(candidateOverflow)));
        Assert.Contains("Candidate count", candidateError.Message, StringComparison.Ordinal);
        Assert.Contains("exceeds bounded limit", candidateError.Message, StringComparison.Ordinal);
        Assert.Contains(
            VegetationScatterBaker.MaximumCandidateCountPerBake.ToString(),
            candidateError.Message,
            StringComparison.Ordinal);

        ScatterFixture acceptedOverflow = WithEntry(
            fixture,
            fixture.Entry with { Density = 2.0f, ClusterSize = 1 });
        InvalidOperationException acceptedError = Assert.Throws<InvalidOperationException>(() =>
            VegetationScatterBaker.Build(CreateDescriptor(acceptedOverflow)));
        Assert.Contains("accepted instance count", acceptedError.Message, StringComparison.Ordinal);
        Assert.Contains("one-page limit '1'", acceptedError.Message, StringComparison.Ordinal);
        Assert.Contains(EntryId, acceptedError.Message, StringComparison.Ordinal);

        ScatterFixture radiusOverflow = fixture with
        {
            Species = fixture.Species with
            {
                ScaleRange = new VegetationValueRange(2.0f, 2.0f)
            }
        };
        InvalidOperationException radiusError = Assert.Throws<InvalidOperationException>(() =>
            VegetationScatterBaker.Build(
                CreateDescriptor(radiusOverflow, conservativeRadius: float.MaxValue)));
        Assert.Contains("conservative radius overflows", radiusError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bake_MaximumBoundedCandidateSetUsesIncrementalSpacing()
    {
        ScatterFixture fixture = CreateFixture();
        ScatterFixture dense = WithEntry(
            fixture,
            fixture.Entry with
            {
                Density = 65_536.0f,
                MinimumSpacing = VegetationBiomeSourceAssetLoader.MaximumSpacing
            });
        var partition = new WorldPartitionSettings(
            new WorldPosition(-2.0, 0.0, -2.0),
            new WorldPosition(4.0, 128.0, 4.0),
            LoadRadius: 1,
            UnloadHysteresis: 1,
            MaxActiveCells: 1);
        var cell = new WorldCellKey(new WorldCellCoordinate(0, 0, 0), "surface");

        VegetationScatterBakeResult result = VegetationScatterBaker.Build(
            CreateDescriptor(dense, partition: partition, cell: cell));

        Assert.Equal(VegetationScatterBaker.MaximumCandidateCountPerBake, result.Metrics.CandidateCount);
        Assert.Equal(1, result.Metrics.AcceptedCount);
        Assert.Equal(result.Metrics.CandidateCount - 1, result.Metrics.SpacingRejectedCount);
        AssertMetricsReconcile(result.Metrics);
    }

    [Fact]
    public void Bake_PageIdentityUsesCodecCanonicalDerivedRadius()
    {
        ScatterFixture fixture = CreateFixture();
        fixture = fixture with
        {
            Species = fixture.Species with
            {
                ScaleRange = new VegetationValueRange(0.001f, 0.001f)
            }
        };
        const float firstRadius = 1000.00006f;
        const float secondRadius = 1000.00012f;

        VegetationScatterBakeResult first = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, conservativeRadius: firstRadius));
        VegetationScatterBakeResult second = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, conservativeRadius: secondRadius));

        Assert.NotEqual(
            BitConverter.SingleToInt32Bits(firstRadius),
            BitConverter.SingleToInt32Bits(secondRadius));
        Assert.Equal(first.Cluster.Pages[0].Instances, second.Cluster.Pages[0].Instances);
        Assert.Equal(first.PlacementContentHash, second.PlacementContentHash);
        Assert.Equal(first.Cluster.Pages[0].Guid, second.Cluster.Pages[0].Guid);
    }

    [Fact]
    public void Bake_UsesStableWorldCellAndContentDerivedIdentities()
    {
        ScatterFixture fixture = CreateFixture();
        VegetationScatterBakeResult result = VegetationScatterBaker.Build(
            CreateDescriptor(fixture));
        Guid expectedCluster = VegetationScatterIdentity.CreateClusterGuid(
            s_BiomeGuid,
            PackageId,
            s_WorldGuid,
            fixture.Terrain.Root.Guid,
            fixture.Terrain.Root.PackageId,
            s_SpeciesGuid,
            fixture.Species.PackageId,
            EntryId,
            s_NegativeCell);
        Guid expectedPage = VegetationScatterIdentity.CreatePageGuid(
            expectedCluster,
            PackageId,
            pageIndex: 0,
            result.PlacementContentHash);

        Assert.Equal(expectedCluster, result.Cluster.Guid);
        Assert.Equal(expectedCluster, result.ClusterMetadata.Guid);
        Assert.Equal(expectedPage, result.Cluster.Pages[0].Guid);
        Assert.Equal(expectedPage, Assert.Single(result.PageMetadata).Guid);
        Assert.NotNull(result.ClusterMetadata.Generated);
        Assert.NotNull(result.PageMetadata[0].Generated);

        VegetationScatterBakeResult changedContent = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, conservativeRadius: ConservativeRadius + 0.25f));
        Assert.Equal(result.Cluster.Guid, changedContent.Cluster.Guid);
        Assert.False(result.PlacementContentHash.SequenceEqual(changedContent.PlacementContentHash));
        Assert.NotEqual(result.Cluster.Pages[0].Guid, changedContent.Cluster.Pages[0].Guid);

        WorldCellKey adjacent = new(new WorldCellCoordinate(0, 0, -1), "surface");
        VegetationScatterBakeResult changedCell = VegetationScatterBaker.Build(
            CreateDescriptor(fixture, cell: adjacent));
        Assert.NotEqual(result.Cluster.Guid, changedCell.Cluster.Guid);

        const string movedSpeciesPackage = "com.arisen.vegetation.scatter-moved";
        CookedVegetationSpecies movedSpecies = fixture.Species with
        {
            PackageId = movedSpeciesPackage
        };
        CookedVegetationBiomeEntry movedEntry = fixture.Entry with
        {
            Species = new CookedVegetationSpeciesReference(
                fixture.Species.Guid,
                movedSpeciesPackage)
        };
        ScatterFixture movedFixture = fixture with
        {
            Species = movedSpecies,
            Entry = movedEntry,
            Biome = fixture.Biome with { Entries = Array.AsReadOnly([movedEntry]) }
        };
        VegetationScatterBakeResult moved = VegetationScatterBaker.Build(
            CreateDescriptor(movedFixture));
        Assert.NotEqual(result.Cluster.Guid, moved.Cluster.Guid);
        Assert.NotEqual(result.Cluster.Pages[0].Guid, moved.Cluster.Pages[0].Guid);
        Assert.False(result.PlacementContentHash.SequenceEqual(moved.PlacementContentHash));
        Assert.Equal(
            result.Cluster.Pages[0].Instances.Select(instance => instance.StableKey),
            moved.Cluster.Pages[0].Instances.Select(instance => instance.StableKey));

        Assert.Equal(Guid.Parse("df8f0fce-580e-efc2-f30c-f103c75f7b13"), result.Cluster.Guid);
        Assert.Equal(Guid.Parse("ee7cda06-91dc-cc9c-7756-73959207ad83"), result.Cluster.Pages[0].Guid);
        string placementHash = Convert.ToHexString(result.PlacementContentHash);
        Assert.Equal("7D35213BC15B07D0FE153B82B87293B5", placementHash[..32]);
        Assert.Equal("C85C0EFC22825EB9DE5C8AEFB7C76D97", placementHash[32..]);
    }

    [Fact]
    public void Bake_PublishesThroughClusterCookerAndRejectsRehashedPageCorruption()
    {
        ScatterFixture fixture = CreateFixture();
        VegetationScatterBakeResult result = VegetationScatterBaker.Build(
            CreateDescriptor(fixture));
        using var publication = new ScatterPublicationFixture(result, fixture);

        CookedVegetationClusterArtifact clusterArtifact = VegetationClusterAssetCooker.Cook(
            publication.Database,
            result.Cluster);
        Assert.True(File.Exists(clusterArtifact.Path));
        Guid pageGuid = result.Cluster.Pages[0].Guid;
        Assert.True(publication.Database.TryGetCookedArtifact(
            pageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out CookedAssetRecord pageArtifact));
        var clusterRef = new AssetRef<VegetationClusterSourceAsset>(
            result.Cluster.Guid,
            VegetationAssetTypes.Cluster,
            PackageId);
        Assert.True(
            VegetationClusterAssetCooker.TryLoadCooked(
                publication.Database,
                clusterRef,
                out CookedVegetationCluster loadedCluster,
                out string loadedClusterDiagnostic),
            loadedClusterDiagnostic);
        CookedVegetationInstancePageReference loadedReference = Assert.Single(loadedCluster.Pages);
        Assert.True(
            VegetationInstancePageAssetCooker.TryLoadCookedForCluster(
                publication.Database,
                new AssetRef<VegetationInstancePageSourceAsset>(
                    pageGuid,
                    VegetationAssetTypes.InstancePage,
                    PackageId),
                result.Cluster.Guid,
                out CookedVegetationInstancePage loadedPage,
                out string loadedPageDiagnostic),
            loadedPageDiagnostic);
        Assert.Equal(result.Metrics.AcceptedCount, loadedPage.Instances.Count);
        Assert.Equal(
            loadedReference.ContentHash,
            SHA256.HashData(File.ReadAllBytes(pageArtifact.Path)));
        byte[] corrupted = File.ReadAllBytes(pageArtifact.Path);
        int instancesOffset = FindPageSectionOffset(corrupted, sectionType: 4);
        BinaryPrimitives.WriteUInt32LittleEndian(
            corrupted.AsSpan(instancesOffset + 12),
            0x7fc00000U);
        RehashPage(corrupted);

        using (CookedArtifactWrite write = publication.Database.BeginCookedArtifactWrite(
                   pageGuid,
                   VegetationInstancePageAssetCooker.RuntimeVariant,
                   VegetationInstancePageAssetCooker.CookedExtension))
        {
            File.WriteAllBytes(write.OutputPath, corrupted);
            write.Commit(VegetationAssetTypes.InstancePage);
        }

        Assert.False(VegetationInstancePageAssetCooker.TryReadPayload(
            pageGuid,
            result.Cluster.Guid,
            PackageId,
            corrupted,
            "rehashed-scatter-page",
            out _,
            out string pageDiagnostic));
        Assert.False(string.IsNullOrWhiteSpace(pageDiagnostic));
        Assert.Contains("non-finite", pageDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(VegetationClusterAssetCooker.TryLoadCooked(
            publication.Database,
            clusterRef,
            out _,
            out string clusterDiagnostic));
        Assert.Contains("instance page", clusterDiagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-finite", clusterDiagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static VegetationScatterCandidate CreateCandidate(long x, long z, int ordinal) =>
        VegetationScatterCandidateSequence.Create(
            s_WorldGuid,
            s_BiomeGuid,
            TerrainRuntimeTestData.RootGuid,
            s_SpeciesGuid,
            EntryId,
            GlobalSeed,
            SeedSalt,
            x,
            z,
            ordinal);

    private static ScatterFixture CreateFixture()
    {
        TerrainRuntimeFixture source = TerrainRuntimeTestData.Create(
            tileCountX: 2,
            tileCountZ: 2,
            resolution: 3,
            tileOrigin: new TerrainTileCoordinate(-1, -1),
            worldPlacement: new WorldPosition(-2.0, 10.0, -2.0),
            sampleSpacing: new TerrainSampleSpacing(1.0, 1.0),
            height: static (x, z) => checked((ushort)(
                4_000 + (x * x * 1_800) + (z * z * 900) + (x * z * 300))),
            weights: static (x, z) =>
            {
                int rock = 35 + (x * 30) + (z * 15);
                return (checked((byte)(255 - rock)), checked((byte)rock), (byte)0, (byte)0);
            });
        CookedTerrainLayer ground = source.Root.Layers[0] with { Id = "ground" };
        CookedTerrainLayer rock = ground with { Id = "rock" };
        CookedTerrainRoot root = source.Root with
        {
            Layers = Array.AsReadOnly([ground, rock])
        };
        CookedTerrainTile[] tiles = source.Tiles
            .Select(CloneAsTwoLayerTile)
            .ToArray();
        var species = new CookedVegetationSpecies(
            s_SpeciesGuid,
            PackageId,
            SourceSchemaVersion: 1,
            "Scatter Test Species",
            [new CookedVegetationSpeciesLod(
                new CookedVegetationMeshReference(s_MeshGuid, PackageId),
                new CookedVegetationMaterialReference(s_MaterialGuid, PackageId),
                MaximumDistance: 200.0f,
                MaximumScreenError: 4.0f)],
            VegetationShadowPolicy.Cast,
            new VegetationValueRange(0.75f, 1.25f),
            new VegetationValueRange(0.0f, 360.0f),
            new VegetationValueRange(-8.0f, 8.0f),
            new VegetationCollisionPromotionDescriptor(
                VegetationCollisionPromotionMode.None,
                0.0f,
                0.0f,
                0.0f),
            WindResponse: 0.1f);
        CookedVegetationBiomeEntry entry = CreateEntry();
        var biome = new CookedVegetationBiome(
            s_BiomeGuid,
            PackageId,
            SourceSchemaVersion: 1,
            "Scatter Test Biome",
            GlobalSeed,
            Array.AsReadOnly([entry]));
        return new ScatterFixture(root, tiles, species, biome, entry);
    }

    private static CookedTerrainTile CloneAsTwoLayerTile(CookedTerrainTile tile) => new(
        tile.Guid,
        tile.RootGuid,
        tile.LayerSetGuid,
        tile.PackageId,
        tile.SourceSchemaVersion,
        tile.Coordinate,
        tile.Resolution,
        layerCount: 2,
        tile.WorldPlacement,
        tile.SampleSpacing,
        tile.HeightRange,
        tile.MinHeight,
        tile.MaxHeight,
        tile.BorderPolicy,
        tile.SourceSampleOffsetX,
        tile.SourceSampleOffsetZ,
        tile.Heights.ToArray(),
        tile.LayerWeights.ToArray(),
        tile.GeometricErrors.ToArray());

    private static CookedVegetationBiomeEntry CreateEntry() => new(
        EntryId,
        new CookedVegetationSpeciesReference(s_SpeciesGuid, PackageId),
        Density: 1.0f,
        SeedSalt,
        new VegetationValueRange(-1000.0f, 5000.0f),
        new VegetationValueRange(0.0f, 90.0f),
        Array.Empty<CookedVegetationLayerWeightRule>(),
        MinimumSpacing: 0.001f,
        ClusterSize: 64,
        VegetationExclusionPolicy.Respect);

    private static ScatterFixture WithEntry(
        ScatterFixture fixture,
        CookedVegetationBiomeEntry entry) => fixture with
    {
        Entry = entry,
        Biome = fixture.Biome with { Entries = Array.AsReadOnly([entry]) }
    };

    private static VegetationScatterBakeDescriptor CreateDescriptor(
        ScatterFixture fixture,
        IReadOnlyList<CookedTerrainTile>? tiles = null,
        WorldCellKey? cell = null,
        IReadOnlyList<VegetationScatterExclusion>? exclusions = null,
        float conservativeRadius = ConservativeRadius,
        WorldPartitionSettings? partition = null) => new(
        s_WorldGuid,
        fixture.Biome,
        fixture.Species,
        fixture.Terrain.Root,
        tiles ?? fixture.Tiles,
        partition ?? s_Partition,
            cell ?? s_NegativeCell,
            EntryId,
            conservativeRadius,
            exclusions ?? Array.Empty<VegetationScatterExclusion>());

    private static SurfaceObservation[] Observe(
        ScatterFixture fixture,
        VegetationScatterBakeResult result)
    {
        var sampler = new CookedTerrainSurfaceSampler(fixture.Terrain.Root, fixture.Tiles);
        VegetationInstancePageCookDescriptor page = result.Cluster.Pages[0];
        return page.Instances.Select(instance =>
        {
            WorldPosition position = ToWorld(page.Origin, instance.LocalPosition);
            Assert.True(sampler.TrySample(position, out CookedTerrainSurfaceSample sample));
            float slope = MathF.Acos(Math.Clamp(sample.Normal.Y, -1.0f, 1.0f)) *
                (180.0f / MathF.PI);
            return new SurfaceObservation(
                instance.StableKey,
                position,
                slope,
                sample.LayerWeights.Y);
        }).ToArray();
    }

    private static void AssertFilteredKeys(
        ScatterFixture fixture,
        CookedVegetationBiomeEntry entry,
        IEnumerable<ulong> expectedKeys)
    {
        ulong[] expected = expectedKeys.Order().ToArray();
        Assert.NotEmpty(expected);
        Assert.True(expected.Length < 4);
        ScatterFixture filteredFixture = WithEntry(fixture, entry);
        VegetationScatterBakeResult filtered = VegetationScatterBaker.Build(
            CreateDescriptor(filteredFixture));
        Assert.Equal(
            expected,
            filtered.Cluster.Pages[0].Instances.Select(instance => instance.StableKey).ToArray());
        Assert.True(filtered.Metrics.RuleRejectedCount > 0);
    }

    private static float ChooseInteriorCut(IEnumerable<float> values)
    {
        float[] ordered = values.Order().ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            if (ordered[index] - ordered[index - 1] > 0.0001f)
            {
                return ordered[index - 1] + ((ordered[index] - ordered[index - 1]) * 0.5f);
            }
        }

        throw new InvalidOperationException("Scatter fixture values do not provide an interior rule split.");
    }

    private static void AssertBakeEquivalent(
        VegetationScatterBakeResult expected,
        VegetationScatterBakeResult actual)
    {
        Assert.Equal(expected.PlacementContentHash, actual.PlacementContentHash);
        Assert.Equal(expected.Metrics, actual.Metrics);
        Assert.Equal(expected.Cluster.Guid, actual.Cluster.Guid);
        Assert.Equal(expected.Cluster.PackageId, actual.Cluster.PackageId);
        Assert.Equal(expected.Cluster.GeneratedSchemaVersion, actual.Cluster.GeneratedSchemaVersion);
        Assert.Equal(expected.Cluster.Biome, actual.Cluster.Biome);
        Assert.Equal(expected.ClusterMetadata.Guid, actual.ClusterMetadata.Guid);
        Assert.Equal(expected.ClusterMetadata.Generated!.ChildKey, actual.ClusterMetadata.Generated!.ChildKey);
        Assert.Equal(expected.PageMetadata[0].Guid, actual.PageMetadata[0].Guid);
        Assert.Equal(expected.PageMetadata[0].Generated!.ChildKey, actual.PageMetadata[0].Generated!.ChildKey);
        VegetationInstancePageCookDescriptor expectedPage = Assert.Single(expected.Cluster.Pages);
        VegetationInstancePageCookDescriptor actualPage = Assert.Single(actual.Cluster.Pages);
        Assert.Equal(expectedPage.Guid, actualPage.Guid);
        Assert.Equal(expectedPage.ClusterGuid, actualPage.ClusterGuid);
        Assert.Equal(expectedPage.PackageId, actualPage.PackageId);
        Assert.Equal(expectedPage.GeneratedSchemaVersion, actualPage.GeneratedSchemaVersion);
        Assert.Equal(expectedPage.Origin, actualPage.Origin);
        Assert.Equal(expectedPage.Species, actualPage.Species);
        Assert.Equal(expectedPage.Instances, actualPage.Instances);
    }

    private static void AssertMetricsReconcile(VegetationScatterBakeMetrics metrics)
    {
        Assert.Equal(
            metrics.CandidateCount,
            checked(
                metrics.DensityRejectedCount +
                metrics.TerrainRejectedCount +
                metrics.RuleRejectedCount +
                metrics.ExclusionRejectedCount +
                metrics.SpacingRejectedCount +
                metrics.OwnershipRejectedCount +
                metrics.AcceptedCount));
    }

    private static WorldPosition ToWorld(WorldPosition origin, Vector3 local) => new(
        origin.X + local.X,
        origin.Y + local.Y,
        origin.Z + local.Z);

    private static CandidateGolden ToGolden(VegetationScatterCandidate candidate) => new(
        candidate.SpatialCellX,
        candidate.SpatialCellZ,
        candidate.Ordinal,
        candidate.StableKey,
        Bits(candidate.WorldX),
        Bits(candidate.WorldZ),
        Bits(candidate.DensityRank),
        Bits(candidate.ScaleRank),
        Bits(candidate.YawRank),
        Bits(candidate.TiltRank));

    private static ulong Bits(double value) =>
        unchecked((ulong)BitConverter.DoubleToInt64Bits(value));

    private static string FormatGolden(CandidateGolden value) =>
        $"new({value.SpatialCellX}L, {value.SpatialCellZ}L, {value.Ordinal}, " +
        $"0x{value.StableKey:x16}UL, 0x{value.WorldXBits:x16}UL, " +
        $"0x{value.WorldZBits:x16}UL, 0x{value.DensityBits:x16}UL, " +
        $"0x{value.ScaleBits:x16}UL, 0x{value.YawBits:x16}UL, " +
        $"0x{value.TiltBits:x16}UL)";

    private static int FindPageSectionOffset(byte[] bytes, uint sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(104));
        for (int index = 0; index < sectionCount; index++)
        {
            int descriptorOffset = VegetationInstancePageAssetCooker.HeaderSize +
                (index * VegetationCookedContainer.SectionDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(descriptorOffset)) == sectionType)
            {
                return checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
                    bytes.AsSpan(descriptorOffset + 8)));
            }
        }

        throw new InvalidOperationException($"Page section '{sectionType}' was not found.");
    }

    private static void RehashPage(byte[] bytes)
    {
        SHA256.HashData(bytes.AsSpan(VegetationInstancePageAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(
                VegetationInstancePageAssetCooker.HashOffset,
                VegetationCookedContainer.HashSize));
    }

    private sealed class ScatterPublicationFixture : IDisposable
    {
        public ScatterPublicationFixture(
            VegetationScatterBakeResult result,
            ScatterFixture fixture)
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationScatterBakerTests",
                Guid.NewGuid().ToString("N"));
            Database = new TestAssetDatabase(
                AssetSourceAccessMode.RuntimeAssetCook,
                Path.Combine(Root, "Cooked"));
            AddAsset(s_BiomeGuid, VegetationAssetTypes.Biome, "biome.arivegetationbiome", CreateBiomeSource(fixture));
            AddAsset(s_SpeciesGuid, VegetationAssetTypes.Species, "species.arivegetationspecies", "fixture species");
            AddAsset(s_MeshGuid, "Mesh", "mesh.asset", "fixture mesh");
            AddAsset(s_MaterialGuid, "Material", "material.asset", "fixture material");
            AddAsset(result.Cluster.Guid, VegetationAssetTypes.Cluster, "cluster.generated", "fixture cluster");
            AddAsset(result.Cluster.Pages[0].Guid, VegetationAssetTypes.InstancePage, "page.generated", "fixture page");
            using CookedArtifactWrite speciesWrite = Database.BeginCookedArtifactWrite(
                s_SpeciesGuid,
                VegetationSpeciesAssetCooker.RuntimeVariant,
                VegetationSpeciesAssetCooker.CookedExtension);
            File.WriteAllBytes(
                speciesWrite.OutputPath,
                VegetationSpeciesAssetCooker.WritePayload(fixture.Species));
            speciesWrite.Commit(VegetationAssetTypes.Species);
            using CookedArtifactWrite biomeWrite = Database.BeginCookedArtifactWrite(
                s_BiomeGuid,
                VegetationBiomeAssetCooker.RuntimeVariant,
                VegetationBiomeAssetCooker.CookedExtension);
            File.WriteAllBytes(
                biomeWrite.OutputPath,
                VegetationBiomeAssetCooker.WritePayload(fixture.Biome));
            biomeWrite.Commit(VegetationAssetTypes.Biome);
        }

        public string Root { get; }

        public TestAssetDatabase Database { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private void AddAsset(Guid guid, string assetType, string fileName, string contents)
        {
            string path = Path.Combine(Root, "Sources", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            Database.AddAsset(guid, assetType, path, PackageId);
        }

        private static string CreateBiomeSource(ScatterFixture fixture) => $$"""
            Version: 1
            BiomeGuid: {{s_BiomeGuid:D}}
            Name: Scatter Test Biome
            GlobalSeed: {{GlobalSeed}}
            Entries:
            - EntryId: {{EntryId}}
              Species: { Guid: {{s_SpeciesGuid:D}}, PackageId: {{PackageId}} }
              Density: {{fixture.Entry.Density}}
              SeedSalt: {{SeedSalt}}
              AltitudeRange: { Minimum: -1000.0, Maximum: 5000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 90.0 }
              LayerWeightRules: []
              MinimumSpacing: {{fixture.Entry.MinimumSpacing}}
              ClusterSize: {{fixture.Entry.ClusterSize}}
              ExclusionPolicy: {{fixture.Entry.ExclusionPolicy}}
            """;
    }

    private readonly record struct CandidateInput(long X, long Z, int Ordinal);

    private readonly record struct CandidateGolden(
        long SpatialCellX,
        long SpatialCellZ,
        int Ordinal,
        ulong StableKey,
        ulong WorldXBits,
        ulong WorldZBits,
        ulong DensityBits,
        ulong ScaleBits,
        ulong YawBits,
        ulong TiltBits);

    private readonly record struct SurfaceObservation(
        ulong StableKey,
        WorldPosition Position,
        float SlopeDegrees,
        float RockWeight);

    private sealed record ScatterFixture(
        CookedTerrainRoot Root,
        CookedTerrainTile[] Tiles,
        CookedVegetationSpecies Species,
        CookedVegetationBiome Biome,
        CookedVegetationBiomeEntry Entry)
    {
        public TerrainRuntimeFixture Terrain => new(Root, Tiles);
    }
}
