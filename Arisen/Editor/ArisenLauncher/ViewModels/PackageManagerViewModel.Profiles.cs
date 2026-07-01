using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

        foreach (var package in EnumerateProfilePackageOptions(profile, enabledPackageIds))
        {
            profile.PackageOptions.Add(package);
        }

        SyncProfileNodesFromOptions(profile);
    }

    private IEnumerable<ProfilePackageOptionViewModel> EnumerateProfilePackageOptions(WorkspaceProfileViewModel profile, HashSet<string> enabledPackageIds)
    {
        var basePackageIds = GetBasePackageIds();
        var options = new Dictionary<string, ProfilePackageOptionViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var req in profile.Nodes)
        {
            if (string.IsNullOrWhiteSpace(req.Id) || basePackageIds.Contains(req.Id))
                continue;

            options[req.Id] = new ProfilePackageOptionViewModel
            {
                Id = req.Id,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Id : req.DisplayName,
                Version = req.Version,
                Url = req.Url,
                Type = req.Type,
                IsEnabled = enabledPackageIds.Contains(req.Id)
            };
        }

        string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
        string localDir = Path.Combine(projectDir, "Local");
        if (Directory.Exists(localDir))
        {
            foreach (var packageDir in Directory.EnumerateDirectories(localDir))
            {
                string packageJson = Path.Combine(packageDir, "package.json");
                var packageJsonManifest = ParsePackageManifest(packageJson);
                string id = string.IsNullOrWhiteSpace(packageJsonManifest?.Id)
                    ? Path.GetFileName(packageDir)
                    : packageJsonManifest!.Id;

                if (string.IsNullOrWhiteSpace(id) || basePackageIds.Contains(id))
                    continue;

                string relativePath = Path.GetRelativePath(projectDir, packageDir).Replace('\\', '/');
                options[id] = new ProfilePackageOptionViewModel
                {
                    Id = id,
                    DisplayName = string.IsNullOrWhiteSpace(packageJsonManifest?.Name) ? id : packageJsonManifest!.Name,
                    Version = packageJsonManifest?.Version ?? string.Empty,
                    Url = $"file://{relativePath}",
                    Type = packageJsonManifest?.Type ?? "managed",
                    IsEnabled = enabledPackageIds.Contains(id)
                };
            }
        }

        return options.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase);
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

    private void SyncProfileNodesFromOptions(WorkspaceProfileViewModel profile)
    {
        var basePackageIds = GetBasePackageIds();
        var previousNodes = profile.Nodes.ToList();
        var optionIds = profile.PackageOptions.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextNodes = new Dictionary<string, PackageRequirementViewModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in previousNodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || basePackageIds.Contains(node.Id) || optionIds.Contains(node.Id))
                continue;

            nextNodes[node.Id] = node;
        }

        foreach (var option in profile.PackageOptions.Where(x => x.IsEnabled))
        {
            if (string.IsNullOrWhiteSpace(option.Id) || basePackageIds.Contains(option.Id))
                continue;

            nextNodes[option.Id] = new PackageRequirementViewModel
            {
                Id = option.Id,
                Url = option.Url,
                Version = option.Version,
                DisplayName = option.DisplayName,
                Type = option.Type
            };
        }

        profile.Nodes.Clear();
        foreach (var node in nextNodes.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            profile.Nodes.Add(node);
        }
    }

    private HashSet<string> GetBasePackageIds()
    {
        return Packages
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
