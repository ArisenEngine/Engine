using System;

namespace ArisenLauncher.Models;

public class LauncherProjectMetadata
{
    public string Name { get; set; } = "New Project";
    public string Description { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public string ProjectPath { get; set; } = string.Empty; // Full path to .arisenproj
    
    // UI Metadata
    public string PreviewImageURL { get; set; } = string.Empty;
    public string IconURL { get; set; } = string.Empty;

    public string EngineVersionId { get; set; } = "default";
}
