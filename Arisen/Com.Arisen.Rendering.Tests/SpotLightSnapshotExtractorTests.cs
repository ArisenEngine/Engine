using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class SpotLightSnapshotExtractorTests
{
    [Fact]
    public void Extract_AcceptsEnabledLightsWithTransformsAndReportsDrops()
    {
        var entityManager = new EntityManager();

        AddSpotLight(
            entityManager,
            new Vector3(99.0f, 99.0f, 99.0f),
            Quaternion.Identity,
            Vector3.One,
            intensity: 1.0f,
            range: 4.0f,
            innerConeAngle: 18.0f,
            outerConeAngle: 28.0f,
            enabled: false,
            withTransform: true);
        AddSpotLight(
            entityManager,
            new Vector3(1.0f, 2.0f, 3.0f),
            Quaternion.Identity,
            new Vector3(1.0f, 0.5f, 0.25f),
            intensity: 2.0f,
            range: 3.0f,
            innerConeAngle: 12.0f,
            outerConeAngle: 24.0f,
            enabled: true,
            withTransform: true);
        AddSpotLight(
            entityManager,
            new Vector3(4.0f, 5.0f, 6.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f),
            new Vector3(0.25f, 0.5f, 1.0f),
            intensity: 1.5f,
            range: 4.0f,
            innerConeAngle: 15.0f,
            outerConeAngle: 30.0f,
            enabled: true,
            withTransform: true);
        AddSpotLight(
            entityManager,
            new Vector3(7.0f, 8.0f, 9.0f),
            Quaternion.Identity,
            Vector3.One,
            intensity: 1.0f,
            range: 4.0f,
            innerConeAngle: 18.0f,
            outerConeAngle: 28.0f,
            enabled: true,
            withTransform: false);
        AddSpotLight(
            entityManager,
            new Vector3(-1.0f, 1.0f, 0.0f),
            Quaternion.Identity,
            Vector3.One,
            intensity: 1.0f,
            range: 2.0f,
            innerConeAngle: 18.0f,
            outerConeAngle: 28.0f,
            enabled: true,
            withTransform: true);
        AddSpotLight(
            entityManager,
            new Vector3(-2.0f, 1.5f, 0.5f),
            Quaternion.Identity,
            Vector3.One,
            intensity: 1.0f,
            range: 2.0f,
            innerConeAngle: 18.0f,
            outerConeAngle: 28.0f,
            enabled: true,
            withTransform: true);
        AddSpotLight(
            entityManager,
            new Vector3(-3.0f, 2.0f, 1.0f),
            Quaternion.Identity,
            Vector3.One,
            intensity: 1.0f,
            range: 2.0f,
            innerConeAngle: 18.0f,
            outerConeAngle: 28.0f,
            enabled: true,
            withTransform: true);

        var spotLightPool = entityManager.GetPool<SpotLightComponent>();
        var transformPool = entityManager.GetPool<TransformComponent>();
        Span<SpotLight> destination = stackalloc SpotLight[SpotLightSnapshotExtractor.MaxSpotLightsPerFrame];

        var stats = SpotLightSnapshotExtractor.Extract(
            new ReadOnlySpan<SpotLightComponent>(spotLightPool.GetRawComponentArray(), 0, spotLightPool.Count),
            new ReadOnlySpan<Entity>(spotLightPool.GetRawEntityArray(), 0, spotLightPool.Count),
            transformPool,
            destination);

        Assert.Equal(7, stats.SourceCount);
        Assert.Equal(6, stats.EnabledCount);
        Assert.Equal(4, stats.AcceptedCount);
        Assert.Equal(1, stats.MissingTransformCount);
        Assert.Equal(0, stats.InvalidInputCount);
        Assert.Equal(2, stats.DroppedCount);
        Assert.Equal(new Vector3(1.0f, 2.0f, 3.0f), destination[0].Position);
        Assert.Equal(Vector3.UnitZ, destination[0].Direction);
        Assert.Equal(new Vector3(1.0f, 0.5f, 0.25f), destination[0].Color);
        Assert.Equal(2.0f, destination[0].Intensity);
        Assert.Equal(3.0f, destination[0].Range);
        Assert.Equal(MathF.Cos(12.0f * MathF.PI / 180.0f), destination[0].InnerConeCosine, precision: 5);
        Assert.Equal(MathF.Cos(24.0f * MathF.PI / 180.0f), destination[0].OuterConeCosine, precision: 5);
        Assert.True(Vector3.Distance(Vector3.UnitX, destination[1].Direction) < 0.0001f);
        Assert.Equal(new Vector3(-2.0f, 1.5f, 0.5f), destination[3].Position);
    }

    [Fact]
    public void Extract_ReportsEnabledLightAsDroppedWhenDestinationIsEmpty()
    {
        var entityManager = new EntityManager();
        AddSpotLight(
            entityManager,
            Vector3.One,
            Quaternion.Identity,
            Vector3.One,
            intensity: 1.0f,
            range: 4.0f,
            innerConeAngle: 18.0f,
            outerConeAngle: 28.0f,
            enabled: true,
            withTransform: true);

        var spotLightPool = entityManager.GetPool<SpotLightComponent>();
        var stats = SpotLightSnapshotExtractor.Extract(
            new ReadOnlySpan<SpotLightComponent>(spotLightPool.GetRawComponentArray(), 0, spotLightPool.Count),
            new ReadOnlySpan<Entity>(spotLightPool.GetRawEntityArray(), 0, spotLightPool.Count),
            entityManager.GetPool<TransformComponent>(),
            Span<SpotLight>.Empty);

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
        AddSpotLight(
            entityManager,
            Vector3.Zero,
            new Quaternion(float.NaN, 0, 0, 1),
            Vector3.One,
            intensity: 1,
            range: 4,
            innerConeAngle: 18,
            outerConeAngle: 28,
            enabled: true,
            withTransform: true);
        var pool = entityManager.GetPool<SpotLightComponent>();
        Span<SpotLight> destination = stackalloc SpotLight[1];

        SpotLightExtractionStats stats = SpotLightSnapshotExtractor.Extract(
            pool.GetRawComponentArray().AsSpan(0, pool.Count),
            pool.GetRawEntityArray().AsSpan(0, pool.Count),
            entityManager.GetPool<TransformComponent>(),
            destination);

        Assert.Equal(0, stats.AcceptedCount);
        Assert.Equal(1, stats.InvalidInputCount);
        Assert.Equal(1, stats.DroppedCount);
    }

    private static void AddSpotLight(
        EntityManager entityManager,
        Vector3 position,
        Quaternion rotation,
        Vector3 color,
        float intensity,
        float range,
        float innerConeAngle,
        float outerConeAngle,
        bool enabled,
        bool withTransform)
    {
        var entity = entityManager.CreateEntity();
        if (withTransform)
        {
            entityManager.AddComponent(entity, new TransformComponent
            {
                Position = position,
                Rotation = rotation,
                Scale = Vector3.One
            });
        }

        entityManager.AddComponent(entity, new SpotLightComponent
        {
            Color = color,
            Intensity = intensity,
            Range = range,
            InnerConeAngleDegrees = innerConeAngle,
            OuterConeAngleDegrees = outerConeAngle,
            Enabled = enabled ? (byte)1 : (byte)0
        });
    }
}
