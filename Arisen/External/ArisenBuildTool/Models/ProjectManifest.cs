using System.Collections.Generic;

namespace ArisenBuildTool.Models;

public class PackageRequirement
{
    public string Id { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Version { get; set; }
}

public class ProjectManifest
{
    public string Name { get; set; } = "New Arisen Project";
    public string EngineVersion { get; set; } = string.Empty;
    public List<PackageRequirement> Packages { get; set; } = new();
}
