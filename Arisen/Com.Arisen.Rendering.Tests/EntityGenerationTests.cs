using System.Runtime.InteropServices;
using ArisenEngine.Core.ECS;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EntityGenerationTests
{
    [Fact]
    public void ReusedEntitySlot_RejectsStaleGeneration()
    {
        var entityManager = new EntityManager();
        Entity stale = entityManager.CreateEntity();
        entityManager.AddComponent(stale, new TransformComponent());

        entityManager.DestroyEntity(stale);
        Entity current = entityManager.CreateEntity();
        entityManager.AddComponent(current, new TransformComponent());

        Assert.Equal(stale.Id, current.Id);
        Assert.NotEqual(stale.Generation, current.Generation);
        Assert.False(entityManager.IsAlive(stale));
        Assert.True(entityManager.IsAlive(current));
        Assert.False(entityManager.HasComponent<TransformComponent>(stale));
        Assert.False(entityManager.GetPool<TransformComponent>().Has(stale));
        Assert.Throws<InvalidOperationException>(() => entityManager.GetComponent<TransformComponent>(stale));
        Assert.Throws<InvalidOperationException>(() => entityManager.AddComponent(stale, new TransformComponent()));
        Assert.Throws<InvalidOperationException>(() => entityManager.DestroyEntity(stale));
        Assert.Equal(8, Marshal.SizeOf<Entity>());
        Assert.True(default(Entity).IsNull);
    }

    [Fact]
    public void BulkDestroy_CompactsSurvivingComponentsInStableOrder()
    {
        var entityManager = new EntityManager();
        Entity first = CreateWithPosition(entityManager, 1.0f);
        Entity second = CreateWithPosition(entityManager, 2.0f);
        Entity third = CreateWithPosition(entityManager, 3.0f);
        Entity fourth = CreateWithPosition(entityManager, 4.0f);

        entityManager.DestroyEntities(new[] { second, fourth });

        ComponentPool<TransformComponent> pool = entityManager.GetPool<TransformComponent>();
        Assert.Equal(2, entityManager.EntityCount);
        Assert.Equal(2, pool.Count);
        Assert.Equal(first, pool.GetRawEntityArray()[0]);
        Assert.Equal(third, pool.GetRawEntityArray()[1]);
        Assert.Equal(1.0f, pool.GetRawComponentArray()[0].Position.X);
        Assert.Equal(3.0f, pool.GetRawComponentArray()[1].Position.X);
    }

    private static Entity CreateWithPosition(EntityManager entityManager, float x)
    {
        Entity entity = entityManager.CreateEntity();
        entityManager.AddComponent(
            entity,
            new TransformComponent
            {
                Position = new System.Numerics.Vector3(x, 0.0f, 0.0f),
                Rotation = System.Numerics.Quaternion.Identity,
                Scale = System.Numerics.Vector3.One
            });
        return entity;
    }
}
