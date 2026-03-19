using System;

namespace ArisenKernel.Packages;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ArisenPackageAttribute : Attribute
{
    public string PackageId { get; }

    public ArisenPackageAttribute(string packageId)
    {
        PackageId = packageId;
    }
}

