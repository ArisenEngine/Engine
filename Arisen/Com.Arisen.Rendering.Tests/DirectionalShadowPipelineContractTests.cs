using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class DirectionalShadowPipelineContractTests
{
    [Fact]
    public void StaticMeshAndTerrainCasterPreparationStayBoundedAndDiagnosed()
    {
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string terrainFeature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainGenericRenderPipelineFeature.cs");

        Assert.Contains(
            "MaximumDirectionalShadowDrawCommands = 65536",
            pipeline,
            StringComparison.Ordinal);
        Assert.Contains(
            "Directional shadow draw budget exhausted",
            pipeline,
            StringComparison.Ordinal);
        Assert.Contains(
            "m_DroppedShadowDrawCommandCount += droppedDrawCount",
            pipeline,
            StringComparison.Ordinal);
        Assert.Contains(
            "MaximumShadowDrawCount = 65536",
            terrainFeature,
            StringComparison.Ordinal);
        Assert.Contains(
            "m_ShadowDrawCount >= MaximumShadowDrawCount",
            terrainFeature,
            StringComparison.Ordinal);
        Assert.Contains(
            "Terrain.DroppedShadowDrawCount",
            terrainFeature,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TerrainShadowFlagsControlCasterAndReceiverPaths()
    {
        string terrainFeature = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainGenericRenderPipelineFeature.cs");
        string terrainPass = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Managed/TerrainRenderPass.cs");
        string terrainShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Assets/Shaders/Terrain.hlsl");

        Assert.Contains(
            "(patch.TileFlags & TerrainTileFlags.CastShadows) == 0",
            terrainFeature,
            StringComparison.Ordinal);
        Assert.Contains(
            "(patch.TileFlags & TerrainTileFlags.ReceiveShadows) != 0",
            terrainPass,
            StringComparison.Ordinal);
        Assert.Contains(
            "DrawConstants.shadowParameters.y < 0.5",
            terrainShader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SharedCascadeGpuLayoutMatchesBothReceiverShaders()
    {
        string frameData = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/DirectionalShadowFrameData.cs");
        string staticMeshShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Assets/Shaders/StandardLit.shader");
        string terrainShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.terrain.generic-renderpipeline/Assets/Shaders/Terrain.hlsl");

        Assert.Contains("public const int VectorCount = 22", frameData, StringComparison.Ordinal);
        Assert.Contains("STATIC_MESH_OBJECT_VECTOR_COUNT = 21", staticMeshShader, StringComparison.Ordinal);
        Assert.Contains("STATIC_MESH_OBJECT_VECTOR_COUNT * 16", staticMeshShader, StringComparison.Ordinal);
        AssertReceiverOffsets(staticMeshShader, " * 16");
        AssertReceiverOffsets(terrainShader, string.Empty);
    }

    [Fact]
    public void StaticMeshShadowPathRetainsAlphaTestAndRejectsTransparentDraws()
    {
        string pipeline = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/GenericRenderPipeline.cs");
        string shadowShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Assets/Shaders/DirectionalShadow.hlsl");

        Assert.Contains("CompactDepthWritingDrawCommands", pipeline, StringComparison.Ordinal);
        Assert.Contains("renderQueue.Class == RenderQueueClass.Transparent", pipeline, StringComparison.Ordinal);
        Assert.Contains("uint alphaTest", shadowShader, StringComparison.Ordinal);
        Assert.Contains("clip(alpha - DrawConstants.alphaCutoff)", shadowShader, StringComparison.Ordinal);
    }

    private static void AssertReceiverOffsets(string shader, string byteScale)
    {
        Assert.Contains($"shadowBufferIndex, 16{byteScale}", shader, StringComparison.Ordinal);
        Assert.Contains($"shadowBufferIndex, 17{byteScale}", shader, StringComparison.Ordinal);
        Assert.Contains($"shadowBufferIndex, 18{byteScale}", shader, StringComparison.Ordinal);
        Assert.Contains($"shadowBufferIndex, 19{byteScale}", shader, StringComparison.Ordinal);
        Assert.Contains($"shadowBufferIndex, 20{byteScale}", shader, StringComparison.Ordinal);
        Assert.Contains($"shadowBufferIndex, 21{byteScale}", shader, StringComparison.Ordinal);
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

        throw new InvalidOperationException(
            "Could not locate repository root from test output directory.");
    }
}
