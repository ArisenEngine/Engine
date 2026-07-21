using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class PointLightSnapshotExtractorTests
{
    [Fact]
    public void Extract_AcceptsEnabledLightsWithTransformsAndReportsDrops()
    {
        var entityManager = new EntityManager();

        AddPointLight(
            entityManager,
            new Vector3(99.0f, 99.0f, 99.0f),
            Vector3.One,
            intensity: 1.0f,
            range: 4.0f,
            enabled: false,
            withTransform: true);
        AddPointLight(
            entityManager,
            new Vector3(1.0f, 2.0f, 3.0f),
            new Vector3(1.0f, 0.5f, 0.25f),
            intensity: 2.0f,
            range: 3.0f,
            enabled: true,
            withTransform: true);
        AddPointLight(
            entityManager,
            new Vector3(4.0f, 5.0f, 6.0f),
            new Vector3(0.25f, 0.5f, 1.0f),
            intensity: 1.5f,
            range: 4.0f,
            enabled: true,
            withTransform: true);
        AddPointLight(
            entityManager,
            new Vector3(7.0f, 8.0f, 9.0f),
            Vector3.One,
            intensity: 1.0f,
            range: 4.0f,
            enabled: true,
            withTransform: false);
        AddPointLight(
            entityManager,
            new Vector3(-1.0f, 1.0f, 0.0f),
            Vector3.One,
            intensity: 1.0f,
            range: 2.0f,
            enabled: true,
            withTransform: true);
        AddPointLight(
            entityManager,
            new Vector3(-2.0f, 1.5f, 0.5f),
            Vector3.One,
            intensity: 1.0f,
            range: 2.0f,
            enabled: true,
            withTransform: true);
        AddPointLight(
            entityManager,
            new Vector3(-3.0f, 2.0f, 1.0f),
            Vector3.One,
            intensity: 1.0f,
            range: 2.0f,
            enabled: true,
            withTransform: true);

        var pointLightPool = entityManager.GetPool<PointLightComponent>();
        var transformPool = entityManager.GetPool<TransformComponent>();
        Span<PointLight> destination = stackalloc PointLight[PointLightSnapshotExtractor.MaxPointLightsPerFrame];

        var stats = PointLightSnapshotExtractor.Extract(
            new ReadOnlySpan<PointLightComponent>(pointLightPool.GetRawComponentArray(), 0, pointLightPool.Count),
            new ReadOnlySpan<Entity>(pointLightPool.GetRawEntityArray(), 0, pointLightPool.Count),
            transformPool,
            destination);

        Assert.Equal(7, stats.SourceCount);
        Assert.Equal(6, stats.EnabledCount);
        Assert.Equal(4, stats.AcceptedCount);
        Assert.Equal(1, stats.MissingTransformCount);
        Assert.Equal(0, stats.InvalidInputCount);
        Assert.Equal(2, stats.DroppedCount);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), destination[0].Position);
        Assert.Equal(new Vector3(1.0f, 0.5f, 0.25f), destination[0].Color);
        Assert.Equal(2.0f, destination[0].Intensity);
        Assert.Equal(3.0f, destination[0].Range);
        Assert.Equal(new Vector3(-2.0f, 1.5f, 0.5f), destination[3].Position);
    }

    [Fact]
    public void Extract_ReportsEnabledLightAsDroppedWhenDestinationIsEmpty()
    {
        var entityManager = new EntityManager();
        AddPointLight(
            entityManager,
            Vector3.One,
            Vector3.One,
            intensity: 1.0f,
            range: 4.0f,
            enabled: true,
            withTransform: true);

        var pointLightPool = entityManager.GetPool<PointLightComponent>();
        var stats = PointLightSnapshotExtractor.Extract(
            new ReadOnlySpan<PointLightComponent>(pointLightPool.GetRawComponentArray(), 0, pointLightPool.Count),
            new ReadOnlySpan<Entity>(pointLightPool.GetRawEntityArray(), 0, pointLightPool.Count),
            entityManager.GetPool<TransformComponent>(),
            Span<PointLight>.Empty);

        Assert.Equal(1, stats.SourceCount);
        Assert.Equal(1, stats.EnabledCount);
        Assert.Equal(0, stats.AcceptedCount);
        Assert.Equal(0, stats.MissingTransformCount);
        Assert.Equal(0, stats.InvalidInputCount);
        Assert.Equal(1, stats.DroppedCount);
    }

    [Fact]
    public void ExtractRejectsNonFiniteOriginRelativeInput()
    {
        var entityManager = new EntityManager();
        AddPointLight(
            entityManager,
            new Vector3(float.NaN, 0, 0),
            Vector3.One,
            intensity: 1,
            range: 4,
            enabled: true,
            withTransform: true);
        var pool = entityManager.GetPool<PointLightComponent>();
        Span<PointLight> destination = stackalloc PointLight[1];

        PointLightExtractionStats stats = PointLightSnapshotExtractor.Extract(
            pool.GetRawComponentArray().AsSpan(0, pool.Count),
            pool.GetRawEntityArray().AsSpan(0, pool.Count),
            entityManager.GetPool<TransformComponent>(),
            destination);

        Assert.Equal(0, stats.AcceptedCount);
        Assert.Equal(1, stats.InvalidInputCount);
        Assert.Equal(1, stats.DroppedCount);
    }

    private static void AddPointLight(
        EntityManager entityManager,
        Vector3 position,
        Vector3 color,
        float intensity,
        float range,
        bool enabled,
        bool withTransform)
    {
        var entity = entityManager.CreateEntity();
        if (withTransform)
        {
            entityManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = Quaternion.Identity,
                Scale = Vector3.One
            });
        }

        entityManager.AddComponent(entity, new PointLightComponent
        {
            Color = color,
            Intensity = intensity,
            Range = range,
            Enabled = enabled ? (byte)1 : (byte)0
        });
    }
}
