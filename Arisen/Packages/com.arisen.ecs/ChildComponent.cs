using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component indicating an entity has children.
/// Acts as the head of an intrusive linked list of sibling entities.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ChildComponent : IComponent
{
    public Entity FirstChild;
    public int ChildCount;
}


