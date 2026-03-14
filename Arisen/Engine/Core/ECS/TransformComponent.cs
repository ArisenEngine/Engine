using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component to store entity position, rotation, and scale.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TransformComponent : IComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Scale;

    public static TransformComponent Identity => new()
    {
        Position = Vector3.Zero,
        Rotation = Quaternion.Identity,
        Scale = Vector3.One
    };
}
