using System.Numerics;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Math;
using ArisenEngine.ECS.Lifecycle;
using ArisenEngine.Resources.Serialization;
using ArisenKernel.Contracts;
using ArisenKernel.Diagnostics;
using ArisenKernel.Lifecycle;

namespace PackageGame;

/// <summary>
/// Interactive runtime camera: free-fly navigation plus world-streaming ownership.
/// </summary>
/// <remarks>
/// The runtime has no gameplay controller yet, so the composition package drives the persistent
/// scene camera and publishes its position as the world streaming source. Loading a world only
/// registers its cells; a composition caller must select the finite source that makes cells stream,
/// which is why an unowned runtime showed a persistent scene with no cell content.
/// Look and movement input only exist for an interactive window, so this subsystem stays idle in
/// editor builds (the editor host owns scene-view navigation and cell selection) and in bounded
/// smoke runs (the host or the package scenario owns world activation).
/// </remarks>
public sealed class RuntimeCameraNavigationSubsystem : ITickableSubsystem
{
    private const float DefaultMoveSpeed = 12.0f;
    private const float FastMoveMultiplier = 4.0f;
    private const float LookDegreesPerPixel = 0.14f;
    private const float MaxPitchDegrees = 89.0f;
    private const float RadiansPerDegree = MathF.PI / 180.0f;

    private IInputProvider? m_Input;
    private IRuntimeWorldStreamingService? m_WorldStreaming;
    private IWorldOriginService? m_Origin;
    private Entity m_CameraEntity;
    private bool m_IsIdle;
    private bool m_HasCamera;
    private bool m_HasPublishedStreamingSource;
    private bool m_HasLoggedCameraPose;

    public int Priority => 60;

    public EnginePhase InitPhase => EnginePhase.Running;

    public void Initialize()
    {
#if ARISEN_ENGINE_EDITOR
        m_IsIdle = true;
        KernelLog.Info(
            "[RuntimeCamera] Editor build detected; the editor host owns scene-view navigation and world-cell selection.");
        return;
#else
        EngineKernel kernel = EngineKernel.Instance;
        string? smokeMode = kernel.Config?.SmokeModeName;
        if (!string.IsNullOrEmpty(smokeMode))
        {
            m_IsIdle = true;
            KernelLog.InfoFormat(
                "[RuntimeCamera] Bounded smoke run '{0}' owns world activation; interactive camera navigation is disabled.",
                smokeMode);
            return;
        }

        kernel.Services.TryGetService<IInputProvider>(out m_Input);
        kernel.Services.TryGetService<IWorldOriginService>(out m_Origin);
        m_WorldStreaming = kernel.Services.GetService<IRuntimeWorldStreamingService>();
        if (m_Input == null || m_Origin == null || m_WorldStreaming == null)
        {
            m_IsIdle = true;
            KernelLog.WarningFormat(
                "[RuntimeCamera] Camera navigation disabled. Input={0}, Origin={1}, WorldStreaming={2}",
                m_Input != null,
                m_Origin != null,
                m_WorldStreaming != null);
            return;
        }

        KernelLog.Info(
            "[RuntimeCamera] Camera navigation ready: WASD to fly, E/Q for up/down, hold right mouse button to look, Shift to move faster.");
#endif
    }

    public void Tick(float deltaTime)
    {
        if (m_IsIdle || m_Input == null || m_Origin == null || m_WorldStreaming == null || deltaTime <= 0.0f)
        {
            return;
        }

        EntityManager? entityManager = EngineKernel.Instance.GetSubsystem<SceneSubsystem>()?.ActiveEntityManager;
        if (entityManager == null ||
            !TryResolveCamera(entityManager, out Entity cameraEntity))
        {
            ReleaseInput();
            return;
        }

        ComponentPool<TransformComponent> transforms = entityManager.GetPool<TransformComponent>();
        ref TransformComponent transform = ref transforms.GetRef(cameraEntity);
        if (!IsFinite(transform.Position) || !IsFinite(transform.Rotation))
        {
            ReleaseInput();
            return;
        }

        LogResolvedCameraOnce(transform);
        ApplyLook(ref transform);
        ApplyMovement(ref transform, deltaTime);
        PublishStreamingSource(transform.Position);
    }

    public void Shutdown()
    {
        ReleaseInput();
        if (!m_IsIdle)
        {
            m_WorldStreaming?.ClearStreamingSource();
        }

        m_Input = null;
        m_WorldStreaming = null;
        m_Origin = null;
        m_HasCamera = false;
        m_HasPublishedStreamingSource = false;
    }

    public void Dispose()
    {
        Shutdown();
    }

