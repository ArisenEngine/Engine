using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArisenLauncher.Models;

public partial class LauncherProjectMetadata : ObservableObject
{
    [ObservableProperty]
    private string _name = "New Project";

    [ObservableProperty]
    private Guid _projectId = Guid.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _lastModified = DateTime.UtcNow;

    [JsonIgnore]
    public string ProjectPath { get; set; } = string.Empty; // Full path to .arisenproj
    
    // UI Metadata
    [ObservableProperty]
    private string _previewImageURL = string.Empty;

    [ObservableProperty]
    private string _iconURL = string.Empty;

    [ObservableProperty]
    private string _engineVersionId = "default";

    // Boot Configuration
    public ObservableCollection<string> AvailableProfiles { get; } = new();

    [ObservableProperty]
    private string _selectedProfile = "Development";

    public ObservableCollection<string> AvailableConfigurations { get; } = new() { "Debug", "Release" };

    [ObservableProperty]
    private string _selectedConfiguration = "Debug";
}
