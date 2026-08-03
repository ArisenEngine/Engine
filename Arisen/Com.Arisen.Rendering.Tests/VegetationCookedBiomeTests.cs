using System.Buffers.Binary;
using System.Security.Cryptography;
using ArisenEngine.Vegetation.Assets;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationCookedBiomeTests
{
    private static readonly Guid s_BiomeGuid =
        Guid.Parse("c0a92f10-0eb9-4d24-b729-7d0f38313001");
    private static readonly Guid s_RockSpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622001");
    private static readonly Guid s_GrassSpeciesGuid =
        Guid.Parse("7b0f2e52-8b67-4e3d-bf0a-cbc42f622002");
    private const string PackageId = "com.arisen.packagegame";

    [Fact]
    public void BiomeV1_RoundTripsDeterministicallyInAuthoredOrder()
    {
        CookedVegetationBiome source = CreateBiome();
        byte[] first = VegetationBiomeAssetCooker.WritePayload(source);
        byte[] repeated = VegetationBiomeAssetCooker.WritePayload(source);

        Assert.Equal(first, repeated);
        Assert.True(
            VegetationBiomeAssetCooker.TryReadPayload(
                s_BiomeGuid,
                PackageId,
                first,
                "biome-memory",
                out CookedVegetationBiome loaded,
                out string diagnostic),
            diagnostic);
        Assert.Equal(source.Guid, loaded.Guid);
        Assert.Equal(source.GlobalSeed, loaded.GlobalSeed);
        Assert.Equal(["rock", "grass"], loaded.Entries.Select(entry => entry.EntryId));
        Assert.Equal(
            ["Rock", "GrassSoil"],
            loaded.Entries.SelectMany(entry => entry.LayerWeightRules).Select(rule => rule.LayerId));
        Assert.Equal(
            [s_RockSpeciesGuid, s_GrassSpeciesGuid],
            loaded.Entries.Select(entry => entry.Species.Guid));

        VegetationCookedAssetDependency[] dependencies =
            VegetationBiomeAssetCooker.BuildDependencies(loaded);
        Assert.Equal(2, dependencies.Length);
        Assert.All(
            dependencies,
            dependency =>
            {
                Assert.Equal(VegetationAssetTypes.Species, dependency.AssetType);
                Assert.Equal(VegetationSpeciesAssetCooker.RuntimeVariant, dependency.Variant);
                Assert.True(dependency.Required);
            });
        Assert.Equal(
            dependencies.OrderBy(dependency => dependency.Guid),
            dependencies);
    }

    [Fact]
    public void BiomeV1_RejectsDirectoryRuleIdentityAndRangeCorruption()
    {
        byte[] valid = VegetationBiomeAssetCooker.WritePayload(CreateBiome());

        byte[] wrongMagic = valid.ToArray();
        wrongMagic[0] ^= 0xff;
        AssertRejected(wrongMagic, "magic");

        byte[] wrongHash = valid.ToArray();
        wrongHash[^1] ^= 0x01;
        AssertRejected(wrongHash, "content hash");

        byte[] wrongHeaderEntryCount = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(
            wrongHeaderEntryCount.AsSpan(40),
            VegetationBiomeSourceAssetLoader.MaximumEntryCount + 1);
        AssertRejected(wrongHeaderEntryCount, "header");

        byte[] wrongHeaderRuleCount = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(wrongHeaderRuleCount.AsSpan(44), -1);
        AssertRejected(wrongHeaderRuleCount, "header");

        byte[] nonzeroReserved = valid.ToArray();
        nonzeroReserved[52] = 1;
        AssertRejected(nonzeroReserved, "reserved");

        int entryDescriptor = FindSectionDescriptor(
            valid,
            (uint)CookedVegetationBiomeSectionType.Entries);
        int ruleDescriptor = FindSectionDescriptor(
            valid,
            (uint)CookedVegetationBiomeSectionType.LayerWeightRules);

        byte[] unknownRequired = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(unknownRequired.AsSpan(ruleDescriptor), 99);
        Rehash(unknownRequired);
        AssertRejected(unknownRequired, "unknown required section");

        byte[] wrongRuleStride = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            wrongRuleStride.AsSpan(ruleDescriptor + 28),
            VegetationBiomeAssetCooker.LayerWeightRuleStride + 8);
        Rehash(wrongRuleStride);
        AssertRejected(wrongRuleStride, "stride");

        byte[] unalignedRuleOffset = valid.ToArray();
        ulong ruleOffsetValue = BinaryPrimitives.ReadUInt64LittleEndian(
            unalignedRuleOffset.AsSpan(ruleDescriptor + 8));
        BinaryPrimitives.WriteUInt64LittleEndian(
            unalignedRuleOffset.AsSpan(ruleDescriptor + 8),
            checked(ruleOffsetValue + 1));
        Rehash(unalignedRuleOffset);
        AssertRejected(unalignedRuleOffset, "unaligned");

        byte[] outOfBoundsRuleOffset = valid.ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(
            outOfBoundsRuleOffset.AsSpan(ruleDescriptor + 8),
            checked((ulong)valid.Length + 8));
        Rehash(outOfBoundsRuleOffset);
        AssertRejected(outOfBoundsRuleOffset, "beyond the file");

        int entriesOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            valid.AsSpan(entryDescriptor + 8)));
        byte[] duplicateEntryId = valid.ToArray();
        uint firstEntryId = BinaryPrimitives.ReadUInt32LittleEndian(
            duplicateEntryId.AsSpan(entriesOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(
            duplicateEntryId.AsSpan(entriesOffset + VegetationBiomeAssetCooker.EntryStride),
            firstEntryId);
        Rehash(duplicateEntryId);
        AssertRejected(duplicateEntryId, "entry");

        int rulesOffset = checked((int)BinaryPrimitives.ReadUInt64LittleEndian(
            valid.AsSpan(ruleDescriptor + 8)));
        byte[] invalidWeight = valid.ToArray();
        BinaryPrimitives.WriteSingleLittleEndian(
            invalidWeight.AsSpan(rulesOffset + 8),
            1.5f);
        Rehash(invalidWeight);
        AssertRejected(invalidWeight, "layer rule");

        byte[] noncanonicalRuleRange = valid.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            noncanonicalRuleRange.AsSpan(entriesOffset + 68),
            1);
        Rehash(noncanonicalRuleRange);
        AssertRejected(noncanonicalRuleRange, "rule range");

        byte[] missingSpecies = valid.ToArray();
        missingSpecies.AsSpan(entriesOffset + 8, 16).Clear();
        Rehash(missingSpecies);
        AssertRejected(missingSpecies, "entry");
    }

    private static CookedVegetationBiome CreateBiome()
    {
        return new CookedVegetationBiome(
            s_BiomeGuid,
            PackageId,
            VegetationBiomeSourceAssetLoader.CurrentSourceSchemaVersion,
            "Showcase Valley Vegetation",
            1469598103934665603,
            Array.AsReadOnly<CookedVegetationBiomeEntry>(
            [
                new(
                    "rock",
                    new CookedVegetationSpeciesReference(s_RockSpeciesGuid, PackageId),
                    0.0125f,
                    16294208416658607535,
                    new VegetationValueRange(-64.0f, 192.0f),
                    new VegetationValueRange(0.0f, 65.0f),
                    Array.AsReadOnly<CookedVegetationLayerWeightRule>(
                    [
                        new("Rock", new VegetationValueRange(0.25f, 1.0f))
                    ]),
                    5.0f,
                    64,
                    VegetationExclusionPolicy.Respect),
                new(
                    "grass",
                    new CookedVegetationSpeciesReference(s_GrassSpeciesGuid, PackageId),
                    0.8f,
                    15111065706836454659,
                    new VegetationValueRange(-64.0f, 192.0f),
                    new VegetationValueRange(0.0f, 42.0f),
                    Array.AsReadOnly<CookedVegetationLayerWeightRule>(
                    [
                        new("GrassSoil", new VegetationValueRange(0.4f, 1.0f))
                    ]),
                    0.35f,
                    256,
                    VegetationExclusionPolicy.IgnoreSoft)
            ]));
    }

    private static void AssertRejected(byte[] bytes, string expectedDiagnostic)
    {
        Assert.False(
            VegetationBiomeAssetCooker.TryReadPayload(
                s_BiomeGuid,
                PackageId,
                bytes,
                "biome-corrupt",
                out _,
                out string diagnostic));
        Assert.Contains(expectedDiagnostic, diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static int FindSectionDescriptor(byte[] bytes, uint sectionType)
    {
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(48));
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = VegetationBiomeAssetCooker.HeaderSize +
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
        SHA256.HashData(bytes.AsSpan(VegetationBiomeAssetCooker.HeaderSize)).CopyTo(
            bytes.AsSpan(
                VegetationBiomeAssetCooker.HashOffset,
                VegetationCookedContainer.HashSize));
    }
}
