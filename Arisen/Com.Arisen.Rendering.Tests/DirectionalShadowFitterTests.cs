using ArisenEngine.Core.RHI;
using ArisenEngine.Rendering;
using ArisenEngine.Rendering.Resources;
using System.Numerics;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class DirectionalShadowFitterTests
{
    [Fact]
    public void CreateCascadesBuildsMonotonicPracticalSplitsToConfiguredDistance()
    {
        Camera camera = CreateCamera();
        GenericShadowSettings settings = GenericShadowSettings.Default with
        {
            CascadeCount = 4,
            MaximumDistance = 320.0f,
            PracticalSplitWeight = 0.65f
        };

        DirectionalShadowCascadeSet result = DirectionalShadowFitter.CreateCascades(
            camera,
            DirectionalLight.Default.Direction,
            settings);

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Count);
        Assert.Equal(camera.NearClip, result.NearClip);
        Assert.Equal(320.0f, result.MaximumDistance);
        float previousFar = camera.NearClip;
        for (int index = 0; index < result.Count; index++)
        {
            DirectionalShadowCascade cascade = result.GetCascade(index);
            Assert.Equal(previousFar, cascade.SplitNear);
            Assert.True(cascade.SplitFar > cascade.SplitNear);
            Assert.InRange(cascade.TransitionStart, cascade.SplitNear, cascade.SplitFar);
            Assert.True(cascade.WorldUnitsPerTexel > 0.0f);
            previousFar = cascade.SplitFar;
        }
        Assert.Equal(result.MaximumDistance, previousFar);
        Assert.InRange(result.TerminalFadeStart, result.NearClip, result.MaximumDistance);
    }

    [Fact]
    public void PracticalSplitWeightSelectsUniformAndLogarithmicEndpoints()
    {
        const float nearClip = 1.0f;
        const float farClip = 256.0f;

        float uniform = DirectionalShadowFitter.CalculatePracticalSplit(
            nearClip,
            farClip,
            2,
            4,
            0.0f);
        float logarithmic = DirectionalShadowFitter.CalculatePracticalSplit(
            nearClip,
            farClip,
            2,
            4,
            1.0f);

        Assert.Equal(128.5f, uniform, 4);
        Assert.Equal(16.0f, logarithmic, 4);
    }

    [Fact]
    public void CreateCascadesFitsEveryFrustumSliceCorner()
    {
        Camera camera = CreateCamera();
        GenericShadowSettings settings = GenericShadowSettings.Default with
        {
            CascadeCount = 3,
            MaximumDistance = 180.0f
        };
        DirectionalShadowCascadeSet result = DirectionalShadowFitter.CreateCascades(
            camera,
            Vector3.Normalize(new Vector3(0.35f, 0.8f, -0.2f)),
            settings);

        Camera fittingCamera = camera;
        fittingCamera.Position = Vector3.Zero;
        Matrix4x4.Invert(
            fittingCamera.ViewMatrix * fittingCamera.ProjectionMatrix,
            out Matrix4x4 inverseViewProjection);
        Vector3[] nearCorners = ExtractCorners(inverseViewProjection, 0.0f);
        Vector3[] farCorners = ExtractCorners(inverseViewProjection, 1.0f);
        for (int cascadeIndex = 0; cascadeIndex < result.Count; cascadeIndex++)
        {
            DirectionalShadowCascade cascade = result.GetCascade(cascadeIndex);
            float nearT = (cascade.SplitNear - camera.NearClip) /
                (camera.FarClip - camera.NearClip);
            float farT = (cascade.SplitFar - camera.NearClip) /
                (camera.FarClip - camera.NearClip);
            for (int cornerIndex = 0; cornerIndex < 4; cornerIndex++)
            {
                Vector3 ray = farCorners[cornerIndex] - nearCorners[cornerIndex];
                AssertInsideShadowClip(
                    nearCorners[cornerIndex] + ray * nearT,
                    cascade.ViewProjection);
                AssertInsideShadowClip(
                    nearCorners[cornerIndex] + ray * farT,
                    cascade.ViewProjection);
            }
        }
    }

    [Fact]
    public void CreateCascadesPreservesClipCoordinatesAcrossOriginRebase()
    {
        Camera initialCamera = CreateCamera();
        GenericShadowSettings settings = GenericShadowSettings.Default with
        {
            CascadeCount = 4,
            MaximumDistance = 200.0f
        };
        Vector3 lightDirection = Vector3.Normalize(new Vector3(0.2f, 0.9f, -0.3f));
        DirectionalShadowCascadeSet initial = DirectionalShadowFitter.CreateCascades(
            initialCamera,
            lightDirection,
            settings);
        Vector3 rebaseDelta = new(4096.0f, 0.0f, -2048.0f);
        Camera rebasedCamera = initialCamera;
        rebasedCamera.Position -= rebaseDelta;
        DirectionalShadowCascadeSet rebased = DirectionalShadowFitter.CreateCascades(
            rebasedCamera,
            lightDirection,
            settings);
        Vector3 receiver = initialCamera.Position + new Vector3(3.0f, -1.0f, -24.0f);

        for (int index = 0; index < initial.Count; index++)
        {
            Vector3 initialNdc = TransformToNdc(
                receiver - initial.CameraPosition,
                initial.GetCascade(index).ViewProjection);
            Vector3 rebasedNdc = TransformToNdc(
                receiver - rebaseDelta - rebased.CameraPosition,
                rebased.GetCascade(index).ViewProjection);
            Assert.InRange(Vector3.Distance(initialNdc, rebasedNdc), 0.0f, 0.00001f);
            Assert.Equal(
                initial.GetCascade(index).ViewProjection,
                rebased.GetCascade(index).ViewProjection);
        }
    }

    [Fact]
    public void CreateFallsBackWhenReceiverBoundsAreUnavailable()
    {
        var result = DirectionalShadowFitter.Create(
            DirectionalLight.Default.Direction,
            MeshBounds.Empty,
            2048);

        Assert.False(result.IsSceneFitted);
        Assert.Equal(10.0f, result.Diameter);
        Assert.True(result.WorldUnitsPerTexel > 0.0f);
        AssertFinite(result.ViewProjection);
    }

    [Fact]
    public void CreateFitsEveryReceiverBoundsCornerInsideShadowClipSpace()
    {
        var bounds = new MeshBounds(
            new Vector3(-2.0f, -1.0f, -3.0f),
            new Vector3(4.0f, 5.0f, 2.0f));

        var result = DirectionalShadowFitter.Create(
            Vector3.Normalize(new Vector3(0.35f, 0.8f, -0.2f)),
            bounds,
            2048);

        Assert.True(result.IsSceneFitted);
        Assert.True(result.Diameter > 0.0f);
        Assert.True(result.Depth > 0.0f);
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            var corner = new Vector3(
                (cornerIndex & 1) == 0 ? bounds.Min.X : bounds.Max.X,
                (cornerIndex & 2) == 0 ? bounds.Min.Y : bounds.Max.Y,
                (cornerIndex & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
            var clip = Vector4.Transform(new Vector4(corner, 1.0f), result.ViewProjection);
            var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
            Assert.InRange(ndc.X, -1.001f, 1.001f);
            Assert.InRange(ndc.Y, -1.001f, 1.001f);
            Assert.InRange(ndc.Z, -0.001f, 1.001f);
        }
    }

    [Fact]
    public void CreateKeepsProjectionStableForSubTexelReceiverMotion()
    {
        var bounds = new MeshBounds(
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f));
        var initial = DirectionalShadowFitter.Create(Vector3.UnitY, bounds, 1024);
        float subTexelShift = initial.WorldUnitsPerTexel * 0.25f;
        var shiftedBounds = new MeshBounds(
            bounds.Min + new Vector3(subTexelShift, 0.0f, 0.0f),
            bounds.Max + new Vector3(subTexelShift, 0.0f, 0.0f));

        var shifted = DirectionalShadowFitter.Create(Vector3.UnitY, shiftedBounds, 1024);

        Assert.True(initial.IsSceneFitted);
        Assert.Equal(initial.SnappedLightSpaceCenter, shifted.SnappedLightSpaceCenter);
        Assert.Equal(initial.ViewProjection, shifted.ViewProjection);
    }

    [Fact]
    public void FittedProjectionCullsCasterOutsideReceiverShadowVolume()
    {
        var receiverBounds = new MeshBounds(
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(1.0f, 1.0f, 1.0f));
        var projection = DirectionalShadowFitter.Create(
            Vector3.Normalize(new Vector3(0.4f, 1.0f, -0.25f)),
            receiverBounds,
            1024);
        var localBounds = new MeshBounds(
            new Vector3(-0.5f),
            new Vector3(0.5f));
        var caster = new StaticMeshRenderItem
        {
            MeshGuid = Guid.NewGuid(),
            LocalToWorld = Matrix4x4.Identity,
            Visible = 1
        };

        Assert.True(StaticMeshFrustumCuller.IsVisible(
            caster,
            localBounds,
            projection.ViewProjection));

        caster.LocalToWorld = Matrix4x4.CreateTranslation(50.0f, 0.0f, 0.0f);

        Assert.False(StaticMeshFrustumCuller.IsVisible(
            caster,
            localBounds,
            projection.ViewProjection));
    }

    [Fact]
    public void BoundsAccumulatorUnionsBoundedVisibleItems()
    {
        var accumulator = new DirectionalShadowBoundsAccumulator();

        Assert.True(accumulator.Add(new MeshBounds(
            new Vector3(-2.0f, -1.0f, 0.0f),
            new Vector3(0.0f, 3.0f, 2.0f))));
        Assert.True(accumulator.Add(new MeshBounds(
            new Vector3(-1.0f, -4.0f, -2.0f),
            new Vector3(5.0f, 1.0f, 1.0f))));

        Assert.True(accumulator.IsValid);
        Assert.Equal(2, accumulator.Count);
        Assert.Equal(new Vector3(-2.0f, -4.0f, -2.0f), accumulator.Bounds.Min);
        Assert.Equal(new Vector3(5.0f, 3.0f, 2.0f), accumulator.Bounds.Max);
    }

    [Fact]
    public void CascadeDrawRangesRequireCompactCoverageAndPreserveDropCount()
    {
        var ranges = new DirectionalShadowCascadeDrawRangeSet(
            4,
            9,
            3,
            new DirectionalShadowCascadeDrawRange(0, 3),
            new DirectionalShadowCascadeDrawRange(3, 2),
            new DirectionalShadowCascadeDrawRange(5, 4),
            new DirectionalShadowCascadeDrawRange(9, 0));

        Assert.Equal(4, ranges.Count);
        Assert.Equal(9, ranges.TotalDrawCount);
        Assert.Equal(3, ranges.DroppedDrawCount);
        Assert.Equal(new DirectionalShadowCascadeDrawRange(5, 4), ranges.GetRange(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ranges.GetRange(4));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DirectionalShadowCascadeDrawRangeSet(
                2,
                4,
                0,
                new DirectionalShadowCascadeDrawRange(0, 2),
                new DirectionalShadowCascadeDrawRange(1, 2),
                default,
                default));
        Assert.Throws<ArgumentException>(() =>
            new DirectionalShadowCascadeDrawRangeSet(
                2,
                5,
                0,
                new DirectionalShadowCascadeDrawRange(0, 2),
                new DirectionalShadowCascadeDrawRange(2, 2),
                default,
                default));
    }

    [Fact]
    public void CasterTransformsAndBoundsRemainCameraRelativeAcrossRebase()
    {
        Matrix4x4 localToWorld = Matrix4x4.CreateRotationY(0.35f) *
            Matrix4x4.CreateTranslation(4100.5f, 32.0f, -2032.25f);
        var bounds = new MeshBounds(
            new Vector3(4098.0f, 30.0f, -2035.0f),
            new Vector3(4103.0f, 36.0f, -2029.0f));
        var camera = new Vector3(4096.0f, 24.0f, -2048.0f);
        var rebaseDelta = new Vector3(8192.0f, -64.0f, 4096.0f);

        Matrix4x4 shiftedLocalToWorld = localToWorld;
        shiftedLocalToWorld.M41 += rebaseDelta.X;
        shiftedLocalToWorld.M42 += rebaseDelta.Y;
        shiftedLocalToWorld.M43 += rebaseDelta.Z;
        var shiftedBounds = new MeshBounds(
            bounds.Min + rebaseDelta,
            bounds.Max + rebaseDelta);
        Vector3 shiftedCamera = camera + rebaseDelta;

        Assert.Equal(
            DirectionalShadowCoordinateSpace.ToCameraRelative(localToWorld, camera),
            DirectionalShadowCoordinateSpace.ToCameraRelative(
                shiftedLocalToWorld,
                shiftedCamera));
        Assert.Equal(
            DirectionalShadowCoordinateSpace.ToCameraRelative(bounds, camera),
            DirectionalShadowCoordinateSpace.ToCameraRelative(
                shiftedBounds,
                shiftedCamera));
    }

    private static void AssertFinite(Matrix4x4 matrix)
    {
        Assert.All(
            new[]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            },
            value => Assert.True(float.IsFinite(value)));
    }

    private static Camera CreateCamera()
    {
        return new Camera
        {
            FieldOfView = 60.0f,
            NearClip = 0.1f,
            FarClip = 1000.0f,
            AspectRatio = 16.0f / 9.0f,
            ProjectionType = CameraProjectionType.Perspective,
            Position = new Vector3(4.0f, 6.0f, 12.0f),
            Rotation = new Vector3(-12.0f, 180.0f, 0.0f)
        };
    }

    private static Vector3[] ExtractCorners(Matrix4x4 inverseViewProjection, float depth)
    {
        var corners = new Vector3[4];
        for (int index = 0; index < corners.Length; index++)
        {
            float x = (index & 1) == 0 ? -1.0f : 1.0f;
            float y = (index & 2) == 0 ? -1.0f : 1.0f;
            Vector4 world = Vector4.Transform(
                new Vector4(x, y, depth, 1.0f),
                inverseViewProjection);
            corners[index] = new Vector3(world.X, world.Y, world.Z) / world.W;
        }
        return corners;
    }

    private static void AssertInsideShadowClip(Vector3 worldPosition, Matrix4x4 viewProjection)
    {
        Vector3 ndc = TransformToNdc(worldPosition, viewProjection);
        Assert.InRange(ndc.X, -1.001f, 1.001f);
        Assert.InRange(ndc.Y, -1.001f, 1.001f);
        Assert.InRange(ndc.Z, -0.001f, 1.001f);
    }

    private static Vector3 TransformToNdc(Vector3 worldPosition, Matrix4x4 viewProjection)
    {
        Vector4 clip = Vector4.Transform(new Vector4(worldPosition, 1.0f), viewProjection);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }
}
