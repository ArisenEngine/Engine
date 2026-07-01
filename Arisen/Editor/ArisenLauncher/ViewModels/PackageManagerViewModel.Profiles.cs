using System;
using System.Collections.Generic;
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
                var selectedPackageIds = kvp.Value.Packages.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var req in kvp.Value.Packages)
                {
                    p.Nodes.Add(new PackageRequirementViewModel
                    {
                        Id = req.Id,
                        Url = req.Url ?? string.Empty,
                        Version = req.Version ?? string.Empty
                    });
                }
                PopulateProfilePackageOptions(p, selectedPackageIds);
                Profiles.Add(p);
                GraphProfiles.Add(p.Name);
            }
        }
        else
        {
            // Default scaffolding
            var development = new WorkspaceProfileViewModel { Name = "Development" };
            var production = new WorkspaceProfileViewModel { Name = "Production" };
            PopulateProfilePackageOptions(development, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            PopulateProfilePackageOptions(production, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            Profiles.Add(development);
            Profiles.Add(production);
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
        var profile = new WorkspaceProfileViewModel { Name = name };
        PopulateProfilePackageOptions(profile, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        Profiles.Add(profile);
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

    [RelayCommand]
    private void SyncProfilePackages(WorkspaceProfileViewModel profile)
    {
        SyncProfileNodesFromOptions(profile);
    }

    private void PopulateProfilePackageOptions(WorkspaceProfileViewModel profile, HashSet<string> enabledPackageIds)
    {
        profile.PackageOptions.Clear();
        foreach (var package in Packages.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            profile.PackageOptions.Add(new ProfilePackageOptionViewModel
            {
                Id = package.Id,
                DisplayName = string.IsNullOrWhiteSpace(package.DisplayName) ? package.Id : package.DisplayName,
                Version = package.Version,
                Url = package.Url,
                Type = package.Type,
                IsEnabled = enabledPackageIds.Contains(package.Id)
            });
        }

        SyncProfileNodesFromOptions(profile);
    }

    private void RefreshProfilePackageOptions()
    {
        foreach (var profile in Profiles)
        {
            var enabledPackageIds = profile.PackageOptions
                .Where(x => x.IsEnabled)
                .Select(x => x.Id)
                .Concat(profile.Nodes.Select(x => x.Id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            PopulateProfilePackageOptions(profile, enabledPackageIds);
        }
    }

    private static void SyncProfileNodesFromOptions(WorkspaceProfileViewModel profile)
    {
        profile.Nodes.Clear();
        foreach (var option in profile.PackageOptions.Where(x => x.IsEnabled).OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            profile.Nodes.Add(new PackageRequirementViewModel
            {
                Id = option.Id,
                Url = option.Url,
                Version = option.Version
            });
        }
    }
}

public partial class WorkspaceProfileViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isEditor;
    public ObservableCollection<PackageRequirementViewModel> Nodes { get; } = new();
    public ObservableCollection<ProfilePackageOptionViewModel> PackageOptions { get; } = new();

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

public partial class ProfilePackageOptionViewModel : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
}
