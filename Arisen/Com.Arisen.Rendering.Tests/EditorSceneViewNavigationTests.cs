using System.Numerics;
using ArisenEditor.Core.Services;
using ArisenEngine.Resources.Serialization;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorSceneViewNavigationTests
{
    private const float Tolerance = 1.0e-4f;

    [Fact]
    public void Input_IsNotConsumedUntilASceneViewAttaches()
    {
        var input = new EditorSceneViewNavigationInput();
        input.AddLookDelta(25.0, -10.0);
        input.SetKey(EditorSceneViewNavigationKeys.Forward, down: true);

        Assert.False(input.TryConsume(out _));

        input.SetActive(true);
        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot snapshot));
        Assert.Equal(25.0, snapshot.LookDeltaX);
        Assert.Equal(-10.0, snapshot.LookDeltaY);
        Assert.Equal(EditorSceneViewNavigationKeys.Forward, snapshot.Keys);
    }

    [Fact]
    public void Input_MotionIsConsumedExactlyOnce()
    {
        var input = new EditorSceneViewNavigationInput();
        input.SetActive(true);
        input.SetLooking(true);
        input.AddLookDelta(4.0, 8.0);
        input.AddDollyDelta(1.0);

        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot first));
        Assert.Equal(4.0, first.LookDeltaX);
        Assert.Equal(8.0, first.LookDeltaY);
        Assert.Equal(1.0, first.DollyDelta);
        Assert.True(first.HasLookInput);
        Assert.True(first.HasDollyInput);

        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot second));
        Assert.Equal(0.0, second.LookDeltaX);
        Assert.Equal(0.0, second.LookDeltaY);
        Assert.Equal(0.0, second.DollyDelta);
        Assert.False(second.HasInput);
    }

    [Fact]
    public void Input_DetachingDropsKeysAndPendingMotion()
    {
        var input = new EditorSceneViewNavigationInput();
        input.SetActive(true);
        input.SetLooking(true);
        input.SetKey(EditorSceneViewNavigationKeys.FastMove, down: true);
        input.AddLookDelta(100.0, 100.0);
        input.AddDollyDelta(-2.0);

        input.SetActive(false);
        Assert.False(input.TryConsume(out _));

        input.SetActive(true);
        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot snapshot));
        Assert.False(snapshot.HasInput);
        Assert.Equal(EditorSceneViewNavigationKeys.None, snapshot.Keys);
    }

    [Fact]
    public void Input_ReleasingLookDropsPendingLookMotionButKeepsKeys()
    {
        var input = new EditorSceneViewNavigationInput();
        input.SetActive(true);
        input.SetKey(EditorSceneViewNavigationKeys.StrafeRight, down: true);
        input.SetLooking(true);
        input.AddLookDelta(12.0, 0.0);
        input.SetLooking(false);

        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot snapshot));
        Assert.False(snapshot.IsLooking);
        Assert.False(snapshot.HasLookInput);
        Assert.Equal(0.0, snapshot.LookDeltaX);
        Assert.Equal(EditorSceneViewNavigationKeys.StrafeRight, snapshot.Keys);
    }

    [Fact]
    public void Input_KeysAreLevelTriggered()
    {
        var input = new EditorSceneViewNavigationInput();
        input.SetActive(true);
        input.SetKey(EditorSceneViewNavigationKeys.Forward, down: true);
        input.SetKey(EditorSceneViewNavigationKeys.FastMove, down: true);

        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot pressed));
        Assert.Equal(
            EditorSceneViewNavigationKeys.Forward | EditorSceneViewNavigationKeys.FastMove,
            pressed.Keys);

        input.SetKey(EditorSceneViewNavigationKeys.Forward, down: false);
        Assert.True(input.TryConsume(out EditorSceneViewNavigationSnapshot released));
        Assert.Equal(EditorSceneViewNavigationKeys.FastMove, released.Keys);
    }

    [Fact]
    public void Look_TurnsTowardTheDragDirectionAndWrapsYaw()
    {
        Vector3 level = new(0.0f, 179.0f, 0.0f);

        // The image's right is forward x up, so dragging right turns the view right and decreases
        // yaw. Wrapping keeps a long drag inside the range where degrees stay precise.
        Vector3 lookedRight = EditorSceneViewCameraMotion.ApplyLook(level, 100.0, 0.0);
        Assert.Equal(165.0f, lookedRight.Y, 3);
        Assert.Equal(0.0f, lookedRight.X, 3);

        Vector3 lookedLeft = EditorSceneViewCameraMotion.ApplyLook(
            new Vector3(0.0f, -179.0f, 0.0f),
            -100.0,
            0.0);
        Assert.Equal(-165.0f, lookedLeft.Y, 3);
    }

    [Fact]
    public void Look_ClampsPitchAtBothLimitsWithoutInvertingTheDrag()
    {
        Vector3 level = new(0.0f, 0.0f, 0.0f);

        // Dragging down looks down (pitch grows); dragging up looks up.
        Vector3 draggedDown = EditorSceneViewCameraMotion.ApplyLook(level, 0.0, 10.0);
        Assert.Equal(EditorSceneViewCameraMotion.LookDegreesPerPixel * 10.0f, draggedDown.X, 3);

        Vector3 draggedUp = EditorSceneViewCameraMotion.ApplyLook(level, 0.0, -10.0);
        Assert.Equal(-EditorSceneViewCameraMotion.LookDegreesPerPixel * 10.0f, draggedUp.X, 3);

        Vector3 downLimit = EditorSceneViewCameraMotion.ApplyLook(level, 0.0, 100_000.0);
        Assert.Equal(EditorSceneViewCameraMotion.MaxPitchDegrees, downLimit.X, 3);
        Assert.Equal(level.Y, downLimit.Y, 3);

        Vector3 upLimit = EditorSceneViewCameraMotion.ApplyLook(level, 0.0, -100_000.0);
        Assert.Equal(-EditorSceneViewCameraMotion.MaxPitchDegrees, upLimit.X, 3);
        Assert.Equal(level.Y, upLimit.Y, 3);

        // Reaching a limit must not stick: reversing the drag moves the view back immediately.
        Vector3 reversed = EditorSceneViewCameraMotion.ApplyLook(upLimit, 0.0, 10.0);
        Assert.Equal(
            -EditorSceneViewCameraMotion.MaxPitchDegrees +
            EditorSceneViewCameraMotion.LookDegreesPerPixel * 10.0f,
            reversed.X,
            3);
    }

    [Fact]
    public void Look_PreservesRollAndRejectsNonFiniteInput()
    {
        Vector3 rolled = new(10.0f, 20.0f, 15.0f);
        Vector3 looked = EditorSceneViewCameraMotion.ApplyLook(rolled, 7.0, -3.0);
        Assert.Equal(15.0f, looked.Z, 3);

        Assert.Equal(rolled, EditorSceneViewCameraMotion.ApplyLook(rolled, double.NaN, 0.0));
    }

    [Fact]
    public void Move_TravelsAlongTheCameraBasisAtTheConfiguredSpeed()
    {
        var position = new WorldPosition(0.0, 0.0, 0.0);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.Forward,
            0.5f,
            out WorldPosition forward));
        Assert.Equal(0.0, forward.X, 6);
        Assert.Equal(0.0, forward.Y, 6);
        Assert.Equal(EditorSceneViewCameraMotion.DefaultMoveSpeed * 0.5, forward.Z, 6);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.Forward | EditorSceneViewNavigationKeys.FastMove,
            0.5f,
            out WorldPosition fast));
        Assert.Equal(
            EditorSceneViewCameraMotion.DefaultMoveSpeed *
            EditorSceneViewCameraMotion.FastMoveMultiplier * 0.5,
            fast.Z,
            6);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.Ascend,
            1.0f,
            out WorldPosition ascending));
        Assert.Equal(EditorSceneViewCameraMotion.DefaultMoveSpeed, ascending.Y, 6);
    }

    [Fact]
    public void Move_NormalizesDiagonalsAndIgnoresAcceleratorOnlyInput()
    {
        var position = new WorldPosition(0.0, 0.0, 0.0);
        EditorSceneViewNavigationKeys diagonal =
            EditorSceneViewNavigationKeys.Forward | EditorSceneViewNavigationKeys.StrafeRight;

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            diagonal,
            1.0f,
            out WorldPosition moved));
        double perAxis = EditorSceneViewCameraMotion.DefaultMoveSpeed / Math.Sqrt(2.0);
        Assert.Equal(perAxis, moved.Z, 4);
        // Strafe follows the image's right axis, which is world -X for an unrotated camera.
        Assert.Equal(-perAxis, moved.X, 4);
        double travelled = Math.Sqrt(
            moved.X * moved.X + moved.Y * moved.Y + moved.Z * moved.Z);
        Assert.Equal(EditorSceneViewCameraMotion.DefaultMoveSpeed, travelled, 4);

        Assert.False(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.FastMove,
            1.0f,
            out _));
        Assert.False(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.Forward,
            0.0f,
            out _));
    }

    [Fact]
    public void Move_StrafesAlongTheImageRightAxis()
    {
        var position = new WorldPosition(0.0, 0.0, 0.0);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.StrafeRight,
            1.0f,
            out WorldPosition right));

        // The rendered image's right axis is forward x up: world -X for an unrotated camera, so
        // strafing right must not travel along the world +X axis.
        Assert.Equal(-EditorSceneViewCameraMotion.DefaultMoveSpeed, right.X, 5);
        Assert.Equal(0.0, right.Z, 5);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            new Vector3(0.0f, 180.0f, 0.0f),
            EditorSceneViewNavigationKeys.StrafeRight,
            1.0f,
            out WorldPosition turnedAround));
        Assert.Equal(EditorSceneViewCameraMotion.DefaultMoveSpeed, turnedAround.X, 5);
    }

    [Fact]
    public void Move_KeepsPrecisionAtRebaseScaleCoordinates()
    {
        var far = new WorldPosition(1_000_000.0, 0.0, -2_000_000.0);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            far,
            Vector3.Zero,
            EditorSceneViewNavigationKeys.Forward,
            0.5f,
            out WorldPosition moved));

        Assert.Equal(1_000_000.0, moved.X, 6);
        Assert.Equal(-1_999_994.0, moved.Z, 6);
    }

    [Fact]
    public void Dolly_TravelsAlongTheViewDirection()
    {
        var position = new WorldPosition(0.0, 0.0, 0.0);

        Assert.True(EditorSceneViewCameraMotion.TryApplyDolly(
            position,
            new Vector3(0.0f, 90.0f, 0.0f),
            1.0,
            out WorldPosition right));
        Assert.Equal(EditorSceneViewCameraMotion.DollyMetersPerNotch, right.X, 5);
        Assert.Equal(0.0, right.Z, 5);

        Assert.True(EditorSceneViewCameraMotion.TryApplyDolly(
            position,
            Vector3.Zero,
            -2.0,
            out WorldPosition backwards));
        Assert.Equal(-2.0 * EditorSceneViewCameraMotion.DollyMetersPerNotch, backwards.Z, 5);

        Assert.False(EditorSceneViewCameraMotion.TryApplyDolly(
            position,
            Vector3.Zero,
            0.0,
            out _));
        Assert.False(EditorSceneViewCameraMotion.TryApplyDolly(
            position,
            Vector3.Zero,
            double.NaN,
            out _));
    }

    [Fact]
    public void Move_TiltingDownTravelsDownAlongTheViewAxis()
    {
        var position = new WorldPosition(0.0, 0.0, 0.0);
        Vector3 tiltedDown = new(45.0f, 0.0f, 0.0f);

        Assert.True(EditorSceneViewCameraMotion.TryApplyMove(
            position,
            tiltedDown,
            EditorSceneViewNavigationKeys.Forward,
            1.0f,
            out WorldPosition moved));

        double expected = EditorSceneViewCameraMotion.DefaultMoveSpeed / Math.Sqrt(2.0);
        Assert.Equal(expected, moved.Z, 4);
        Assert.Equal(-expected, moved.Y, 4);
    }
}
