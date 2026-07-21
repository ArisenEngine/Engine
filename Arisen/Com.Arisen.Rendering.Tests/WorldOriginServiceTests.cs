using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class WorldOriginServiceTests
{
    [Fact]
    public void PartitionCoordinatesRemainIndependentOfRenderOrigin()
    {
        WorldPartitionSettings partition = CreatePartition(
            new WorldPosition(-256, -64, -256),
            new WorldPosition(256, 128, 256));
        var position = new WorldPosition(10_000_012.5, 3.0, -8_000_000.25);

        WorldCellCoordinate coordinate = WorldPartitionCoordinates.GetCoordinate(partition, position);
        WorldPosition cellOrigin = WorldPartitionCoordinates.GetCellOrigin(partition, coordinate);

        Assert.Equal(39_063, coordinate.X);
        Assert.Equal(0, coordinate.Y);
        Assert.Equal(-31_250, coordinate.Z);
        Assert.True(cellOrigin.X <= position.X && position.X < cellOrigin.X + partition.CellSize.X);
        Assert.True(cellOrigin.Z <= position.Z && position.Z < cellOrigin.Z + partition.CellSize.Z);
    }

    [Fact]
    public void FrameBoundaryRebaseShiftsTransformsOnceAndPreservesWorldPositions()
    {
        var origin = new WorldOriginService();
        origin.ConfigureForWorld(CreatePartition(default, new WorldPosition(100, 100, 100)));
        var world = new EntityManager();
        Entity parent = world.CreateEntity();
        Entity child = world.CreateEntity();
        world.AddComponent(parent, TransformAt(1010.0f, 2.0f, -3.0f));
        world.AddComponent(child, TransformAt(1011.25f, 2.5f, -2.0f));
        world.AddComponent(child, new ParentComponent { Parent = parent });

        WorldPosition parentBefore = origin.ToWorld(world.GetComponent<TransformComponent>(parent).Position);
        WorldPosition childBefore = origin.ToWorld(world.GetComponent<TransformComponent>(child).Position);
        var callbacks = new List<string>();
        WorldOriginRebase observed = default;
        origin.RebaseStarting += rebase => callbacks.Add($"before:{rebase.Sequence}");
        origin.Rebased += rebase =>
        {
            callbacks.Add($"after:{rebase.Sequence}");
            observed = rebase;
        };
        origin.RequestPrimarySource(new WorldPosition(1010.0, 2.0, -3.0));

        Assert.True(origin.ProcessAtFrameBoundary(world));

        Assert.Equal(new WorldPosition(1000, 0, 0), origin.CurrentOrigin);
        Assert.Equal(10.0f, world.GetComponent<TransformComponent>(parent).Position.X);
        Assert.Equal(11.25f, world.GetComponent<TransformComponent>(child).Position.X);
        Assert.Equal(parentBefore, origin.ToWorld(world.GetComponent<TransformComponent>(parent).Position));
        Assert.Equal(childBefore, origin.ToWorld(world.GetComponent<TransformComponent>(child).Position));
        Assert.Equal(1.25f,
            world.GetComponent<TransformComponent>(child).Position.X -
            world.GetComponent<TransformComponent>(parent).Position.X);
        Assert.Equal(["before:1", "after:1"], callbacks);
        Assert.Equal(2, observed.ShiftedTransformCount);
        Assert.False(origin.ProcessAtFrameBoundary(world));
        Assert.Equal(1, origin.RebaseSequence);
    }

    [Fact]
    public void FarCellPlacementRetainsSubMeterSeparationWithoutMutatingCookedStaging()
    {
        var origin = new WorldOriginService();
        WorldPartitionSettings partition = CreatePartition(default, new WorldPosition(256, 128, 256));
        origin.ConfigureForWorld(partition);
        WorldPosition source = new(10_000_012.5, 0, 0);
        origin.RequestPrimarySource(source);
        Assert.True(origin.ProcessAtFrameBoundary(new EntityManager()));

        WorldCellCoordinate coordinate = WorldPartitionCoordinates.GetCoordinate(partition, source);
        WorldPosition cellOrigin = WorldPartitionCoordinates.GetCellOrigin(partition, coordinate);
        SceneStagingData staging = CreateStaging(0.125f, 0.25f);
        SceneStagingData placed = SceneStagingPlacement.PlaceCell(
            staging,
            cellOrigin,
            origin.CurrentOrigin);

        float first = placed.Entities[0].Transform.Position.X;
        float second = placed.Entities[1].Transform.Position.X;
        Assert.Equal(0.125f, second - first, 5);
        Assert.Equal(0.125f, staging.Entities[0].Transform.Position.X);
        Assert.Equal(0.25f, staging.Entities[1].Transform.Position.X);
        Assert.Equal(
            (float)(cellOrigin.X + 0.125),
            (float)(cellOrigin.X + 0.25));
        Assert.Equal(
            cellOrigin.X + 0.125,
            origin.ToWorld(placed.Entities[0].Transform.Position).X,
            5);
    }

    [Fact]
    public void RebaseKeepsCameraRelativeViewAndLightInputsStable()
    {
        var origin = new WorldOriginService();
        origin.ConfigureForWorld(CreatePartition(default, new WorldPosition(100, 100, 100)));
        var world = new EntityManager();
        Entity camera = world.CreateEntity();
        Entity target = world.CreateEntity();
        Entity light = world.CreateEntity();
        world.AddComponent(camera, TransformAt(1010, 4, -5));
        world.AddComponent(target, TransformAt(1012, 4, 3));
        world.AddComponent(light, TransformAt(1008, 7, 1));
        world.AddComponent(light, new PointLightComponent
        {
            Color = Vector3.One,
            Intensity = 2,
            Range = 20,
            Enabled = 1
        });

        Vector3 viewDeltaBefore =
            world.GetComponent<TransformComponent>(target).Position -
            world.GetComponent<TransformComponent>(camera).Position;
        Vector3 lightDeltaBefore =
            world.GetComponent<TransformComponent>(light).Position -
            world.GetComponent<TransformComponent>(camera).Position;
        origin.RequestPrimarySource(origin.ToWorld(world.GetComponent<TransformComponent>(camera).Position));

        Assert.True(origin.ProcessAtFrameBoundary(world));

        Vector3 viewDeltaAfter =
            world.GetComponent<TransformComponent>(target).Position -
            world.GetComponent<TransformComponent>(camera).Position;
        Vector3 lightDeltaAfter =
            world.GetComponent<TransformComponent>(light).Position -
            world.GetComponent<TransformComponent>(camera).Position;
        Assert.Equal(viewDeltaBefore, viewDeltaAfter);
        Assert.Equal(lightDeltaBefore, lightDeltaAfter);
        Assert.All(
            world.GetPool<TransformComponent>().GetRawComponentArray().Take(
                world.GetPool<TransformComponent>().Count),
            transform => Assert.True(
                float.IsFinite(transform.Position.X) &&
                float.IsFinite(transform.Position.Y) &&
                float.IsFinite(transform.Position.Z)));
    }

    private static WorldPartitionSettings CreatePartition(
        WorldPosition partitionOrigin,
        WorldPosition cellSize) =>
        new(partitionOrigin, cellSize, 1, 1, 16);

    private static TransformComponent TransformAt(float x, float y, float z) => new()
    {
        Position = new Vector3(x, y, z),
        Rotation = Quaternion.Identity,
        Scale = Vector3.One
    };

    private static SceneStagingData CreateStaging(float firstX, float secondX)
    {
        return new SceneStagingData(
            Guid.Parse("94000000-0000-0000-0000-000000000001"),
            2,
            "Far Cell",
            "FarCell.arisenscene",
            [],
            [
                CreateEntity(Guid.Parse("94000000-0000-0000-0000-000000000002"), firstX),
                CreateEntity(Guid.Parse("94000000-0000-0000-0000-000000000003"), secondX)
            ]);
    }

    private static SceneStagingEntity CreateEntity(Guid guid, float x) =>
        new(
            guid,
            Guid.Empty,
            guid.ToString("D"),
            TransformAt(x, 0, 0),
            null,
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            string.Empty);
}
