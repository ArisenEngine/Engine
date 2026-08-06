using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationGpuResourceCleanupContractTests
{
    [Fact]
    public void ClusterRetainsGpuBufferUntilRetryableDisposalCompletes()
    {
        string source = ReadFactorySource();
        string clusterResource = SliceBetween(
            source,
            "private sealed class VegetationClusterGpuResource",
            "private sealed class VegetationGpuBuffer");
        int disposeStart = clusterResource.IndexOf(
            "public void Dispose()",
            StringComparison.Ordinal);
        Assert.True(disposeStart >= 0);
        string dispose = clusterResource[disposeStart..];

        AssertInOrder(
            dispose,
            "Volatile.Read(ref m_InstanceBuffer)",
            "instanceBuffer.Dispose();",
            "Interlocked.CompareExchange(ref m_InstanceBuffer, null, instanceBuffer);",
            "Volatile.Read(ref m_DependencyLeases)",
            "dependencyLeases.Dispose();",
            "ref m_DependencyLeases");
        Assert.DoesNotContain(
            "Interlocked.Exchange(ref m_InstanceBuffer, null)",
            dispose,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyAcquisitionPrecedesPackingAndRollsBackInReverse()
    {
        string source = ReadFactorySource();
        string create = SliceBetween(
            source,
            "public VegetationGpuResourceBuildResult TryCreate(",
            "public void RequestRelease(IVegetationClusterGpuResource resource)");
        AssertInOrder(
            create,
            "ValidateClosure(cluster, species, pages);",
            "TryAcquireDependencies(",
            "BuildRecords(cluster, species, pages);",
            "new VegetationGpuInstance[records.Length]",
            "dependencyLeases.DependenciesCurrent",
            "VegetationGpuBuffer.Create(");

        string acquire = SliceBetween(
            source,
            "private bool TryAcquireDependencies(",
            "private static RuntimeAssetResidencyKey CreateMeshKey(");
        Assert.Contains("RuntimeAssetResidencyKey", acquire, StringComparison.Ordinal);
        Assert.Contains("meshLease.Key != meshKey", acquire, StringComparison.Ordinal);
        Assert.Contains("materialLease.Key != materialKey", acquire, StringComparison.Ordinal);
        AssertInOrder(
            acquire,
            "finally",
            "if (!ownershipTransferred)",
            "DisposeAcquiredDependencies(acquired);");

        string rollback = SliceBetween(
            source,
            "private static void DisposeAcquiredDependencies(",
            "private void RetainPendingBufferRelease(");
        Assert.Contains(
            "for (int index = acquired.Count - 1; index >= 0; index--)",
            rollback,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyLeaseDisposalIsPartialFailureRetryable()
    {
        string source = ReadFactorySource();
        string dependencySet = SliceBetween(
            source,
            "private sealed class PreparedDependencyLeaseSet",
            "private sealed class VegetationClusterGpuResource");

        Assert.Contains(
            "for (int index = m_OwnedLeases.Length - 1; index >= 0; index--)",
            dependencySet,
            StringComparison.Ordinal);
        AssertInOrder(
            dependencySet,
            "lease.Dispose();",
            "m_OwnedLeases[index] = null;",
            "catch (Exception error)",
            "throw new AggregateException(");
    }

    [Fact]
    public void GpuBufferDisposalRetainsBufferUntilBindlessOwnershipIsReleased()
    {
        string source = ReadFactorySource();
        string gpuBuffer = source[source.IndexOf(
            "private sealed class VegetationGpuBuffer",
            StringComparison.Ordinal)..];
        string dispose = SliceBetween(
            gpuBuffer,
            "public void Dispose()",
            "private static unsafe RHIBufferHandle Upload(");

        Assert.Contains("lock (m_DisposeGate)", dispose, StringComparison.Ordinal);
        AssertInOrder(
            dispose,
            "m_Factory.UnregisterBindlessResourceBuffer(BindlessIndex);",
            "BindlessIndex = InvalidBindlessIndex;",
            "catch (Exception error)",
            "if (BindlessIndex == InvalidBindlessIndex && Buffer.IsValid)",
            "m_Factory.ReleaseBuffer(Buffer);",
            "Buffer = RHIBufferHandle.Invalid;");
        AssertInOrder(
            dispose,
            "if (!Buffer.IsValid && BindlessIndex == InvalidBindlessIndex)",
            "m_Factory = default;",
            "if (failures != null)",
            "throw new AggregateException(");
        Assert.Contains(
            "Vegetation GPU buffer ownership cannot be released without a valid RHI factory.",
            dispose,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructionFailuresRetainBuffersForFactoryOwnedRetry()
    {
        string source = ReadFactorySource();
        string gpuBuffer = source[source.IndexOf(
            "private sealed class VegetationGpuBuffer",
            StringComparison.Ordinal)..];
        string create = SliceBetween(
            gpuBuffer,
            "public static unsafe VegetationGpuBuffer Create(",
            "public void Dispose()");

        AssertInOrder(
            create,
            "try\n            {\n                bindlessIndex = factory.RegisterBindlessResourceBuffer(buffer);",
            "catch (Exception registrationFailure)",
            "TryReleaseConstructionBuffer(",
            "retainPendingBuffer",
            "throw new AggregateException(",
            "throw;");
        Assert.Contains(
            "if (bindlessIndex == InvalidBindlessIndex)",
            create,
            StringComparison.Ordinal);
        Assert.Contains(
            "registration and rollback failed.",
            create,
            StringComparison.Ordinal);

        string upload = SliceBetween(
            gpuBuffer,
            "private static unsafe RHIBufferHandle Upload(",
            "private static void TryReleaseConstructionBuffer(");
        AssertInOrder(
            upload,
            "TryReleaseConstructionBuffer(\n                factory,\n                ref staging",
            "if (uploadFailure != null || cleanupFailures.Count != 0)",
            "TryReleaseConstructionBuffer(\n                    factory,\n                    ref destination",
            "construction cleanup failed.");

        AssertInOrder(
            source,
            "private readonly List<PendingBufferRelease> m_PendingBufferReleases",
            "RetainPendingBufferRelease",
            "ReleasePendingBufferReleases();",
            "pending.Factory.ReleaseBuffer(pending.Buffer);",
            "m_PendingBufferReleases.RemoveAt(index);");
        Assert.Contains(
            "retainPendingBuffer(factory, owned, name);",
            gpuBuffer,
            StringComparison.Ordinal);
        Assert.Contains(
            "buffer = RHIBufferHandle.Invalid;",
            gpuBuffer,
            StringComparison.Ordinal);
    }

    private static string ReadFactorySource() => ReadRepoFile(
        "Arisen/Development/PackageGame/Local/" +
        "com.arisen.vegetation.generic-renderpipeline/Managed/" +
        "VegetationGpuResourceFactory.cs").Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void AssertInOrder(string source, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = source.IndexOf(value, previous + 1, StringComparison.Ordinal);
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
