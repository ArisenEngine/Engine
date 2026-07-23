using System.Numerics;
using ArisenEditor.Core.Services;
using ArisenEngine.Rendering.Resources;
using ArisenEngine.Resources.Serialization;
using ArisenKernel.Contracts;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorSceneViewFocusFramingTests
{
    [Fact]
    public void Create_UsesPlacedVisibleMeshBoundsAndLooksAtTheirCenter()
    {
        WorldPartitionSettings partition = CreatePartition();
        WorldCellDescriptor cell = CreateCell();
        SceneInspectionResult inspection = CreateInspection(
            new SceneEntityInspection(
                Guid.NewGuid(),
                Guid.Empty,
                Guid.Empty,
                "Visible mesh",
                new SceneTransformInspection(
                    new Vector3(10.0f, 5.0f, 20.0f),
                    Quaternion.Identity,
                    new Vector3(2.0f, 3.0f, 4.0f)),
                null,
                new SceneMeshRendererInspection(
                    EmptyAssetReference("Mesh"),
                    EmptyAssetReference("Material"),
                    0,
                    1,
                    new Vector3(1.0f, 0.0f, -1.0f),
                    new Vector3(0.5f, 1.0f, 2.0f),
                    true),
                null,
                null,
                null,
                null));

        Assert.True(EditorSceneViewFocusFraming.TryCreate(
            partition,
            cell,
            inspection,
            new Dictionary<Guid, MeshBounds>(),
            out EditorSceneViewFocusFrame frame));
        Assert.True(frame.UsesMeshBounds);
        Assert.Equal(new WorldPosition(1211.0, 12.0, -2092.0), frame.Bounds.Min);
        Assert.Equal(new WorldPosition(1213.0, 18.0, -2076.0), frame.Bounds.Max);

        WorldPosition center = new(1212.0, 15.0, -2084.0);
        Vector3 expectedForward = Vector3.Normalize(new Vector3(
            (float)(center.X - frame.Camera.Position.X),
            (float)(center.Y - frame.Camera.Position.Y),
            (float)(center.Z - frame.Camera.Position.Z)));
        Quaternion cameraRotation = Quaternion.CreateFromYawPitchRoll(
            DegreesToRadians(frame.Camera.Rotation.Y),
            DegreesToRadians(frame.Camera.Rotation.X),
            DegreesToRadians(frame.Camera.Rotation.Z));
        Vector3 actualForward = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, cameraRotation));
        Assert.True(Vector3.Dot(expectedForward, actualForward) > 0.9999f);
    }

    [Fact]
    public void Create_UsesCellBoundsWhenNoVisibleMeshCanBeFramed()
    {
        WorldCellDescriptor cell = CreateCell();
        SceneInspectionResult inspection = CreateInspection(
            CreateMeshEntity(
                Guid.NewGuid(),
                Vector3.Zero,
                Vector3.Zero,
                Vector3.One));

        Assert.True(EditorSceneViewFocusFraming.TryCreate(
            CreatePartition(),
            cell,
            inspection,
            new Dictionary<Guid, MeshBounds>(),
            out EditorSceneViewFocusFrame frame));
        Assert.False(frame.UsesMeshBounds);
        Assert.Equal(cell.Bounds, frame.Bounds);
        Assert.True(frame.Camera.IsValid);
    }

    [Fact]
    public void Create_UsesCookedMeshBoundsWhenInstanceBoundsAreMissing()
    {
        Guid meshGuid = Guid.NewGuid();
        SceneInspectionResult inspection = CreateInspection(
            CreateMeshEntity(
                meshGuid,
                new Vector3(10.0f, 5.0f, 20.0f),
                Vector3.Zero,
                new Vector3(2.0f, 3.0f, 4.0f)));
        var authoritativeBounds = new Dictionary<Guid, MeshBounds>
        {
            [meshGuid] = new MeshBounds(
                new Vector3(-2.0f, -1.0f, -3.0f),
                new Vector3(4.0f, 5.0f, 7.0f))
        };

        Assert.True(EditorSceneViewFocusFraming.TryCreate(
            CreatePartition(),
            CreateCell(),
            inspection,
            authoritativeBounds,
            out EditorSceneViewFocusFrame frame));
        Assert.True(frame.UsesMeshBounds);
        Assert.Equal(new WorldPosition(1206.0, 12.0, -2092.0), frame.Bounds.Min);
        Assert.Equal(new WorldPosition(1218.0, 30.0, -2052.0), frame.Bounds.Max);
    }

    [Fact]
    public void Create_PrefersExplicitCellFocusBoundsOverLargeMeshBounds()
    {
        Guid meshGuid = Guid.NewGuid();
        var focusBounds = new WorldBounds(
            new WorldPosition(1240.0, 20.0, -2070.0),
            new WorldPosition(1250.0, 30.0, -2060.0));
        WorldCellDescriptor cell = CreateCell() with { FocusBounds = focusBounds };
        SceneInspectionResult inspection = CreateInspection(
            CreateMeshEntity(
                meshGuid,
                new Vector3(50.0f),
                Vector3.Zero,
                new Vector3(100.0f)));
        var authoritativeBounds = new Dictionary<Guid, MeshBounds>
        {
            [meshGuid] = new MeshBounds(new Vector3(-5.0f), new Vector3(5.0f))
        };

        Assert.True(EditorSceneViewFocusFraming.TryCreate(
            CreatePartition(),
            cell,
            inspection,
            authoritativeBounds,
            out EditorSceneViewFocusFrame frame));
        Assert.False(frame.UsesMeshBounds);
        Assert.Equal(focusBounds, frame.Bounds);
    }

    private static WorldPartitionSettings CreatePartition() => new(
        new WorldPosition(1000.0, 10.0, -2000.0),
        new WorldPosition(100.0, 100.0, 100.0),
        1,
        1,
        8);

    private static WorldCellDescriptor CreateCell() => new(
        new WorldCellId(Guid.NewGuid()),
        new WorldCellKey(new WorldCellCoordinate(2, 0, -1), "surface"),
        new WorldSceneReference(Guid.NewGuid(), "com.arisen.test", string.Empty),
        new WorldBounds(
            new WorldPosition(1200.0, 10.0, -2100.0),
            new WorldPosition(1300.0, 110.0, -2000.0)),
        Array.Empty<byte>(),
        0,
        0,
        0,
        Array.Empty<WorldCellId>(),
        Array.Empty<WorldCellId>());

    private static SceneInspectionResult CreateInspection(
        params SceneEntityInspection[] entities) => new(
        true,
        "Cell",
        "Cell.arisenscene",
        entities.Length,
        0,
        entities.Count(entity => entity.MeshRenderer != null),
        0,
        0,
        0,
        0,
        entities,
        Array.Empty<string>());

    private static SceneEntityInspection CreateMeshEntity(
        Guid meshGuid,
        Vector3 position,
        Vector3 boundsExtents,
        Vector3 scale) => new(
        Guid.NewGuid(),
        Guid.Empty,
        Guid.Empty,
        "Mesh",
        new SceneTransformInspection(position, Quaternion.Identity, scale),
        null,
        new SceneMeshRendererInspection(
            new SceneAssetReferenceInspection(
                meshGuid,
                "com.arisen.test",
                "Mesh",
                "Mesh",
                string.Empty,
                true,
                string.Empty),
            EmptyAssetReference("Material"),
            0,
            1,
            Vector3.Zero,
            boundsExtents,
            true),
        null,
        null,
        null,
        null);

    private static SceneAssetReferenceInspection EmptyAssetReference(string type) => new(
        Guid.NewGuid(),
        "com.arisen.test",
        type,
        type,
        string.Empty,
        true,
        string.Empty);

    private static float DegreesToRadians(float value) => value * (MathF.PI / 180.0f);
}
