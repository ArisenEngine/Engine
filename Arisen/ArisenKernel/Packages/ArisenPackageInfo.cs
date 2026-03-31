using System.Reflection;

namespace ArisenKernel.Packages;

public class ArisenPackageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public PackageSource Source { get; set; } = PackageSource.Official;
    public string EngineVersion { get; set; } = string.Empty;
    public string Type { get; set; } = "managed";
    public Dictionary<string, string> Dependencies { get; set; } = new();
    public Assembly? Assembly { get; set; }
    public object? EntryInstance { get; set; }
}

