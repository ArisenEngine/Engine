using System.Collections.Generic;

namespace ArisenLauncher.Models;

public class PackageRequirement
{
    public string Id { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Version { get; set; }
}

public class ProfileDefinition
{
    public bool IsEditor { get; set; } = false;
    public List<PackageRequirement> Packages { get; set; } = new();
}

public class ProjectManifest
{
    public string Name { get; set; } = "New Arisen Project";
    public string EngineVersion { get; set; } = "Current";
    public List<PackageRequirement>? Packages { get; set; }
    public Dictionary<string, ProfileDefinition>? Profiles { get; set; }
}

public class PackageManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? Type { get; set; }
    public Dictionary<string, string>? Dependencies { get; set; }
    public PackageServices? Services { get; set; }
}

public class PackageServices
{
    public List<string>? Provides { get; set; }
    public List<string>? Requires { get; set; }
}
