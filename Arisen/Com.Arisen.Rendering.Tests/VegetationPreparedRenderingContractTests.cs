using Arisen.Native.RHI;
using ArisenEngine.Resources.Serialization;
using ArisenEngine.Vegetation.Assets;
using ArisenEngine.Vegetation.GenericRenderPipeline;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationPreparedRenderingContractTests
{
    private static readonly Guid ClusterGuid =
        Guid.Parse("5e7f45cd-90d0-438d-b973-a024e40a99ea");
    private static readonly Guid SpeciesGuid =
        Guid.Parse("453af7be-7ff7-4799-8e0c-03d7f2f98963");
    private static readonly Guid MeshGuid =
        Guid.Parse("82cc0e0b-16b7-41b1-9de8-354abf67643c");
    private static readonly Guid MaterialGuid =
        Guid.Parse("15a34d80-d0ba-49c4-b503-0cbdd7b7577d");

    [Fact]
    public void GpuInstanceHasExactFortyEightByteShaderLayout()
    {
        Assert.Equal(48, VegetationGpuInstance.Stride);
        Assert.Equal(VegetationGpuInstance.Stride, Marshal.SizeOf<VegetationGpuInstance>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<VegetationGpuInstance>());
        Assert.Equal(0, OffsetOf(nameof(VegetationGpuInstance.OriginRelativePositionScale)));
        Assert.Equal(16, OffsetOf(nameof(VegetationGpuInstance.Orientation)));
        Assert.Equal(32, OffsetOf(nameof(VegetationGpuInstance.StableVariation)));
        Assert.Equal(36, OffsetOf(nameof(VegetationGpuInstance.WindPhase)));
        Assert.Equal(40, OffsetOf(nameof(VegetationGpuInstance.ColorVariation)));
        Assert.Equal(44, OffsetOf(nameof(VegetationGpuInstance.Flags)));
    }

    [Fact]
    public void PackingUsesClusterRelativePositionAndStableGoldenFields()
    {
        var instance = new CookedVegetationInstance(
            StableKey: 0x0123_4567_89AB_CDEFUL,
            SpeciesIndex: 3,
            LocalPosition: new Vector3(0.25f, 1.5f, -3.75f),
            Orientation: new Quaternion(0.25f, -0.5f, 0.75f, 1.0f),
            UniformScale: 1.25f,
            ConservativeRadius: 4.0f);
        var clusterOrigin = new WorldPosition(
            1_000_000_000_000.0,
            -2_000_000_000_000.0,
            3_000_000_000_000.0);
        var pageOrigin = new WorldPosition(
            clusterOrigin.X + 1024.0,
            clusterOrigin.Y - 2048.0,
            clusterOrigin.Z + 4096.0);

        VegetationGpuInstance packed = VegetationGpuInstancePacking.Pack(
            instance,
            pageOrigin,
            clusterOrigin,
            flags: 0xA5A5_5A5Au);
        VegetationGpuInstance repeated = VegetationGpuInstancePacking.Pack(
            instance,
            pageOrigin,
            clusterOrigin,
            flags: 0xA5A5_5A5Au);

        Assert.Equal(new Vector4(1024.25f, -2046.5f, 4092.25f, 1.25f),
            packed.OriginRelativePositionScale);
        Assert.Equal(instance.Orientation, packed.Orientation);
        Assert.Equal(0xE542_5A90u, packed.StableVariation);
        Assert.Equal(0x3FDD_F82Eu, FloatBits(packed.WindPhase));
        Assert.Equal(0x3F63_271Bu, FloatBits(packed.ColorVariation));
        Assert.Equal(0xA5A5_5A5Au, packed.Flags);

        Assert.Equal(packed.OriginRelativePositionScale, repeated.OriginRelativePositionScale);
        Assert.Equal(packed.Orientation, repeated.Orientation);
        Assert.Equal(packed.StableVariation, repeated.StableVariation);
        Assert.Equal(FloatBits(packed.WindPhase), FloatBits(repeated.WindPhase));
        Assert.Equal(FloatBits(packed.ColorVariation), FloatBits(repeated.ColorVariation));
        Assert.Equal(packed.Flags, repeated.Flags);
    }

    [Fact]
    public void PackingAcceptsExactOriginRelativeFloatBoundary()
    {
        CookedVegetationInstance instance = CreateInstance();

        VegetationGpuInstance packed = VegetationGpuInstancePacking.Pack(
            instance,
            new WorldPosition(16_777_216.0, -16_777_216.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0));

        Assert.Equal(
            new Vector4(16_777_216.0f, -16_777_216.0f, 0.0f, 1.0f),
            packed.OriginRelativePositionScale);
    }

    [Theory]
    [InlineData(16_777_216.5)]
    [InlineData(-16_777_216.5)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]
    public void PackingRejectsUnsafeOriginRelativeCoordinates(double pageOriginX)
    {
        CookedVegetationInstance instance = CreateInstance();

        Assert.Throws<InvalidDataException>(() => VegetationGpuInstancePacking.Pack(
            instance,
            new WorldPosition(pageOriginX, 0.0, 0.0),
            new WorldPosition(0.0, 0.0, 0.0)));
    }

    [Fact]
    public void PackingRejectsInvalidOrientationAndScale()
    {
        CookedVegetationInstance valid = CreateInstance();
        var invalidOrientation = valid with
        {
            Orientation = new Quaternion(float.NaN, 0.0f, 0.0f, 1.0f)
        };

        Assert.Throws<InvalidDataException>(() => VegetationGpuInstancePacking.Pack(
            invalidOrientation,
            default,
            default));

        float[] invalidScales =
        {
            0.0f,
            -1.0f,
            float.NaN,
            float.PositiveInfinity,
            float.NegativeInfinity
        };
        foreach (float scale in invalidScales)
        {
            CookedVegetationInstance invalidScale = valid with { UniformScale = scale };
            Assert.Throws<InvalidDataException>(() => VegetationGpuInstancePacking.Pack(
                invalidScale,
                default,
                default));
        }
    }

    [Fact]
    public void PreparedClusterViewRequiresIdentityGenerationFiniteOriginAndContents()
    {
        VegetationPreparedBatch batch = CreateBatch(firstInstance: 0, instanceCount: 1);
        var batches = new[] { batch };
        var origin = new WorldPosition(12.0, 3.5, -8.0);
        var valid = new VegetationPreparedClusterView(
            ClusterGuid,
            generation: 27,
            origin,
            batches,
            instanceCount: 1);

        Assert.True(valid.IsValid);
        Assert.Equal(ClusterGuid, valid.ClusterGuid);
        Assert.Equal(27UL, valid.Generation);
        Assert.Equal(origin, valid.Origin);
        Assert.Equal(1, valid.InstanceCount);
        Assert.Equal(1, valid.Batches.Length);
        Assert.Equal(batch.FirstInstance, valid.Batches[0].FirstInstance);
        Assert.Equal(batch.InstanceCount, valid.Batches[0].InstanceCount);

        Assert.False(default(VegetationPreparedClusterView).IsValid);
        Assert.Equal(0, default(VegetationPreparedClusterView).Batches.Length);
        Assert.False(new VegetationPreparedClusterView(
            Guid.Empty, 27, origin, batches, 1).IsValid);
        Assert.False(new VegetationPreparedClusterView(
            ClusterGuid, 0, origin, batches, 1).IsValid);
        Assert.False(new VegetationPreparedClusterView(
            ClusterGuid,
            27,
            new WorldPosition(double.NaN, 0.0, 0.0),
            batches,
            1).IsValid);
        Assert.False(new VegetationPreparedClusterView(
            ClusterGuid, 27, origin, Array.Empty<VegetationPreparedBatch>(), 1).IsValid);
        Assert.False(new VegetationPreparedClusterView(
            ClusterGuid, 27, origin, batches, 0).IsValid);
        Assert.Throws<ArgumentNullException>(() => new VegetationPreparedClusterView(
            ClusterGuid, 27, origin, null!, 1));
    }

    [Fact]
    public void PreparedBatchesExposeContiguousNonOverlappingInstanceRanges()
    {
        VegetationPreparedBatch[] batches =
        {
            CreateBatch(firstInstance: 0, instanceCount: 2),
            CreateBatch(firstInstance: 2, instanceCount: 3),
            CreateBatch(firstInstance: 5, instanceCount: 3)
        };
        var view = new VegetationPreparedClusterView(
            ClusterGuid,
            generation: 91,
            new WorldPosition(0.0, 0.0, 0.0),
            batches,
            instanceCount: 8);

        uint expectedFirstInstance = 0;
        ReadOnlySpan<VegetationPreparedBatch> preparedBatches = view.Batches;
        for (int index = 0; index < preparedBatches.Length; index++)
        {
            ref readonly VegetationPreparedBatch batch = ref preparedBatches[index];
            Assert.True(batch.IsValid);
            Assert.Equal(expectedFirstInstance, batch.FirstInstance);
            expectedFirstInstance = checked(expectedFirstInstance + batch.InstanceCount);
        }

        Assert.Equal((uint)view.InstanceCount, expectedFirstInstance);
    }

    [Fact]
    public void FactoryReleaseQueuesCallerThreadOwnershipUntilSubmittedTicketIsKnown()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationGpuResourceFactory.cs");
        string release = SliceBetween(
            source,
            "public void RequestRelease(IVegetationClusterGpuResource resource)",
            "public void UpdateSubmittedTicket(ulong submittedTicket)");
        string submitted = SliceBetween(
            source,
            "public void UpdateSubmittedTicket(ulong submittedTicket)",
            "public void ReleaseAllDeviceResources()");
        string releaseAll = SliceBetween(
            source,
            "public void ReleaseAllDeviceResources()",
            "private bool TryBuildPendingBatches(");

        Assert.Contains(
            "m_RetirementState.RequestRelease(resource);",
            release,
            StringComparison.Ordinal);
        Assert.DoesNotContain("m_DisposalQueue", release, StringComparison.Ordinal);
        Assert.DoesNotContain("resource.Dispose()", release, StringComparison.Ordinal);
        AssertInOrder(
            submitted,
            "m_LastSubmittedTicket = Math.Max(m_LastSubmittedTicket, submittedTicket);",
            "DrainPendingResourceReleases();",
            "m_DisposalQueue.ReleaseCompleted(m_Device, m_DeviceGeneration);");
        AssertInOrder(
            releaseAll.Replace("\r\n", "\n", StringComparison.Ordinal),
            "DrainPendingResourceReleases();",
            "m_DisposalQueue.ReleaseDevice(");
    }

    [Fact]
    public void PreparedClusterLookupRequiresMatchingResidentGeneration()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationPreparedAssetProvider.cs");
        string lookup = SliceBetween(
            source,
            "internal bool TryGetCluster(",
            "internal void UpdateSubmittedTicket(ulong submittedTicket)");

        AssertInOrder(
            lookup,
            "generation != 0",
            "Volatile.Read(ref m_ClusterPublications)",
            "publication.ResidentGeneration == generation",
            "publication.Resource.CreateView(generation)");
        Assert.DoesNotContain("lock (", lookup, StringComparison.Ordinal);
    }

    [Fact]
    public void PhysicalMetricsAreCapturedInsideTheLifecycleGate()
    {
        string source = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/" +
            "com.arisen.vegetation.generic-renderpipeline/Managed/" +
            "VegetationPreparedAssetProvider.cs");
        string publication = SliceBetween(
            source,
            "private void PublishMetricsSnapshot()",
            "private static string GetExpectedVariant(string assetType)");

        AssertInOrder(
            publication,
            "lock (m_LifecycleState.Gate)",
            "m_LifecycleState.PublishPhysicalMetricsLocked(CapturePhysicalMetrics());");
        Assert.DoesNotContain(
            "PublishPhysicalMetrics(CapturePhysicalMetrics())",
            publication,
            StringComparison.Ordinal);
    }

    private static CookedVegetationInstance CreateInstance() => new(
        StableKey: 1,
        SpeciesIndex: 0,
        LocalPosition: Vector3.Zero,
        Orientation: Quaternion.Identity,
        UniformScale: 1.0f,
        ConservativeRadius: 1.0f);

    private static VegetationPreparedBatch CreateBatch(
        uint firstInstance,
        uint instanceCount) => new(
        SpeciesGuid,
        MeshGuid,
        MaterialGuid,
        new RHIBufferHandle { Index = 1, Generation = 1 },
        new RHIBufferHandle { Index = 2, Generation = 1 },
        EIndexType.INDEX_TYPE_UINT32,
        indexCount: 36,
        firstIndex: 0,
        vertexOffset: 0,
        instanceBufferIndex: 7,
        firstInstance,
        instanceCount,
        VegetationShadowPolicy.Cast,
        new VegetationPreparedMaterialData(
            Vector4.One,
            metallicFactor: 0.0f,
            roughnessFactor: 1.0f,
            baseColorImageIndex: uint.MaxValue,
            baseColorSamplerIndex: uint.MaxValue));

    private static int OffsetOf(string fieldName) =>
        Marshal.OffsetOf<VegetationGpuInstance>(fieldName).ToInt32();

    private static uint FloatBits(float value) =>
        unchecked((uint)BitConverter.SingleToInt32Bits(value));

    private static void AssertInOrder(string source, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = source.IndexOf(value, StringComparison.Ordinal);
            Assert.True(
                current > previous,
                $"Expected source contract '{value}' after offset {previous}.");
            previous = current;
        }
    }

    private static string SliceBetween(string source, string startText, string endText)
    {
        int start = source.IndexOf(startText, StringComparison.Ordinal);
        int end = source.IndexOf(endText, start + startText.Length, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find source marker '{startText}'.");
        Assert.True(end > start, $"Could not find source marker '{endText}' after '{startText}'.");
        return source[start..end];
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repository root from test output directory.");
    }
}
