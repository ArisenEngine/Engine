using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using ArisenEngine.Core.Assets;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationCookedClusterTests
{
    private static readonly Guid s_PageBGuid =
        Guid.Parse("c1200000-0000-0000-0000-000000000002");
    private static readonly Guid s_PageCGuid =
        Guid.Parse("c1200000-0000-0000-0000-000000000003");
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("c1400000-0000-0000-0000-000000000001");
    private const string ForeignPackageId = "com.arisen.vegetation.foreign";

    [Fact]
    public void ClusterV1_RoundTripsCanonicalPagesWithExactHashPinsAndDependencyDag()
    {
        using var fixture = new ClusterDiskFixture();
        AssetDatabase database = fixture.CreateDatabase();
        VegetationClusterCookDescriptor descriptor = CreateClusterDescriptor(
            reversePages: true,
            permutePageContents: true);

        CookedVegetationClusterArtifact artifact = VegetationClusterAssetCooker.Cook(
            database,
            descriptor);
        byte[] bytes = File.ReadAllBytes(artifact.Path);

        Assert.True(
            VegetationClusterAssetCooker.TryReadPayload(
                VegetationCookedInstancePageTests.ClusterGuid,
                VegetationCookedInstancePageTests.PackageId,
                bytes,
                "cluster-memory",
                out CookedVegetationCluster loaded,
                out string diagnostic),
            diagnostic);
        Assert.Equal(2, loaded.Pages.Count);
        Assert.Equal(6, loaded.InstanceCount);
        Assert.Equal(
            [VegetationCookedInstancePageTests.PageGuid, s_PageBGuid],
            loaded.Pages.Select(page => page.Guid));
        Assert.Equal(
            [
                VegetationCookedInstancePageTests.SpeciesAGuid,
                VegetationCookedInstancePageTests.SpeciesBGuid
            ],
            loaded.Species.Select(species => species.Guid));
        Assert.Equal(s_BiomeGuid, loaded.Biome.Guid);
        Assert.Equal(2, artifact.PageCount);
        Assert.Equal(6, artifact.InstanceCount);
        Assert.Equal(loaded.Bounds, artifact.Bounds);

        foreach (CookedVegetationInstancePageReference reference in loaded.Pages)
        {
            Assert.True(database.TryGetCookedArtifact(
                reference.Guid,
                VegetationInstancePageAssetCooker.RuntimeVariant,
                out CookedAssetRecord pageArtifact));
            byte[] pageBytes = File.ReadAllBytes(pageArtifact.Path);
            Assert.Equal(pageBytes.LongLength, reference.SizeInBytes);
            Assert.Equal(SHA256.HashData(pageBytes), reference.ContentHash);
        }

        VegetationCookedAssetDependency[] dependencies =
            VegetationClusterAssetCooker.BuildDependencies(loaded);
        Assert.Equal(5, dependencies.Length);
        Assert.Single(dependencies, dependency =>
            dependency.Guid == s_BiomeGuid &&
            dependency.AssetType == VegetationAssetTypes.Biome &&
            dependency.Variant == VegetationBiomeAssetCooker.RuntimeVariant);
        Assert.Equal(
            2,
            dependencies.Count(dependency =>
                dependency.AssetType == VegetationAssetTypes.Species &&
                dependency.Variant == VegetationSpeciesAssetCooker.RuntimeVariant));
        Assert.Equal(
            2,
            dependencies.Count(dependency =>
                dependency.AssetType == VegetationAssetTypes.InstancePage &&
                dependency.Variant == VegetationInstancePageAssetCooker.RuntimeVariant));
        Assert.All(dependencies, dependency => Assert.True(dependency.Required));
        Assert.Equal(
            dependencies.OrderBy(dependency => dependency.Guid)
                .ThenBy(dependency => dependency.PackageId, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.AssetType, StringComparer.Ordinal)
                .ThenBy(dependency => dependency.Variant, StringComparer.Ordinal),
            dependencies);

        CookedVegetationInstancePage page = VegetationInstancePageAssetCooker.BuildForCook(
            database,
            descriptor.Pages[0]);
        Assert.All(
            VegetationInstancePageAssetCooker.BuildDependencies(page),
            dependency => Assert.Equal(VegetationAssetTypes.Species, dependency.AssetType));
    }

    [Fact]
    public void ClusterV1_RejectsDuplicateStableKeysAcrossPagesBeforePublication()
    {
        using var fixture = new ClusterDiskFixture();
        AssetDatabase database = fixture.CreateDatabase();
        long initialGeneration = database.CookedRegistryGeneration;
        VegetationClusterCookDescriptor descriptor = CreateClusterDescriptor(
            secondPageKeyOffset: 0);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            VegetationClusterAssetCooker.Cook(database, descriptor));

        Assert.Contains("stable instance key", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(initialGeneration, database.CookedRegistryGeneration);
        AssertNoPublishedClusterOrPages(database);
    }

    [Theory]
    [InlineData(OwnershipFault.WrongBiomeOwner)]
    [InlineData(OwnershipFault.WrongPageOwner)]
    [InlineData(OwnershipFault.WrongSpeciesOwner)]
    public void ClusterV1_WrongAssetOwnershipFailsBeforeStaging(OwnershipFault fault)
    {
        using var fixture = new ClusterDiskFixture();
        AssetDatabase database = fixture.CreateDatabase();
        long initialGeneration = database.CookedRegistryGeneration;
        VegetationClusterCookDescriptor descriptor = CreateOwnershipFaultDescriptor(fault);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            VegetationClusterAssetCooker.Cook(database, descriptor));

        Assert.Contains("VegetationClusterAssetCooker", error.Message, StringComparison.Ordinal);
        Assert.Equal(initialGeneration, database.CookedRegistryGeneration);
        AssertNoPublishedClusterOrPages(database);
    }

    [Fact]
    public void ClusterV1_RejectsRehashedPageHashSizeOrderAndBoundsCorruption()
    {
        using var fixture = new ClusterDiskFixture();
        AssetDatabase database = fixture.CreateDatabase();
        CookedVegetationClusterArtifact artifact = VegetationClusterAssetCooker.Cook(
            database,
            CreateClusterDescriptor());
        byte[] valid = File.ReadAllBytes(artifact.Path);
        int pagesDescriptor = FindSectionDescriptor(valid, 4);
        int pagesOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            valid.AsSpan(pagesDescriptor + 8)));

        byte[] badHash = valid.ToArray();
        badHash.AsSpan(pagesOffset + 104, 32).Clear();
        Rehash(badHash);
        AssertRejected(badHash);

        byte[] badSize = valid.ToArray();
        badSize.AsSpan(pagesOffset + 96, sizeof(ulong)).Clear();
        Rehash(badSize);
        AssertRejected(badSize);

        byte[] oversizedPage = valid.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(
            oversizedPage.AsSpan(pagesOffset + 96),
            checked((ulong)VegetationInstancePageAssetCooker.MaxCookedPageBytes + 8));
        Rehash(oversizedPage);
        AssertRejected(oversizedPage);

        byte[] oversizedInstanceCount = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            oversizedInstanceCount.AsSpan(pagesOffset + 20),
            checked((uint)VegetationInstancePageAssetCooker.MaximumInstanceCount + 1U));
        Rehash(oversizedInstanceCount);
        AssertRejected(oversizedInstanceCount);

        byte[] badOrder = valid.ToArray();
        Span<byte> firstRecord = badOrder.AsSpan(
            pagesOffset,
            VegetationClusterAssetCooker.PageStride);
        Span<byte> secondRecord = badOrder.AsSpan(
            pagesOffset + VegetationClusterAssetCooker.PageStride,
            VegetationClusterAssetCooker.PageStride);
        byte[] savedFirst = firstRecord.ToArray();
        secondRecord.CopyTo(firstRecord);
        savedFirst.CopyTo(secondRecord);
        Rehash(badOrder);
        AssertRejected(badOrder);

        byte[] badBounds = valid.ToArray();
        double maximumX = BinaryPrimitives.ReadDoubleLittleEndian(
            badBounds.AsSpan(pagesOffset + 72));
        BinaryPrimitives.WriteDoubleLittleEndian(
            badBounds.AsSpan(pagesOffset + 48),
            maximumX);
        Rehash(badBounds);
        AssertRejected(badBounds);

        byte[] negativeZeroOrigin = valid.ToArray();
        int secondPageOriginY = pagesOffset + VegetationClusterAssetCooker.PageStride + 32;
        BinaryPrimitives.WriteUInt64LittleEndian(
            negativeZeroOrigin.AsSpan(secondPageOriginY),
            0x8000000000000000UL);
        Rehash(negativeZeroOrigin);
        AssertRejected(negativeZeroOrigin);
    }

    [Fact]
    public void ClusterCook_ReusesIdenticalPagesAndRequiresNewIdentityForChangedContent()
    {
        using var fixture = new ClusterDiskFixture();
        AssetDatabase database = fixture.CreateDatabase();
        VegetationClusterCookDescriptor forward = CreateClusterDescriptor();

        CookedVegetationClusterArtifact first = VegetationClusterAssetCooker.Cook(
            database,
            forward);
        CookedAssetRecord firstPageA = GetPageArtifact(
            database,
            VegetationCookedInstancePageTests.PageGuid);
        CookedAssetRecord firstPageB = GetPageArtifact(database, s_PageBGuid);
        byte[] firstRootBytes = File.ReadAllBytes(first.Path);
        byte[] firstPageABytes = File.ReadAllBytes(firstPageA.Path);
        byte[] firstPageBBytes = File.ReadAllBytes(firstPageB.Path);
        byte[] firstRootHash = SHA256.HashData(firstRootBytes);
        byte[] firstPageAHash = SHA256.HashData(firstPageABytes);
        byte[] firstPageBHash = SHA256.HashData(firstPageBBytes);
        DateTime preservedRootTimestamp = new(2024, 5, 6, 7, 8, 10, DateTimeKind.Utc);
        DateTime preservedPageATimestamp = new(2024, 5, 6, 7, 8, 12, DateTimeKind.Utc);
        DateTime preservedPageBTimestamp = new(2024, 5, 6, 7, 8, 14, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first.Path, preservedRootTimestamp);
        File.SetLastWriteTimeUtc(firstPageA.Path, preservedPageATimestamp);
        File.SetLastWriteTimeUtc(firstPageB.Path, preservedPageBTimestamp);
        long preservedGeneration = database.CookedRegistryGeneration;

        CookedVegetationClusterArtifact repeated = VegetationClusterAssetCooker.Cook(
            database,
            CreateClusterDescriptor(reversePages: true, permutePageContents: true));
        CookedAssetRecord repeatedPageA = GetPageArtifact(
            database,
            VegetationCookedInstancePageTests.PageGuid);
        CookedAssetRecord repeatedPageB = GetPageArtifact(database, s_PageBGuid);

        Assert.Equal(first.Path, repeated.Path);
        Assert.Equal(firstPageA.Path, repeatedPageA.Path);
        Assert.Equal(firstPageB.Path, repeatedPageB.Path);
        Assert.Equal(firstRootBytes, File.ReadAllBytes(repeated.Path));
        Assert.Equal(firstPageABytes, File.ReadAllBytes(repeatedPageA.Path));
        Assert.Equal(firstPageBBytes, File.ReadAllBytes(repeatedPageB.Path));
        Assert.Equal(firstRootHash, SHA256.HashData(File.ReadAllBytes(repeated.Path)));
        Assert.Equal(firstPageAHash, SHA256.HashData(File.ReadAllBytes(repeatedPageA.Path)));
        Assert.Equal(firstPageBHash, SHA256.HashData(File.ReadAllBytes(repeatedPageB.Path)));
        Assert.Equal(preservedRootTimestamp, File.GetLastWriteTimeUtc(repeated.Path));
        Assert.Equal(preservedPageATimestamp, File.GetLastWriteTimeUtc(repeatedPageA.Path));
        Assert.Equal(preservedPageBTimestamp, File.GetLastWriteTimeUtc(repeatedPageB.Path));
        Assert.Equal(preservedGeneration, database.CookedRegistryGeneration);

        VegetationClusterCookDescriptor mutatedDescriptor = MutateFirstPagePosition(forward);
        InvalidOperationException directPageError = Assert.Throws<InvalidOperationException>(() =>
            VegetationInstancePageAssetCooker.Cook(database, mutatedDescriptor.Pages[0]));
        Assert.Contains("immutable", directPageError.Message, StringComparison.OrdinalIgnoreCase);
        InvalidOperationException mutationError = Assert.Throws<InvalidOperationException>(() =>
            VegetationClusterAssetCooker.Cook(database, mutatedDescriptor));
        Assert.Contains("immutable", mutationError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(preservedGeneration, database.CookedRegistryGeneration);
        Assert.Equal(first.Path, GetClusterArtifact(database).Path);
        Assert.Equal(firstPageA.Path, GetPageArtifact(
            database,
            VegetationCookedInstancePageTests.PageGuid).Path);

        VegetationClusterCookDescriptor replacedDescriptor = ReplaceFirstPageIdentity(
            mutatedDescriptor,
            s_PageCGuid);
        CookedVegetationClusterArtifact replaced = VegetationClusterAssetCooker.Cook(
            database,
            replacedDescriptor);
        CookedAssetRecord replacementPage = GetPageArtifact(database, s_PageCGuid);
        CookedAssetRecord retainedPageB = GetPageArtifact(database, s_PageBGuid);

        Assert.NotEqual(first.Path, replaced.Path);
        Assert.NotEqual(firstPageA.Path, replacementPage.Path);
        Assert.Equal(firstPageB.Path, retainedPageB.Path);
        Assert.False(firstRootHash.AsSpan().SequenceEqual(
            SHA256.HashData(File.ReadAllBytes(replaced.Path))));
        Assert.False(firstPageAHash.AsSpan().SequenceEqual(
            SHA256.HashData(File.ReadAllBytes(replacementPage.Path))));
        Assert.Equal(firstPageBHash, SHA256.HashData(File.ReadAllBytes(retainedPageB.Path)));
        Assert.Equal(preservedPageBTimestamp, File.GetLastWriteTimeUtc(retainedPageB.Path));
        Assert.Equal(preservedGeneration + 2, database.CookedRegistryGeneration);
        Assert.Equal(firstPageA.Path, GetPageArtifact(
            database,
            VegetationCookedInstancePageTests.PageGuid).Path);
    }

    private static VegetationClusterCookDescriptor CreateClusterDescriptor(
        bool reversePages = false,
        bool permutePageContents = false,
        ulong secondPageKeyOffset = 0x100)
    {
        VegetationInstancePageCookDescriptor pageA =
            VegetationCookedInstancePageTests.CreateDescriptor();
        VegetationInstancePageCookDescriptor pageB =
            VegetationCookedInstancePageTests.CreateDescriptor(
                s_PageBGuid,
                VegetationCookedInstancePageTests.ClusterGuid,
                secondPageKeyOffset) with
            {
                Origin = new WorldPosition(-2048.5, 0.0, 1024.75)
            };
        if (permutePageContents)
        {
            pageA = VegetationCookedInstancePageTests.PermuteDescriptor(
                pageA,
                negateQuaternions: true);
            pageB = VegetationCookedInstancePageTests.PermuteDescriptor(
                pageB,
                negateQuaternions: true);
        }

        VegetationInstancePageCookDescriptor[] pages = reversePages
            ? [pageB, pageA]
            : [pageA, pageB];
        return new VegetationClusterCookDescriptor(
            VegetationCookedInstancePageTests.ClusterGuid,
            VegetationCookedInstancePageTests.PackageId,
            GeneratedSchemaVersion: 1,
            new CookedVegetationBiomeReference(
                s_BiomeGuid,
                VegetationCookedInstancePageTests.PackageId),
            Array.AsReadOnly(pages));
    }

    private static VegetationClusterCookDescriptor CreateOwnershipFaultDescriptor(
        OwnershipFault fault)
    {
        VegetationClusterCookDescriptor descriptor = CreateClusterDescriptor();
        if (fault == OwnershipFault.WrongBiomeOwner)
        {
            return descriptor with
            {
                Biome = descriptor.Biome with { PackageId = ForeignPackageId }
            };
        }

        VegetationInstancePageCookDescriptor[] pages = descriptor.Pages.ToArray();
        if (fault == OwnershipFault.WrongPageOwner)
        {
            pages[0] = pages[0] with { PackageId = ForeignPackageId };
        }
        else
        {
            CookedVegetationSpeciesReference[] species = pages[0].Species.ToArray();
            species[0] = species[0] with { PackageId = ForeignPackageId };
            pages[0] = pages[0] with { Species = Array.AsReadOnly(species) };
        }

        return descriptor with { Pages = Array.AsReadOnly(pages) };
    }

    private static VegetationClusterCookDescriptor MutateFirstPagePosition(
        VegetationClusterCookDescriptor source)
    {
        VegetationInstancePageCookDescriptor[] pages = source.Pages.ToArray();
        VegetationCookedInstanceInput[] instances = pages[0].Instances.ToArray();
        instances[0] = instances[0] with
        {
            LocalPosition = instances[0].LocalPosition + new Vector3(1.0f / 1024.0f, 0.0f, 0.0f)
        };
        pages[0] = pages[0] with { Instances = Array.AsReadOnly(instances) };
        return source with { Pages = Array.AsReadOnly(pages) };
    }

    private static VegetationClusterCookDescriptor ReplaceFirstPageIdentity(
        VegetationClusterCookDescriptor source,
        Guid replacementGuid)
    {
        VegetationInstancePageCookDescriptor[] pages = source.Pages.ToArray();
        pages[0] = pages[0] with { Guid = replacementGuid };
        return source with { Pages = Array.AsReadOnly(pages) };
    }

    private static CookedAssetRecord GetClusterArtifact(AssetDatabase database)
    {
        Assert.True(database.TryGetCookedArtifact(
            VegetationCookedInstancePageTests.ClusterGuid,
            VegetationClusterAssetCooker.RuntimeVariant,
            out CookedAssetRecord artifact));
        return artifact;
    }

    private static CookedAssetRecord GetPageArtifact(AssetDatabase database, Guid pageGuid)
    {
        Assert.True(database.TryGetCookedArtifact(
            pageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out CookedAssetRecord artifact));
        return artifact;
    }

    private static void AssertNoPublishedClusterOrPages(AssetDatabase database)
    {
        Assert.False(database.TryGetCookedArtifact(
            VegetationCookedInstancePageTests.ClusterGuid,
            VegetationClusterAssetCooker.RuntimeVariant,
            out _));
        Assert.False(database.TryGetCookedArtifact(
            VegetationCookedInstancePageTests.PageGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out _));
        Assert.False(database.TryGetCookedArtifact(
            s_PageBGuid,
            VegetationInstancePageAssetCooker.RuntimeVariant,
            out _));
        Assert.False(Directory.Exists(Path.Combine(database.CookedRoot, ".staging")));
    }

    private static void AssertRejected(byte[] bytes)
    {
        Assert.False(
            VegetationClusterAssetCooker.TryReadPayload(
                VegetationCookedInstancePageTests.ClusterGuid,
                VegetationCookedInstancePageTests.PackageId,
                bytes,
                "cluster-corrupt",
                out _,
                out string diagnostic));
        Assert.False(string.IsNullOrWhiteSpace(diagnostic));
    }

    private static int FindSectionDescriptor(byte[] bytes, uint sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(52));
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = VegetationClusterAssetCooker.HeaderSize +
                (index * VegetationCookedContainer.SectionDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) == sectionType)
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"Section '{sectionType}' was not found.");
    }

    private static void Rehash(byte[] bytes)
    {
        SHA256.HashData(bytes.AsSpan(VegetationClusterAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(
                VegetationClusterAssetCooker.HashOffset,
                VegetationCookedContainer.HashSize));
    }

    public enum OwnershipFault
    {
        WrongBiomeOwner,
        WrongPageOwner,
        WrongSpeciesOwner
    }

    private sealed class ClusterDiskFixture : IDisposable
    {
        public ClusterDiskFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "ArisenVegetationClusterTests",
                Guid.NewGuid().ToString("N"));
            WorkspaceRoot = Path.Combine(Root, "Workspace");
            PackageRoot = Path.Combine(Root, "Package");
            Directory.CreateDirectory(WorkspaceRoot);
            WriteAsset(
                "Cluster.generated",
                VegetationCookedInstancePageTests.ClusterGuid,
                VegetationAssetTypes.Cluster);
            WriteAsset(
                "PageA.generated",
                VegetationCookedInstancePageTests.PageGuid,
                VegetationAssetTypes.InstancePage);
            WriteAsset(
                "PageB.generated",
                s_PageBGuid,
                VegetationAssetTypes.InstancePage);
            WriteAsset(
                "PageC.generated",
                s_PageCGuid,
                VegetationAssetTypes.InstancePage);
            WriteAsset(
                "Biome.generated",
                s_BiomeGuid,
                VegetationAssetTypes.Biome,
                CreateBiomeSource());
            WriteAsset(
                "SpeciesA.generated",
                VegetationCookedInstancePageTests.SpeciesAGuid,
                VegetationAssetTypes.Species);
            WriteAsset(
                "SpeciesB.generated",
                VegetationCookedInstancePageTests.SpeciesBGuid,
                VegetationAssetTypes.Species);
        }

        public string Root { get; }

        public string WorkspaceRoot { get; }

        public string PackageRoot { get; }

        public AssetDatabase CreateDatabase()
        {
            var database = new AssetDatabase();
            database.InitializeWorkspace(
                WorkspaceRoot,
                [(VegetationCookedInstancePageTests.PackageId, PackageRoot)],
                AssetSourceAccessMode.RuntimeAssetCook);
            return database;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static string CreateBiomeSource() => $$"""
            Version: 1
            BiomeGuid: {{s_BiomeGuid:D}}
            Name: Cluster Test Biome
            GlobalSeed: 19
            Entries:
            - EntryId: species-a
              Species: { Guid: {{VegetationCookedInstancePageTests.SpeciesAGuid:D}}, PackageId: {{VegetationCookedInstancePageTests.PackageId}} }
              Density: 0.5
              SeedSalt: 11
              AltitudeRange: { Minimum: -1000.0, Maximum: 5000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 90.0 }
              LayerWeightRules: []
              MinimumSpacing: 1.0
              ClusterSize: 64
              ExclusionPolicy: Respect
            - EntryId: species-b
              Species: { Guid: {{VegetationCookedInstancePageTests.SpeciesBGuid:D}}, PackageId: {{VegetationCookedInstancePageTests.PackageId}} }
              Density: 0.25
              SeedSalt: 13
              AltitudeRange: { Minimum: -1000.0, Maximum: 5000.0 }
              SlopeRangeDegrees: { Minimum: 0.0, Maximum: 90.0 }
              LayerWeightRules: []
              MinimumSpacing: 1.5
              ClusterSize: 64
              ExclusionPolicy: Respect
            """;

        private void WriteAsset(
            string fileName,
            Guid guid,
            string assetType,
            string contents = "generated fixture body")
        {
            string path = Path.Combine(PackageRoot, "Assets", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            SerializationUtil.Serialize(
                new AssetMetadata
                {
                    Guid = guid,
                    AssetType = assetType,
                    Importer = "VegetationClusterTestFixture"
                },
                path + ".meta");
        }
    }
}
