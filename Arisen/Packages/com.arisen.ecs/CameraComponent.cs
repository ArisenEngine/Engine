using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;
using System.Numerics;
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
    /// <summary>
    /// 1 = Perspective, 0 = Orthographic. Using byte instead of bool for blittability.
    /// </summary>
    public byte IsPerspective;

    public static CameraComponent Default => new()
    {
        VerticalFov = 60.0f,
        NearPlane = 0.1f,
        FarPlane = 1000.0f,
        IsPerspective = 1
    };
}


