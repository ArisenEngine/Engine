using ArisenEngine.Vegetation.Assets;

namespace ArisenEngine.Core.RHI
{
    public readonly struct RHIDevice
    {
    }
}

namespace ArisenEngine.Rendering
{
    public interface IGenericRenderPipelinePreparedAssetSource
    {
    }

    internal sealed class TestGenericRenderPipelinePreparedAssetSource :
        IGenericRenderPipelinePreparedAssetSource
    {
    }

    internal sealed class TestGenericRenderPipelineRuntimeShaderRegistry :
        IGenericRenderPipelineRuntimeShaderRegistry
    {
        private string? m_OwnerId;

        public string? RegisteredOwnerId => m_OwnerId;

        public int UnregisterCallCount { get; private set; }

        public void RegisterRuntimeShaders(
            string ownerId,
            IReadOnlyList<ShaderAsset> shaders)
        {
            if (m_OwnerId != null)
            {
                throw new InvalidOperationException("Runtime shaders are already registered.");
            }
            m_OwnerId = ownerId;
        }

        public bool UnregisterRuntimeShaders(string ownerId)
        {
            UnregisterCallCount++;
            if (!string.Equals(m_OwnerId, ownerId, StringComparison.Ordinal))
            {
                return false;
            }
            m_OwnerId = null;
            return true;
        }
    }
}

namespace ArisenEngine.Vegetation.GenericRenderPipeline
{
    internal static class VegetationGenericRenderPipelineShaderAssets
    {
        public static ArisenEngine.Rendering.ShaderAsset[] CreateRuntimeShaders() =>
            Array.Empty<ArisenEngine.Rendering.ShaderAsset>();
    }

    internal sealed class VegetationOpaquePass
    {
        public VegetationOpaquePass(ArisenEngine.Core.Assets.IAssetDatabase assetDatabase)
        {
            ArgumentNullException.ThrowIfNull(assetDatabase);
        }
    }

    internal sealed class VegetationShadowPass
    {
        public VegetationShadowPass(ArisenEngine.Core.Assets.IAssetDatabase assetDatabase)
        {
            ArgumentNullException.ThrowIfNull(assetDatabase);
        }
    }

    internal sealed class VegetationGpuResourceFactory : IVegetationClusterGpuResourceFactory
    {
        public VegetationGpuResourceFactory(
            ArisenEngine.Rendering.IGenericRenderPipelinePreparedAssetSource preparedAssets)
        {
            ArgumentNullException.ThrowIfNull(preparedAssets);
        }

        public int PendingDisposalCount => 0;

        public void UpdateFrameContext(
            ArisenEngine.Core.RHI.RHIDevice device,
            ulong deviceGeneration)
        {
        }

        public VegetationGpuResourceBuildResult TryCreate(
            CookedVegetationCluster cluster,
            IReadOnlyList<CookedVegetationSpecies> species,
            IReadOnlyList<CookedVegetationInstancePage> pages) =>
            VegetationGpuResourceBuildResult.Failed(
                "GPU resource creation is not part of the lightweight rendering test host.");

        public void RequestRelease(IVegetationClusterGpuResource resource) => resource.Dispose();

        public void UpdateSubmittedTicket(ulong submittedTicket)
        {
        }

        public void ReleaseAllDeviceResources()
        {
        }
    }

    internal sealed class VegetationGenericRenderPipelineFeature :
        ArisenEngine.Rendering.IGenericRenderPipelineFeature
    {
        private readonly VegetationPreparedAssetProvider m_PreparedAssets;

        public const string Id = "com.arisen.vegetation.generic-renderpipeline";

        public VegetationGenericRenderPipelineFeature(
            IVegetationClusterRenderSource renderSource,
            IVegetationClusterDataSource clusterData,
            IVegetationDiagnosticsPublisher diagnostics,
            IVegetationAuthoringPreviewService authoringPreviews,
            VegetationPreparedAssetProvider preparedAssets,
            VegetationOpaquePass opaquePass,
            VegetationShadowPass shadowPass,
            VegetationRenderValidationMode validationMode)
        {
            ArgumentNullException.ThrowIfNull(renderSource);
            ArgumentNullException.ThrowIfNull(clusterData);
            ArgumentNullException.ThrowIfNull(diagnostics);
            ArgumentNullException.ThrowIfNull(authoringPreviews);
            ArgumentNullException.ThrowIfNull(preparedAssets);
            ArgumentNullException.ThrowIfNull(opaquePass);
            ArgumentNullException.ThrowIfNull(shadowPass);
            _ = validationMode;
            m_PreparedAssets = preparedAssets;
        }

        public string FeatureId => Id;
        public int Order => 200;

        public void ConsumeExtractedFrame(
            in ArisenEngine.Rendering.GenericRenderPipelineFeatureFrameContext context)
        {
        }

        public void PrepareResources(
            in ArisenEngine.Rendering.GenericRenderPipelineFeatureFrameContext context)
        {
        }

        public void AddRenderGraphPasses(
            ArisenEngine.Rendering.GenericRenderPipelineFeatureGraphStage stage,
            in ArisenEngine.Rendering.GenericRenderPipelineFeatureGraphContext context)
        {
        }

        public void OnFrameSubmitted(
            in ArisenEngine.Rendering.GenericRenderPipelineFeatureSubmissionContext context)
        {
        }

        public void ReleaseDeviceResources()
        {
            m_PreparedAssets.ReleaseAllDeviceResources();
        }
    }
}
