using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A zero-allocation, purely blittable component representing doubly-linked list nodes 
/// among sibling entities that share the same parent.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SiblingComponent : IComponent
{
    public Entity PrevSibling;
    public Entity NextSibling;
}


