using System.Text.Json;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class GenericPreparedAssetSourceContractTests
{
    private const string ContractName =
        "ArisenEngine.Rendering.IGenericRenderPipelinePreparedAssetSource";

    [Fact]
    public void ContractExposesExactKeyRetainedBackendNeutralPreparedResources()
    {
        string contract = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/IGenericRenderPipelinePreparedAssetSource.cs");

        Assert.Contains("IGenericRenderPipelinePreparedAssetLease : IDisposable", contract, StringComparison.Ordinal);
        Assert.Contains("RuntimeAssetResidencyKey Key", contract, StringComparison.Ordinal);
        Assert.Contains("ulong DeviceGeneration", contract, StringComparison.Ordinal);
        Assert.Contains("ulong PublicationGeneration", contract, StringComparison.Ordinal);
        Assert.Contains("bool IsCurrent", contract, StringComparison.Ordinal);
        Assert.Contains("TryAcquirePreparedMesh(\n        in RuntimeAssetResidencyKey key", contract, StringComparison.Ordinal);
        Assert.Contains("out IGenericRenderPipelinePreparedMeshLease lease", contract, StringComparison.Ordinal);
        Assert.Contains("TryAcquirePreparedMaterial(\n        in RuntimeAssetResidencyKey key", contract, StringComparison.Ordinal);
        Assert.Contains("out IGenericRenderPipelinePreparedMaterialLease lease", contract, StringComparison.Ordinal);
        Assert.Contains("retained", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("setup-thread affine", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not safe for\n/// concurrent use", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetPreparedMesh", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetPreparedMaterial", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("Vulkan", contract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderRetiresExactPublicationsWithoutResolvingReplacementByGuid()
    {
        string provider = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        string materials = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderMaterialLibrary.cs");

        Assert.Contains("m_Meshes.TryGetValue(key", provider, StringComparison.Ordinal);
        Assert.Contains("m_Materials.TryGetValue(key", provider, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(current, entry)", provider, StringComparison.Ordinal);
        Assert.Contains("entry.IsCurrent = false;", provider, StringComparison.Ordinal);
        Assert.Contains("m_RetiredMeshes.Add(entry)", provider, StringComparison.Ordinal);
        Assert.Contains("RemovePreparedMaterialMapping(key)", provider, StringComparison.Ordinal);
        Assert.Contains("material.Resource,\n                material.PublicationGeneration", provider, StringComparison.Ordinal);
        Assert.Contains("entry.EstimatedGpuBytes", provider, StringComparison.Ordinal);
        Assert.Contains("BindPreparedAssetThread();", provider, StringComparison.Ordinal);
        Assert.Contains("ownership.IsCurrent = false;", materials, StringComparison.Ordinal);
        Assert.Contains("m_PreparedOwnership.Remove(ownership.Resource)", materials, StringComparison.Ordinal);
        Assert.Contains("expectedPublicationGeneration", materials, StringComparison.Ordinal);
        Assert.Contains("RetiredPreparedPublicationGpuBytes", materials, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleasePreparedMeshLease(\n        RuntimeAssetResidencyKey", provider, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidencyLifecycleCallbacksBridgeToSetupThreadOwnedRetirement()
    {
        string provider = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        int releaseStart = provider.IndexOf(
            "public void Release(RuntimeAssetResidencyKey key)",
            StringComparison.Ordinal);
        int metricsStart = provider.IndexOf(
            "public RuntimePreparedAssetProviderMetrics GetMetrics()",
            releaseStart,
            StringComparison.Ordinal);
        int updateStart = provider.IndexOf(
            "public ulong UpdateFrameContext(",
            metricsStart,
            StringComparison.Ordinal);
        Assert.True(releaseStart >= 0 && metricsStart > releaseStart && updateStart > metricsStart);

        string release = provider[releaseStart..metricsStart];
        string metrics = provider[metricsStart..updateStart];
        Assert.Contains("m_LifecycleState.RequestRelease(key)", release, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsurePreparedAssetThread", release, StringComparison.Ordinal);
        Assert.Contains("m_LifecycleState.ReadMetrics()", metrics, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsurePreparedAssetThread", metrics, StringComparison.Ordinal);
        Assert.DoesNotContain("m_Meshes", metrics, StringComparison.Ordinal);
        Assert.Contains("DrainPendingReleases();", provider, StringComparison.Ordinal);
        Assert.Contains("IsReleasePendingLocked(entry.Key)", provider, StringComparison.Ordinal);

        string lifecycle = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProviderLifecycleState.cs");
        AssertInOrder(
            lifecycle,
            "MetricsPublication currentPublication =",
            "if (currentPublication.Metrics == nextMetrics)",
            "return;",
            "new MetricsPublication(nextMetrics)");
    }

    [Fact]
    public void EnvironmentLookupTombstonesWithoutRetiringBeforeTicketPublication()
    {
        string provider = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        int lookupStart = provider.IndexOf(
            "public bool TryGetEnvironment(",
            StringComparison.Ordinal);
        int lookupEnd = provider.IndexOf(
            "public void InvalidateByAssetGuids",
            lookupStart,
            StringComparison.Ordinal);
        Assert.True(lookupStart >= 0 && lookupEnd > lookupStart);
        string lookup = provider[lookupStart..lookupEnd];

        Assert.DoesNotContain("DrainPendingReleases();", lookup, StringComparison.Ordinal);
        Assert.Contains("IsReleasePendingLocked(key)", lookup, StringComparison.Ordinal);
        Assert.Contains("environment.IsCurrent", lookup, StringComparison.Ordinal);

        int submissionStart = provider.IndexOf(
            "public void UpdateSubmittedTicket",
            StringComparison.Ordinal);
        int submissionEnd = provider.IndexOf(
            "public bool TryAcquirePreparedMesh",
            submissionStart,
            StringComparison.Ordinal);
        Assert.True(submissionStart >= 0 && submissionEnd > submissionStart);
        AssertInOrder(
            provider[submissionStart..submissionEnd],
            "m_LastSubmittedTicket = Math.Max",
            "DrainPendingReleases();");
    }

    [Fact]
    public void AssetInvalidationTransitionsResidencyBeforeDependenciesCanRebuild()
    {
        string provider = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        int methodStart = provider.IndexOf(
            "public void InvalidateByAssetGuids",
            StringComparison.Ordinal);
        int methodEnd = provider.IndexOf(
            "public void ReleaseAll",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        string invalidation = provider[methodStart..methodEnd];

        Assert.Contains("m_MaterialLibrary.InvalidateByAssetGuids", invalidation, StringComparison.Ordinal);
        Assert.Contains("m_ResidencyService.InvalidatePreparedProvider", invalidation, StringComparison.Ordinal);
        Assert.DoesNotContain("m_Meshes.Remove", invalidation, StringComparison.Ordinal);
        Assert.DoesNotContain("m_Materials.Remove", invalidation, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialStalenessIsReportedWithoutReplacingTheCurrentPublication()
    {
        string materials = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderMaterialLibrary.cs");
        int methodStart = materials.IndexOf(
            "private Guid EnsurePreparedAtIndex",
            StringComparison.Ordinal);
        int methodEnd = materials.IndexOf(
            "private ulong NextPreparedPublicationGeneration",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        string preparation = materials[methodStart..methodEnd];

        AssertInOrder(
            preparation,
            "if (entry.Resource is { } current)",
            "current.IsValid && !current.IsSourceStale()",
            ": entry.MaterialGuid;",
            "var resource = new RHIMaterialResource(",
            "m_PreparedOwnership.Add(",
            "entry.Resource = resource;",
            "m_Materials[index] = entry;");
        Assert.DoesNotContain("previous", preparation, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetirePreparedResource(\n                    current",
            preparation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DetectedMaterialStalenessRoutesThroughResidencyBeforeRetry()
    {
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string provider = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        int methodStart = pipeline.IndexOf(
            "private void EnsurePreparedMaterials",
            StringComparison.Ordinal);
        int methodEnd = pipeline.IndexOf(
            "private static bool ContainsGuid",
            methodStart,
            StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        string preparation = pipeline[methodStart..methodEnd];

        AssertInOrder(
            preparation,
            "Guid[] staleMaterialGuids = m_MaterialLibrary.EnsurePrepared(",
            "ApplyAssetInvalidations(staleMaterialGuids);",
            "Guid[] unresolvedStaleGuids = m_MaterialLibrary.EnsurePrepared(",
            "material invalidation did not retire every stale current publication");

        int providerStart = provider.IndexOf(
            "private RuntimePreparedAssetResult PrepareMaterial",
            StringComparison.Ordinal);
        int providerEnd = provider.IndexOf(
            "private RuntimePreparedAssetResult PrepareEnvironment",
            providerStart,
            StringComparison.Ordinal);
        Assert.True(providerStart >= 0 && providerEnd > providerStart);
        string providerPreparation = provider[providerStart..providerEnd];
        AssertInOrder(
            providerPreparation,
            "Guid staleMaterialGuid = m_MaterialLibrary.EnsurePrepared(",
            "if (staleMaterialGuid != Guid.Empty)",
            "RuntimePreparedAssetResult.Waiting(",
            "setup coordinator must invalidate its current publication before replacement");
    }

    [Fact]
    public void ProviderRegistersContractAndConsumerRequiresIt()
    {
        string provider = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericPreparedAssetProvider.cs");
        string package = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipelinePackage.cs");

        Assert.Contains("IGenericRenderPipelinePreparedAssetSource", provider, StringComparison.Ordinal);
        Assert.Contains(
            "RegisterService<IGenericRenderPipelinePreparedAssetSource>",
            package,
            StringComparison.Ordinal);

        Assert.Contains(
            ContractName,
            ReadServiceContracts(
                "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/package.json",
                "provides"));
        Assert.Contains(
            ContractName,
            ReadServiceContracts(
                "Arisen/Development/PackageGame/Local/com.arisen.vegetation.generic-renderpipeline/package.json",
                "requires"));
    }

    private static string[] ReadServiceContracts(string relativePath, string listName)
    {
        using JsonDocument document = JsonDocument.Parse(ReadRepoFile(relativePath));
        return document.RootElement
            .GetProperty("services")
            .GetProperty(listName)
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

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

        throw new DirectoryNotFoundException("Could not locate the Arisen repository root.");
    }

    private static void AssertInOrder(string source, params string[] values)
    {
        int previous = -1;
        foreach (string value in values)
        {
            int current = source.IndexOf(value, previous + 1, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{value}' after offset {previous}.");
            previous = current;
        }
    }
}
