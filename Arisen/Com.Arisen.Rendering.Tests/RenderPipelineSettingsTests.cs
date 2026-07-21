using ArisenEngine.Core.Assets;
using ArisenEngine.Rendering;
using ArisenKernel.Packages;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderPipelineSettingsTests
{
    [Fact]
    public void GenericSettingsLoader_LoadsConsumedQualityValues()
    {
        using var temp = new TemporaryDirectory();
        string sourcePath = Path.Combine(temp.Path, "Quality.arisrenderpipeline");
        File.WriteAllText(
            sourcePath,
            """
            Version: 1
            Pipeline: GenericRP
            Name: Cinematic
            Fallback:
              ClearColor:
                R: 0.1
                G: 0.2
                B: 0.3
                A: 1.0
            Shadows:
              Enabled: true
              MapSize: 4096
              DepthBias: 0.001
              SlopeBias: 0.003
              Strength: 0.9
              PcfRadius: 2
            """);
        var asset = new AssetRecord(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            GenericRenderPipelineSettingsLoader.AssetType,
            sourcePath,
            sourcePath + ".meta",
            GenericRenderPipelineSettingsLoader.ProviderPackageId);

        var settings = GenericRenderPipelineSettingsLoader.LoadSource(asset);

        Assert.Equal("Cinematic", settings.Name);
        Assert.Equal(0.1f, settings.FallbackClearColor.r);
        Assert.Equal(0.2f, settings.FallbackClearColor.g);
        Assert.Equal(0.3f, settings.FallbackClearColor.b);
        Assert.True(settings.Shadows.Enabled);
        Assert.Equal(4096u, settings.Shadows.MapSize);
        Assert.Equal(0.001f, settings.Shadows.DepthBias);
        Assert.Equal(0.003f, settings.Shadows.SlopeBias);
        Assert.Equal(0.9f, settings.Shadows.Strength);
        Assert.Equal(2, settings.Shadows.PcfRadius);
    }

    [Theory]
    [InlineData(300, 1)]
    [InlineData(2048, 4)]
    public void GenericSettingsLoader_RejectsInvalidShadowQuality(uint mapSize, int pcfRadius)
    {
        using var temp = new TemporaryDirectory();
        string sourcePath = Path.Combine(temp.Path, "Invalid.arisrenderpipeline");
        File.WriteAllText(
            sourcePath,
            $$"""
            Version: 1
            Pipeline: GenericRP
            Fallback:
              ClearColor: { R: 0, G: 0, B: 0, A: 1 }
            Shadows:
              Enabled: true
              MapSize: {{mapSize}}
              DepthBias: 0.001
              SlopeBias: 0.002
              Strength: 1
              PcfRadius: {{pcfRadius}}
            """);
        var asset = new AssetRecord(
            Guid.NewGuid(),
            GenericRenderPipelineSettingsLoader.AssetType,
            sourcePath,
            sourcePath + ".meta",
            GenericRenderPipelineSettingsLoader.ProviderPackageId);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GenericRenderPipelineSettingsLoader.LoadSource(asset));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderSelection_ActivatesOnlyMatchingCompositionProvider()
    {
        var selection = new ProjectAssetReference
        {
            Guid = Guid.Parse("22222222-3333-4444-5555-666666666666"),
            PackageId = "com.example.pipeline"
        };
        var project = new ProjectManifest { RenderPipeline = selection };
        var provider = new RecordingProvider("com.example.pipeline");

        RenderPipelineProviderSelection.Activate(project, provider);

        Assert.Same(selection, provider.ActivatedSettings);
    }

    [Fact]
    public void ProviderSelection_AllowsSettingsOwnedByAnotherBasePackage()
    {
        var project = new ProjectManifest
        {
            RenderPipeline = new ProjectAssetReference
            {
                Guid = Guid.Parse("33333333-4444-5555-6666-777777777777"),
                PackageId = "com.example.selected"
            }
        };
        var provider = new RecordingProvider("com.example.other");

        RenderPipelineProviderSelection.Activate(project, provider);

        Assert.Same(project.RenderPipeline, provider.ActivatedSettings);
    }

    [Fact]
    public void ProviderSelection_RejectsMissingSettingsWithoutActivation()
    {
        var project = new ProjectManifest();
        var provider = new RecordingProvider("com.example.pipeline");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RenderPipelineProviderSelection.Activate(project, provider));

        Assert.Contains("must select", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(provider.ActivatedSettings);
    }

    private sealed class RecordingProvider : IRenderPipelineProvider
    {
        public RecordingProvider(string providerPackageId)
        {
            ProviderPackageId = providerPackageId;
        }

        public string ProviderPackageId { get; }
        public string SettingsAssetType => GenericRenderPipelineSettingsLoader.AssetType;
        public ProjectAssetReference? ActivatedSettings { get; private set; }

        public void Activate(ProjectAssetReference settings)
        {
            ActivatedSettings = settings;
        }

        public void Deactivate()
        {
            ActivatedSettings = null;
        }

        public void ReleaseDeviceResources()
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ArisenRenderPipelineSettingsTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
