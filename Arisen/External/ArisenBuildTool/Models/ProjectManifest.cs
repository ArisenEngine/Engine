using System;
using System.Collections.Generic;

namespace ArisenBuildTool.Models;

public class PackageRequirement
{
    public string Id { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Version { get; set; }
}

public class ProfileDefinition
{
    public bool IsEditor { get; set; } = false;
    public bool EnableProfiler { get; set; } = false;
    public List<PackageRequirement> Packages { get; set; } = new();
}

public class ProjectAssetReference
{
    public Guid Guid { get; set; }
    public string PackageId { get; set; } = string.Empty;
}

public class ProjectManifest
{
    public string Name { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = string.Empty;
    public ProjectAssetReference? StartupScene { get; set; }
    public ProjectAssetReference? RenderPipeline { get; set; }
    public List<PackageRequirement> Packages { get; set; } = new();
    public Dictionary<string, ProfileDefinition>? Profiles { get; set; }
}
