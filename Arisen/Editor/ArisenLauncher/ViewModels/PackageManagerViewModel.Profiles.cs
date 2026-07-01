using System.Collections.ObjectModel;
using System.Linq;
using ArisenLauncher.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class PackageManagerViewModel
{
    public ObservableCollection<WorkspaceProfileViewModel> Profiles { get; } = new();

    private void InitializeProfiles()
    {
        Profiles.Clear();
        GraphProfiles.Clear();
        if (Manifest.Profiles != null)
        {
            foreach (var kvp in Manifest.Profiles)
            {
                var p = new WorkspaceProfileViewModel { Name = kvp.Key, IsEditor = kvp.Value.IsEditor };
                foreach (var req in kvp.Value.Packages)
                {
                    p.Nodes.Add(new PackageRequirementViewModel
                    {
                        Id = req.Id,
                        Url = req.Url ?? string.Empty,
                        Version = req.Version ?? string.Empty
                    });
                }
                Profiles.Add(p);
                GraphProfiles.Add(p.Name);
            }
        }
        else
        {
            // Default scaffolding
            Profiles.Add(new WorkspaceProfileViewModel { Name = "Development" });
            Profiles.Add(new WorkspaceProfileViewModel { Name = "Production" });
            GraphProfiles.Add("Development");
            GraphProfiles.Add("Production");
        }

        if (GraphProfiles.Contains(_project.SelectedProfile))
            SelectedGraphProfile = _project.SelectedProfile;
        else if (GraphProfiles.Contains("Development"))
            SelectedGraphProfile = "Development";
        else if (GraphProfiles.Count > 0)
            SelectedGraphProfile = GraphProfiles[0];
    }

    [RelayCommand]
    private void AddProfile()
    {
        int i = 1;
        string name = "CustomProfile";
        while(Profiles.Any(p => p.Name == name)) {
            name = $"CustomProfile{i++}";
        }
        Profiles.Add(new WorkspaceProfileViewModel { Name = name });
        GraphProfiles.Add(name);
        SelectedGraphProfile = name;
    }

    [RelayCommand]
    private void RemoveProfile(WorkspaceProfileViewModel vm)
    {
        Profiles.Remove(vm);
        GraphProfiles.Remove(vm.Name);
        if (SelectedGraphProfile == vm.Name)
            SelectedGraphProfile = GraphProfiles.FirstOrDefault() ?? string.Empty;
    }
}

public partial class WorkspaceProfileViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isEditor;
    public ObservableCollection<PackageRequirementViewModel> Nodes { get; } = new();

    [RelayCommand]
    private void AddEntryNode()
    {
        Nodes.Add(new PackageRequirementViewModel { Id = "com.user.game", Version = "1.0.0" });
    }

    [RelayCommand]
    private void RemoveEntryNode(PackageRequirementViewModel vm)
    {
        Nodes.Remove(vm);
    }
}
