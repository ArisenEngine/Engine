using System;

namespace ArisenKernel.Contracts;

/// <summary>
/// A marker attribute used by the ArisenLauncher and the Package Manager UI
/// to automatically discover and list Engine Capabilities dynamically.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, Inherited = false)]
public sealed class ServiceContractAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    public ServiceContractAttribute(string name, string description = "")
    {
        Name = name;
        Description = description;
    }
}
