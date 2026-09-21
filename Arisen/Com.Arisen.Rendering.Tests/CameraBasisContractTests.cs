using System.Numerics;
using ArisenEngine.Core.Math;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// Locks the camera basis contract shared by the renderer, the runtime free-fly camera and the editor
/// scene view: euler degrees are (X = pitch, Y = yaw, Z = roll) over the +Z forward basis, the rendered
/// image's right axis is forward x up, and the quaternion decomposition is the exact inverse of
/// <see cref="Quaternion.CreateFromYawPitchRoll"/>.
/// </summary>
public sealed class CameraBasisContractTests
{
    private const float RadiansPerDegree = MathF.PI / 180.0f;

    [Fact]
    public void QuaternionToEulerDegrees_IsTheExactInverseOfCreateFromYawPitchRoll()
    {
        float[] yaws = { -179.0f, -137.58f, -44.0f, 0.0f, 60.0f, 179.0f };
        float[] pitches = { -89.0f, -51.2f, 0.0f, 44.0f, 89.0f };
        float[] rolls = { -150.0f, -12.5f, 0.0f, 33.3f, 120.0f };

        foreach (float yaw in yaws)
        {
            foreach (float pitch in pitches)
            {
                foreach (float roll in rolls)
                {
                    Quaternion rotation = CreateRotation(yaw, pitch, roll);
                    Vector3 euler = rotation.QuaternionToEulerDegrees();
                    Quaternion rebuilt = CreateRotation(euler.Y, euler.X, euler.Z);

                    // Angles may differ by a wrap, so compare the rotations themselves.
                    float dot = MathF.Abs(Quaternion.Dot(rotation, rebuilt));
                    Assert.True(
                        dot > 1.0f - 1.0e-5f,
                        $"yaw={yaw} pitch={pitch} roll={roll} decomposed to ({euler.X}, {euler.Y}, " +
                        $"{euler.Z}) and rebuilt to a different rotation (dot={dot}).");
                }
            }
        }
    }

    [Fact]
    public void QuaternionToEulerDegrees_KeepsTheAuthoredYawPitchRoll()
    {
        // The showcase scene's camera component is authored with this rotation.
        Vector3 euler = CreateRotation(-40.0f, 4.0f, 0.0f).QuaternionToEulerDegrees();

        Assert.Equal(4.0f, euler.X, 3);
        Assert.Equal(-40.0f, euler.Y, 3);
        Assert.Equal(0.0f, euler.Z, 3);
    }

    [Fact]
    public void LookLoop_KeepsRollAndForwardStableAcrossALongDrag()
    {
        // Mirrors the runtime free-fly loop: read the euler angles, apply the mouse delta, rebuild the
        // rotation from them. A decomposition that is not the exact inverse accumulated roll on every
        // step until the whole view flipped 180 degrees in the middle of a drag.
        Quaternion rotation = CreateRotation(-40.0f, 4.0f, 0.0f);
        Vector3 previousForward = rotation.ForwardVector();
        float maxAbsoluteRoll = 0.0f;
        float minForwardDot = 1.0f;

        for (int step = 0; step < 4000; step++)
        {
            Vector3 euler = rotation.QuaternionToEulerDegrees();
            float yaw = MathExtensions.WrapDegrees(euler.Y - 3.4f);
            float pitch = Math.Clamp(euler.X + ((step % 7) - 3) * 0.4f, -89.0f, 89.0f);
            rotation = CreateRotation(yaw, pitch, euler.Z);

            Vector3 forward = rotation.ForwardVector();
            minForwardDot = MathF.Min(minForwardDot, Vector3.Dot(previousForward, forward));
            previousForward = forward;
            maxAbsoluteRoll = MathF.Max(maxAbsoluteRoll, MathF.Abs(euler.Z));
        }

        Assert.True(maxAbsoluteRoll < 0.01f, $"roll drifted to {maxAbsoluteRoll} degrees.");
        Assert.True(minForwardDot > 0.9f, $"forward reversed mid-look (dot={minForwardDot}).");
    }

