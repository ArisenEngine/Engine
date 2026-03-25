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
        if (Manifest.Profiles != null)
        {
            foreach (var kvp in Manifest.Profiles)
            {
                var p = new WorkspaceProfileViewModel { Name = kvp.Key };
                foreach (var req in kvp.Value)
                {
                    p.Nodes.Add(new PackageRequirementViewModel
                    {
                        Id = req.Id,
                        Url = req.Url ?? string.Empty,
                        Version = req.Version ?? string.Empty
                    });
                }
                Profiles.Add(p);
            }
        }
        else
        {
            // Default scaffolding
            Profiles.Add(new WorkspaceProfileViewModel { Name = "Development" });
            Profiles.Add(new WorkspaceProfileViewModel { Name = "Production" });
        }
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
    }

    [RelayCommand]
    private void RemoveProfile(WorkspaceProfileViewModel vm)
    {
        Profiles.Remove(vm);
    }
}

public partial class WorkspaceProfileViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
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
