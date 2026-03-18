using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component indicating an entity has a parent in the hierarchy.
/// Stores the ID of the parent Entity.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ParentComponent : IComponent
{
    public Entity Parent;
}