    [Fact]
    public void RightVector_IsTheWorldDirectionThatAppearsOnTheImageRight()
    {
        foreach (float yaw in new[] { -137.0f, -45.0f, 0.0f, 90.0f })
        {
            foreach (float pitch in new[] { -60.0f, 0.0f, 30.0f })
            {
                Quaternion rotation = CreateRotation(yaw, pitch, 0.0f);
                Vector3 forward = rotation.ForwardVector();
                Vector3 up = Vector3.Transform(MathExtensions.Up, rotation);
                Matrix4x4 view = Matrix4x4.CreateLookAt(Vector3.Zero, forward, up);

                Vector3 mapped = Vector3.TransformNormal(rotation.RightVector(), view);

                Assert.Equal(1.0f, mapped.X, 3);
                Assert.Equal(0.0f, mapped.Y, 3);
                Assert.Equal(0.0f, mapped.Z, 3);
            }
        }

        // An unrotated camera therefore has its image right on world -X, not on world +X.
        Assert.Equal(-1.0f, Quaternion.Identity.RightVector().X, 3);
        Assert.Equal(1.0f, MathExtensions.Right.X, 3);
    }

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(180.0f, 180.0f)]
    [InlineData(190.0f, -170.0f)]
    [InlineData(-190.0f, 170.0f)]
    [InlineData(540.0f, 180.0f)]
    public void WrapDegrees_KeepsYawInsideHalfOpenRange(float degrees, float expected)
    {
        Assert.Equal(expected, MathExtensions.WrapDegrees(degrees), 3);
    }

    [Fact]
    public void SkyAndAtmosphereRebuildTheirViewRayFromTheImageRightAxis()
    {
        string frameData = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Src/EnvironmentFrameData.cs");
        string skyShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Assets/Shaders/EnvironmentSky.hlsl");
        string atmosphereShader = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.generic-renderpipeline/Assets/Shaders/OutdoorAtmosphere.hlsl");

        Assert.Contains("rotation.RightVector()", frameData, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Vector3 right = NormalizeOrFallback(Vector3.UnitX",
            frameData,
            StringComparison.Ordinal);
        Assert.Contains("cameraRightTanHalfFov.xyz", skyShader, StringComparison.Ordinal);
        Assert.Contains("cameraRightTanHalfFov.xyz", atmosphereShader, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererAndNavigationSourcesShareTheEulerAndRightAxisContract()
    {
        string renderSubsystem = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.rendering/RenderSubsystem.cs");
        string runtimeCamera = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.packagegame/RuntimeCameraNavigationSubsystem.cs");
        string editorCamera = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Services/EditorSceneViewCameraMotion.cs");
        string editorSeed = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Services/EditorSceneViewCameraSeed.cs");

        // The renderer must reuse the shared decomposition instead of re-deriving euler angles.
        Assert.Contains(
            "transComp.Rotation.QuaternionToEulerDegrees()",
            renderSubsystem,
            StringComparison.Ordinal);
        Assert.Contains("QuaternionToEulerDegrees()", editorSeed, StringComparison.Ordinal);

        foreach (string source in new[] { runtimeCamera, editorCamera })
        {
            Assert.Contains("MathExtensions.WrapDegrees(", source, StringComparison.Ordinal);
            Assert.Contains("RightVector()", source, StringComparison.Ordinal);
        }

        Assert.Contains("QuaternionToEulerDegrees()", runtimeCamera, StringComparison.Ordinal);

        // Both navigation sources must agree: dragging right turns right (yaw decreases) and dragging
        // down looks down (pitch grows).
        Assert.Contains("eulerDegrees.Y - input.MouseDeltaX", runtimeCamera, StringComparison.Ordinal);
        Assert.Contains("eulerDegrees.X + input.MouseDeltaY", runtimeCamera, StringComparison.Ordinal);
        Assert.Contains("eulerDegrees.Y - (float)(lookDeltaX", editorCamera, StringComparison.Ordinal);
        Assert.Contains("eulerDegrees.X + (float)(lookDeltaY", editorCamera, StringComparison.Ordinal);
    }

    private static Quaternion CreateRotation(float yawDegrees, float pitchDegrees, float rollDegrees) =>
        Quaternion.CreateFromYawPitchRoll(
            yawDegrees * RadiansPerDegree,
            pitchDegrees * RadiansPerDegree,
            rollDegrees * RadiansPerDegree);

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

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
