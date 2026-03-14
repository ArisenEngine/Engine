using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Defines camera properties for rendering.
/// In a DOD system, this is just data; the rendering logic resides in RenderSubsystem or specialized CameraSystems.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CameraComponent : IComponent
{
    public float VerticalFov;
    public float NearPlane;
    public float FarPlane;
    public bool IsPerspective;

    public static CameraComponent Default => new()
    {
        VerticalFov = 60.0f,
        NearPlane = 0.1f,
        FarPlane = 1000.0f,
        IsPerspective = true
    };
}
