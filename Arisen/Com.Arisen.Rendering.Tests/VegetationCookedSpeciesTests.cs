using System.Buffers.Binary;
using System.Security.Cryptography;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationCookedSpeciesTests
{
    private static readonly Guid s_SpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001");
    private const string PackageId = "com.arisen.packagegame";

    [Fact]
    public void SpeciesV1_RoundTripsDeterministicallyWithCanonicalDependencies()
    {
        CookedVegetationSpecies source = CreateSpecies();
        byte[] first = VegetationSpeciesAssetCooker.WritePayload(source);
        byte[] repeated = VegetationSpeciesAssetCooker.WritePayload(source);

        Assert.Equal(first, repeated);
        Assert.True(
            VegetationSpeciesAssetCooker.TryReadPayload(
                s_SpeciesGuid,
                PackageId,
                first,
                "species-memory",
                out CookedVegetationSpecies loaded,
                out string diagnostic),
            diagnostic);
        Assert.Equal(source.Guid, loaded.Guid);
        Assert.Equal(source.Name, loaded.Name);
        Assert.Equal(source.ScaleRange, loaded.ScaleRange);
        Assert.Equal(source.CollisionPromotion, loaded.CollisionPromotion);
        Assert.Equal([45.0f, 180.0f], loaded.Lods.Select(lod => lod.MaximumDistance));
        Assert.Equal(
            source.Lods.Select(lod => lod.Mesh.Guid),
            loaded.Lods.Select(lod => lod.Mesh.Guid));

        VegetationCookedAssetDependency[] dependencies =
            VegetationSpeciesAssetCooker.BuildDependencies(loaded);
        Assert.Equal(4, dependencies.Length);
        Assert.Equal(2, dependencies.Count(dependency => dependency.AssetType == "Mesh"));
        Assert.Equal(2, dependencies.Count(dependency => dependency.AssetType == "Material"));
        Assert.All(
            dependencies.Where(dependency => dependency.AssetType == "Mesh"),
            dependency => Assert.Equal("staticmesh.uint32", dependency.Variant));
        Assert.All(
            dependencies.Where(dependency => dependency.AssetType == "Material"),
            dependency => Assert.Equal("material.runtime", dependency.Variant));
        Assert.Equal(
            dependencies.OrderBy(dependency => dependency.Guid),
            dependencies);
    }

    [Fact]
    public void SpeciesV1_RejectsHeaderHashDirectoryAndSemanticCorruption()
    {
        byte[] valid = VegetationSpeciesAssetCooker.WritePayload(CreateSpecies());

        byte[] wrongMagic = valid.ToArray();
        wrongMagic[0] ^= 0xff;
        AssertRejected(wrongMagic, "magic");

        byte[] wrongVersion = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(wrongVersion.AsSpan(12), 99);
        AssertRejected(wrongVersion, "header");

        byte[] wrongHeaderCount = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(
            wrongHeaderCount.AsSpan(40),
            VegetationSpeciesSourceAssetLoader.MaximumLodCount + 1);
        AssertRejected(wrongHeaderCount, "header");

        byte[] wrongHash = valid.ToArray();
        wrongHash[^1] ^= 0x01;
        AssertRejected(wrongHash, "content hash");

        byte[] nonzeroReserved = valid.ToArray();
        nonzeroReserved[56] = 1;
        AssertRejected(nonzeroReserved, "reserved");

        int metadataDescriptor = FindSectionDescriptor(
            valid,
            (uint)CookedVegetationSpeciesSectionType.Metadata);
        int stringsDescriptor = FindSectionDescriptor(
            valid,
            (uint)CookedVegetationSpeciesSectionType.Strings);
        int lodDescriptor = FindSectionDescriptor(
            valid,
            (uint)CookedVegetationSpeciesSectionType.Lods);

        byte[] unknownRequired = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(unknownRequired.AsSpan(lodDescriptor), 99);
        Rehash(unknownRequired);
        AssertRejected(unknownRequired, "unknown required section");

        byte[] overlapping = valid.ToArray();
        ulong metadataOffset = BinaryPrimitives.ReadUInt64LittleEndian(
            overlapping.AsSpan(metadataDescriptor + 8));
        BinaryPrimitives.WriteUInt64LittleEndian(
            overlapping.AsSpan(stringsDescriptor + 8),
            metadataOffset);
        Rehash(overlapping);
        AssertRejected(overlapping, "overlap");

        byte[] wrongDescriptorCount = valid.ToArray();
        uint lodCount = BinaryPrimitives.ReadUInt32LittleEndian(
            wrongDescriptorCount.AsSpan(lodDescriptor + 24));
        BinaryPrimitives.WriteUInt32LittleEndian(
            wrongDescriptorCount.AsSpan(lodDescriptor + 24),
            checked(lodCount + 1));
        Rehash(wrongDescriptorCount);
        AssertRejected(wrongDescriptorCount, "count");

        byte[] wrongStride = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            wrongStride.AsSpan(lodDescriptor + 28),
            VegetationSpeciesAssetCooker.LodStride + 8);
        Rehash(wrongStride);
        AssertRejected(wrongStride, "stride");

        byte[] unalignedOffset = valid.ToArray();
        ulong lodOffsetValue = BinaryPrimitives.ReadUInt64LittleEndian(
            unalignedOffset.AsSpan(lodDescriptor + 8));
        BinaryPrimitives.WriteUInt64LittleEndian(
            unalignedOffset.AsSpan(lodDescriptor + 8),
            checked(lodOffsetValue + 1));
        Rehash(unalignedOffset);
        AssertRejected(unalignedOffset, "unaligned");

        byte[] outOfBoundsOffset = valid.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(
            outOfBoundsOffset.AsSpan(lodDescriptor + 8),
            checked((ulong)valid.Length + 8));
        Rehash(outOfBoundsOffset);
        AssertRejected(outOfBoundsOffset, "beyond the file");

        byte[] nonFiniteScale = valid.ToArray();
        int metadataOffsetInt = checked((int)metadataOffset);
        BinaryPrimitives.WriteSingleLittleEndian(
            nonFiniteScale.AsSpan(metadataOffsetInt + 16),
            float.NaN);
        Rehash(nonFiniteScale);
        AssertRejected(nonFiniteScale, "ranges");

        byte[] missingMesh = valid.ToArray();
        int lodOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            missingMesh.AsSpan(lodDescriptor + 8)));
        missingMesh.AsSpan(lodOffset, 16).Clear();
        Rehash(missingMesh);
        AssertRejected(missingMesh, "LOD");
    }

    private static CookedVegetationSpecies CreateSpecies()
    {
        return new CookedVegetationSpecies(
            s_SpeciesGuid,
            PackageId,
            VegetationSpeciesSourceAssetLoader.CurrentSourceSchemaVersion,
            "Valley Rock",
            Array.AsReadOnly<CookedVegetationSpeciesLod>(
            [
                new(
                    new CookedVegetationMeshReference(
                        Guid.Parse("30000000-0000-0000-0000-000000000001"),
                        "com.arisen.meshes"),
                    new CookedVegetationMaterialReference(
                        Guid.Parse("40000000-0000-0000-0000-000000000001"),
                        "com.arisen.materials"),
                    45.0f,
                    1.0f),
                new(
                    new CookedVegetationMeshReference(
                        Guid.Parse("30000000-0000-0000-0000-000000000002"),
                        "com.arisen.meshes"),
                    new CookedVegetationMaterialReference(
                        Guid.Parse("40000000-0000-0000-0000-000000000002"),
                        "com.arisen.materials"),
                    180.0f,
                    4.0f)
            ]),
            VegetationShadowPolicy.Cast,
            new VegetationValueRange(0.6f, 1.8f),
            new VegetationValueRange(0.0f, 360.0f),
            new VegetationValueRange(-15.0f, 15.0f),
            new VegetationCollisionPromotionDescriptor(
                VegetationCollisionPromotionMode.Capsule,
                0.75f,
                1.5f,
                24.0f),
            0.05f);
    }

    private static void AssertRejected(byte[] bytes, string expectedDiagnostic)
    {
        Assert.False(
            VegetationSpeciesAssetCooker.TryReadPayload(
                s_SpeciesGuid,
                PackageId,
                bytes,
                "species-corrupt",
                out _,
                out string diagnostic));
        Assert.Contains(expectedDiagnostic, diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static int FindSectionDescriptor(byte[] bytes, uint sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(44));
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = VegetationSpeciesAssetCooker.HeaderSize +
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
        SHA256.HashData(bytes.AsSpan(VegetationSpeciesAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(
                VegetationSpeciesAssetCooker.HashOffset,
                VegetationCookedContainer.HashSize));
    }
}
