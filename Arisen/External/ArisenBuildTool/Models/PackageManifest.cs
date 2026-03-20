using System.Collections.Generic;

namespace ArisenBuildTool.Models;

public class PackageManifest
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Type { get; set; } = "managed";
    public Dictionary<string, string>? Dependencies { get; set; }
}