    private void ApplyLook(ref TransformComponent transform)
    {
        IInputProvider input = m_Input!;
        bool looking = input.IsMouseButtonDown(InputMouseButton.Right);
        if (looking != input.IsCursorCaptured)
        {
            input.SetCursorCapture(looking);
        }

        if (!looking)
        {
            return;
        }

        // Mouse look is expressed in the rendered image's frame: the render camera builds a
        // right-handed view over the +Z forward basis, so the image's right is forward x up (world
        // -X for an unrotated camera) and pitch grows while the view tilts down. Dragging the mouse
        // right therefore turns right by decreasing yaw, and dragging it down looks down by
        // increasing pitch. Yaw stays wrapped so long sessions never lose degree precision.
        Vector3 eulerDegrees = transform.Rotation.QuaternionToEulerDegrees();
        float yawDegrees = MathExtensions.WrapDegrees(
            eulerDegrees.Y - input.MouseDeltaX * LookDegreesPerPixel);
        float pitchDegrees = Math.Clamp(
            eulerDegrees.X + input.MouseDeltaY * LookDegreesPerPixel,
            -MaxPitchDegrees,
            MaxPitchDegrees);
        transform.Rotation = Quaternion.CreateFromYawPitchRoll(
            yawDegrees * RadiansPerDegree,
            pitchDegrees * RadiansPerDegree,
            eulerDegrees.Z * RadiansPerDegree);
    }

    private void ApplyMovement(ref TransformComponent transform, float deltaTime)
    {
        IInputProvider input = m_Input!;
        float forwardInput = ReadAxis(input, InputKey.W, InputKey.S);
        float rightInput = ReadAxis(input, InputKey.D, InputKey.A);
        float upInput = ReadAxis(input, InputKey.E, InputKey.Q);
        if (forwardInput == 0.0f && rightInput == 0.0f && upInput == 0.0f)
        {
            return;
        }

        Vector3 direction =
            Vector3.Transform(MathExtensions.Forward, transform.Rotation) * forwardInput +
            transform.Rotation.RightVector() * rightInput +
            MathExtensions.Up * upInput;
        float lengthSquared = direction.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.000001f)
        {
            return;
        }

        float speed = DefaultMoveSpeed;
        if (input.IsKeyDown(InputKey.LeftShift) || input.IsKeyDown(InputKey.RightShift))
        {
            speed *= FastMoveMultiplier;
        }

        transform.Position += direction * (speed * deltaTime / MathF.Sqrt(lengthSquared));
    }

    private void PublishStreamingSource(Vector3 originRelativePosition)
    {
        if (!IsFinite(originRelativePosition))
        {
            return;
        }

        WorldPosition worldPosition = m_Origin!.ToWorld(originRelativePosition);
        if (!worldPosition.IsFinite)
        {
            return;
        }

        m_WorldStreaming!.SetStreamingSource(worldPosition);
        if (m_HasPublishedStreamingSource)
        {
            return;
        }

        m_HasPublishedStreamingSource = true;
        KernelLog.InfoFormat(
            "[RuntimeCamera] World streaming now follows the runtime camera at ({0:F2}, {1:F2}, {2:F2}).",
            worldPosition.X,
            worldPosition.Y,
            worldPosition.Z);
    }

    private bool TryResolveCamera(EntityManager entityManager, out Entity cameraEntity)
    {
        ComponentPool<CameraComponent> cameras = entityManager.GetPool<CameraComponent>();
        ComponentPool<TransformComponent> transforms = entityManager.GetPool<TransformComponent>();
        if (m_HasCamera &&
            entityManager.IsAlive(m_CameraEntity) &&
            cameras.Has(m_CameraEntity) &&
            transforms.Has(m_CameraEntity))
        {
            cameraEntity = m_CameraEntity;
            return true;
        }

        m_HasCamera = false;
        ReadOnlySpan<Entity> entities = cameras.GetRawEntityArray();
        for (int index = 0; index < cameras.Count; index++)
        {
            Entity candidate = entities[index];
            if (!entityManager.IsAlive(candidate) || !transforms.Has(candidate))
            {
                continue;
            }

            m_CameraEntity = candidate;
            m_HasCamera = true;
            cameraEntity = candidate;
            return true;
        }

        cameraEntity = default;
        return false;
    }

    private void ReleaseInput()
    {
        if (m_Input is { IsCursorCaptured: true } input)
        {
            input.SetCursorCapture(false);
        }
    }

    private void LogResolvedCameraOnce(in TransformComponent transform)
    {
        if (m_HasLoggedCameraPose)
        {
            return;
        }

        m_HasLoggedCameraPose = true;
        Vector3 eulerDegrees = transform.Rotation.QuaternionToEulerDegrees();
        KernelLog.InfoFormat(
            "[RuntimeCamera] Persistent camera resolved at ({0:F2}, {1:F2}, {2:F2}) facing yaw {3:F1}°, pitch {4:F1}°.",
            transform.Position.X,
            transform.Position.Y,
            transform.Position.Z,
            eulerDegrees.Y,
            eulerDegrees.X);
    }

    private static float ReadAxis(IInputProvider input, InputKey positive, InputKey negative)
    {
        float value = 0.0f;
        if (input.IsKeyDown(positive))
        {
            value += 1.0f;
        }

        if (input.IsKeyDown(negative))
        {
            value -= 1.0f;
        }

        return value;
    }

    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return float.IsFinite(value.X) &&
               float.IsFinite(value.Y) &&
               float.IsFinite(value.Z) &&
               float.IsFinite(value.W);
    }
}
