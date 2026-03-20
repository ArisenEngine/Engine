using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Linq;
using ArisenLauncher.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class PackageManagerViewModel : ObservableObject
{
    private readonly LauncherProjectMetadata _project;
    private readonly string _manifestPath;

    [ObservableProperty]
    private ProjectManifest _manifest = new();

    public ObservableCollection<PackageRequirementViewModel> Packages { get; } = new();

    public PackageManagerViewModel(LauncherProjectMetadata project)
    {
        _project = project;
        _manifestPath = Path.Combine(Path.GetDirectoryName(project.ProjectPath)!, "manifest.json");
        LoadManifest();
    }

    private void LoadManifest()
    {
        if (File.Exists(_manifestPath))
        {
            Manifest = JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(_manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ProjectManifest();
        }

        Packages.Clear();
        if (Manifest.Packages != null)
        {
            foreach (var req in Manifest.Packages) 
            {
                Packages.Add(new PackageRequirementViewModel { Id = req.Id, Url = req.Url ?? string.Empty, Version = req.Version ?? string.Empty });
            }
        }
    }

    [RelayCommand]
    public void SaveManifest()
    {
        Manifest.Packages = Packages.Select(x => new PackageRequirement { Id = x.Id, Url = x.Url, Version = string.IsNullOrEmpty(x.Version) ? null : x.Version }).ToList();

        string json = JsonSerializer.Serialize(Manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }
    
    [RelayCommand]
    private void AddPackage() => Packages.Add(new PackageRequirementViewModel { Id = "com.new.package", Url = "https://github.com/...", Version = "1.0.0" });
    
    [RelayCommand]
    private void RemovePackage(PackageRequirementViewModel vm) => Packages.Remove(vm);
}

public partial class PackageRequirementViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
}
