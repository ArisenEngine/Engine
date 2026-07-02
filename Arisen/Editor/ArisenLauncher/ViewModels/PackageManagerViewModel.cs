using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Nodes;
using ArisenLauncher.Models;
using ArisenLauncher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class PackageManagerViewModel : ObservableObject
{
    private readonly LauncherProjectMetadata _project;
    private readonly EngineInstance? _engine;
    private readonly ProjectService? _projectService;
    private readonly string _manifestPath;

    [ObservableProperty]
    private ProjectManifest _manifest = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _verificationError = string.Empty;

    [ObservableProperty]
    private string _saveFeedback = string.Empty;

    partial void OnVerificationErrorChanged(string value) => OnPropertyChanged(nameof(HasVerificationError));
    public bool HasVerificationError => !string.IsNullOrEmpty(VerificationError);
    public bool HasSaveFeedback => !string.IsNullOrEmpty(SaveFeedback);

    [ObservableProperty] private bool _isGraphLoading;
    [ObservableProperty] private bool _isGeneratingProjects;
    [ObservableProperty] private bool _isValidatingProfile;
    [ObservableProperty] private bool _isLaunchingProject;
    [ObservableProperty] private string _selectedGraphProfile = "Development";
    [ObservableProperty] private string _selectedLaunchConfiguration = "Debug";
    [ObservableProperty] private string _graphStatus = "Package graph has not been refreshed yet.";
    [ObservableProperty] private string _graphError = string.Empty;
    [ObservableProperty] private string _generateStatus = string.Empty;
    [ObservableProperty] private string _generateError = string.Empty;
    [ObservableProperty] private string _validationStatus = string.Empty;
    [ObservableProperty] private string _validationError = string.Empty;
    [ObservableProperty] private string _launchStatus = string.Empty;
    [ObservableProperty] private string _launchError = string.Empty;

    partial void OnGraphErrorChanged(string value) => OnPropertyChanged(nameof(HasGraphError));
    partial void OnGenerateErrorChanged(string value) => OnPropertyChanged(nameof(HasGenerateError));
    partial void OnGenerateStatusChanged(string value) => OnPropertyChanged(nameof(HasGenerateStatus));
    partial void OnValidationErrorChanged(string value) => OnPropertyChanged(nameof(HasValidationError));
    partial void OnValidationStatusChanged(string value) => OnPropertyChanged(nameof(HasValidationStatus));
    partial void OnLaunchErrorChanged(string value) => OnPropertyChanged(nameof(HasLaunchError));
    partial void OnLaunchStatusChanged(string value) => OnPropertyChanged(nameof(HasLaunchStatus));
    public bool HasGraphError => !string.IsNullOrEmpty(GraphError);
    public bool HasGenerateError => !string.IsNullOrEmpty(GenerateError);
    public bool HasGenerateStatus => !string.IsNullOrEmpty(GenerateStatus);
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);
    public bool HasValidationStatus => !string.IsNullOrEmpty(ValidationStatus);
    public bool HasLaunchError => !string.IsNullOrEmpty(LaunchError);
    public bool HasLaunchStatus => !string.IsNullOrEmpty(LaunchStatus);
    public bool HasGraphResult => PackageGraphPackages.Count > 0;

    public ObservableCollection<string> GraphProfiles { get; } = new();
    public ObservableCollection<string> LaunchConfigurations => _project.AvailableConfigurations;
    public ObservableCollection<PackageGraphPackageViewModel> PackageGraphPackages { get; } = new();
    public ObservableCollection<PackageGraphEdgeViewModel> PackageGraphEdges { get; } = new();

    [ObservableProperty] private int _sortMode = 1; // 1 = ID, 0 = Name

    partial void OnSortModeChanged(int value) => UpdateFilteredPackages();
    

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    public Action? RequestClose;


    // Create Package State
    [ObservableProperty] private bool _isCreatingPackage;
    [ObservableProperty] private PackageTemplateViewModel? _selectedPackageTemplate;
    [ObservableProperty] private string _newPackageId = "com.mycompany.mypackage";
    [ObservableProperty] private string _newPackageName = "My Package";
    [ObservableProperty] private string _newPackageVersion = "1.0.0";
    [ObservableProperty] private string _newPackageType = "managed";
    [ObservableProperty] private string _newPackageLayer = "user";
    [ObservableProperty] private bool _generatePackageEntry = true;
    [ObservableProperty] private string _newPackageAuthor = string.Empty;
    [ObservableProperty] private string _newPackageDependencies = string.Empty;
    [ObservableProperty] private string _newPackageAssemblyEntry = string.Empty;
    [ObservableProperty] private string _newPackageEntryClass = string.Empty;
    [ObservableProperty] private string _createPackageError = string.Empty;
    
    public ObservableCollection<ServiceSelectionViewModel> NewPackageServices { get; } = new();
    
    public System.Collections.Generic.List<string> AvailablePackageTypes { get; } = new() { "managed" };
    public ObservableCollection<PackageTemplateViewModel> PackageTemplates { get; } = new()
    {
        new PackageTemplateViewModel
        {
            Id = "managed-library",
            Name = "Managed Library",
            Description = "Plain managed package with explicit dependencies and no runtime entry.",
            PackageId = "com.mycompany.library",
            PackageName = "Managed Library",
            Type = "managed",
            Layer = "user",
            Dependencies = "com.arisen.core",
            GenerateEntry = false,
            AddToBaseManifest = true
        },
        new PackageTemplateViewModel
        {
            Id = "runtime-entry",
            Name = "Runtime Entry",
            Description = "Managed package with an IPackageEntry hook for project/game initialization.",
            PackageId = "com.mycompany.runtime",
            PackageName = "Runtime Package",
            Type = "managed",
            Layer = "user",
            Dependencies = "com.arisen.core",
            GenerateEntry = true,
            AddToBaseManifest = true
        },
        new PackageTemplateViewModel
        {
            Id = "service-provider",
            Name = "Service Provider",
            Description = "Managed package prepared for registering coarse-grained engine services.",
            PackageId = "com.mycompany.service",
            PackageName = "Service Provider",
            Type = "managed",
            Layer = "user",
            Dependencies = "com.arisen.core",
            GenerateEntry = true,
            AddToBaseManifest = true
        },
        new PackageTemplateViewModel
        {
            Id = "editor-tool",
            Name = "Editor Tool",
            Description = "Profile-specific editor/tooling package with editor-facing dependencies.",
            PackageId = "com.mycompany.editor-tool",
            PackageName = "Editor Tool",
            Type = "managed",
            Layer = "tooling",
            Dependencies = "com.arisen.core, com.arisen.editor",
            GenerateEntry = true,
            AddToBaseManifest = false,
            TargetProfile = "Development"
        }
    };

    partial void OnSelectedPackageTemplateChanged(PackageTemplateViewModel? value)
    {
        if (value != null)
        {
            ApplyPackageTemplate(value);
        }
    }

    partial void OnNewPackageIdChanged(string value)
    {
        RefreshGeneratedEntryMetadata();
    }

    partial void OnGeneratePackageEntryChanged(bool value)
    {
        RefreshGeneratedEntryMetadata();
    }

    partial void OnCreatePackageErrorChanged(string value) => OnPropertyChanged(nameof(HasCreatePackageError));
    public bool HasCreatePackageError => !string.IsNullOrEmpty(CreatePackageError);

    public ObservableCollection<PackageRequirementViewModel> Packages { get; } = new();

    [ObservableProperty] private PackageRequirementViewModel? _selectedPackage;

    [ObservableProperty] private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        UpdateFilteredPackages();
    }

    public ObservableCollection<PackageRequirementViewModel> FilteredPackages { get; } = new();

    private void UpdateFilteredPackages()
    {
        var query = Packages.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(p => p.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                                     p.Version.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (SortMode == 0) // Name
            query = query.OrderBy(p => p.DisplayName ?? p.Id);
        else // ID
            query = query.OrderBy(p => p.Id);

        // P8: Batch update UI collection
        var results = query.ToList();
        FilteredPackages.Clear();
        foreach (var p in results)
        {
            FilteredPackages.Add(p);
        }
        
        if (SelectedPackage != null && !Packages.Contains(SelectedPackage))
        {
            SelectedPackage = null;
        }
    }

    public Func<Task<string?>>? RequestFolderPickerAsync;

    public static PackageJsonManifest? ParsePackageManifest(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PackageJsonManifest>(json, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to parse manifest at {path}: {ex.Message}");
            return null;
        }
    }


    /// <summary>
    /// Resolves a file:// URL (which may be relative like "file://Local/pkg") to an absolute local path
    /// by combining it with the project directory.
    /// </summary>
    private string? ResolveFileUrl(string fileUrl)
    {
        if (!fileUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return null;

        // Strip the "file://" prefix to get the raw path portion
        string rawPath = fileUrl.Substring("file://".Length);
        rawPath = Uri.UnescapeDataString(rawPath);

        // If it's already an absolute path (e.g. file:///C:/foo or file://C:/foo), use it directly
        if (Path.IsPathRooted(rawPath))
        {
            // Handle file:///C:/... which gives /C:/... after stripping
            if (rawPath.Length > 2 && rawPath[0] == '/' && rawPath[2] == ':')
                rawPath = rawPath.Substring(1);
            return rawPath.TrimEnd('/', '\\');
        }

        // Relative path: resolve against project directory
        string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
        
        // Remove leading slash if it's treated as a relative path segment (e.g. file://Local/...)
        if (rawPath.StartsWith("/") || rawPath.StartsWith("\\"))
            rawPath = rawPath.Substring(1);
            
        return Path.GetFullPath(Path.Combine(projectDir, rawPath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private async Task LoadManifestAsync()
    {
        IsLoading = true;
        try 
        {
            if (File.Exists(_manifestPath))
            {
                string content = await File.ReadAllTextAsync(_manifestPath);
                Manifest = JsonSerializer.Deserialize<ProjectManifest>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ProjectManifest();
            }

            Packages.Clear();
            if (Manifest.Packages != null)
            {
                foreach (var req in Manifest.Packages) 
                {
                    if (!Packages.Any(p => p.Id == req.Id))
                    {
                        bool isMissing = string.IsNullOrWhiteSpace(req.Url);
                        PackageJsonManifest? pData = null;

                        if (!isMissing && (req.Url ?? "").StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                        {
                            string? localPath = ResolveFileUrl(req.Url!);
                            if (localPath != null)
                            {
                                string pkgJson = Path.Combine(localPath, "package.json");
                                pData = ParsePackageManifest(pkgJson);
                            }
                        }

                        var vm = CreateViewModelFromData(req, pData);
                        Packages.Add(vm);
                    }
                }
            }

            InitializeProfiles();
            RefreshServiceStatus();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private PackageRequirementViewModel CreateViewModelFromData(PackageRequirement req, PackageJsonManifest? pData)
    {
        bool isMissing = string.IsNullOrWhiteSpace(req.Url);
        string dispName = string.Empty;
        string type = "managed";
        string engVer = "Current";
        string entryDll = string.Empty;
        string entryType = string.Empty;
        string desc = string.Empty;
        string auth = string.Empty;
        string prov = string.Empty;
        string reqsStr = string.Empty;
        string nativeRuntimes = string.Empty;
        var depsList = new System.Collections.Generic.List<DependencyViewModel>();

        if (pData != null)
        {
            dispName = pData.Name;
            if (string.IsNullOrEmpty(dispName)) dispName = pData.Id;
            type = pData.Type ?? "managed";
            desc = pData.Description ?? string.Empty;
            auth = pData.Author ?? string.Empty;
            engVer = pData.EngineVersion ?? "Current";

            if (pData.Entry != null)
            {
                entryDll = pData.Entry.Assembly ?? string.Empty;
                entryType = pData.Entry.Class ?? string.Empty;
            }

            if (pData.Services != null)
            {
                if (pData.Services.Provides != null)
                {
                    var provNames = new List<string>();
                    foreach (var p in pData.Services.Provides)
                    {
                        if (p.ValueKind == JsonValueKind.String) provNames.Add(p.GetString()!);
                        else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("interface", out var iface))
                            provNames.Add(iface.GetString()!);
                    }
                    prov = string.Join(", ", provNames.Where(x => !string.IsNullOrEmpty(x)));
                }

                if (pData.Services.Requires != null)
                {
                    var reqNames = new List<string>();
                    foreach (var r in pData.Services.Requires)
                    {
                        if (r.ValueKind == JsonValueKind.String) reqNames.Add(r.GetString()!);
                        else if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("interface", out var iface))
                            reqNames.Add(iface.GetString()!);
                    }
                    reqsStr = string.Join(", ", reqNames.Where(x => !string.IsNullOrEmpty(x)));
                }
            }

            if (pData.Dependencies != null)
            {
                foreach (var dep in pData.Dependencies)
                {
                    depsList.Add(new DependencyViewModel { Id = dep.Key, Version = dep.Value ?? "*" });
                }
            }

            if (pData.NativeRuntimes != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kvp in pData.NativeRuntimes)
                {
                    sb.Append(kvp.Key).Append(": ");
                    if (kvp.Value != null)
                        sb.Append(string.Join(", ", kvp.Value));
                    sb.AppendLine();
                }
                nativeRuntimes = sb.ToString().Trim();
            }
        }

        var vm = new PackageRequirementViewModel { 
            Id = req.Id, 
            DisplayName = string.IsNullOrEmpty(dispName) ? req.Id : dispName,
            Url = req.Url ?? string.Empty, 
            Version = req.Version ?? string.Empty,
            Description = desc,
            Author = auth,
            Type = type,
            EngineVersion = engVer,
            AssemblyEntry = entryDll,
            EntryClass = entryType,
            ServicesProvides = prov,
            ServicesRequires = reqsStr,
            NativeRuntimesDisplay = nativeRuntimes,
            IsLocal = !isMissing && (req.Url ?? "").StartsWith("file://", StringComparison.OrdinalIgnoreCase),
            IsUnresolved = isMissing,
            UnresolvedMessage = isMissing ? "URL is completely missing in manifest.json" : string.Empty,
            HasLocalOverrideCandidate = IsRemoteUrl(req.Url ?? string.Empty) && TryGetLocalPackageOverride(req.Id, out _),
            ParentContext = this,
            CachedManifest = pData // P5/P6 Cache
        };
        foreach (var d in depsList) vm.Dependencies.Add(d);
        return vm;
    }

    public PackageManagerViewModel(LauncherProjectMetadata project, EngineInstance? engine = null, ProjectService? projectService = null)
    {
        _project = project;
        _engine = engine;
        _projectService = projectService;
        _manifestPath = Path.Combine(Path.GetDirectoryName(project.ProjectPath)!, "manifest.json");
        SelectedLaunchConfiguration = string.IsNullOrWhiteSpace(project.SelectedConfiguration) ? "Debug" : project.SelectedConfiguration;
        
        Packages.CollectionChanged += (s, e) => {
            UpdateFilteredPackages();
            RefreshProfilePackageOptions();
        };

        Profiles.CollectionChanged += (s, e) => { };

        InitializeServices();
        
        // P4: Trigger async load to avoid blocking UI thread
        Task.Run(LoadManifestAsync);
    }

    [RelayCommand]
    public void SaveManifest()
    {
        Manifest.Packages = Packages.Select(x => new PackageRequirement { Id = x.Id, Url = x.Url, Version = string.IsNullOrEmpty(x.Version) ? null : x.Version }).ToList();
        
        // Save Profiles
        Manifest.Profiles = new System.Collections.Generic.Dictionary<string, ProfileDefinition>();
        foreach (var profile in Profiles)
        {
            var def = new ProfileDefinition { IsEditor = profile.IsEditor };
            SyncProfileNodesFromOptions(profile);

            foreach (var package in profile.Nodes.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(package.Id))
                    continue;

                def.Packages.Add(new PackageRequirement
                {
                    Id = package.Id,
                    Url = string.IsNullOrEmpty(package.Url) ? null : package.Url,
                    Version = string.IsNullOrEmpty(package.Version) ? null : package.Version
                });
            }

            Manifest.Profiles[profile.Name] = def;
        }

        string json = JsonSerializer.Serialize(Manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    private async Task RestoreRemotePackagesForSelectedProfileAsync()
    {
        string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
        var resolver = new PackageResolver(null);
        await resolver.RestoreManifestPackagesAsync(Manifest, SelectedGraphProfile, projectDir);
    }

    [RelayCommand]
    private async Task RefreshPackageGraph()
    {
        GraphError = string.Empty;
        GraphStatus = "Refreshing package graph...";
        PackageGraphPackages.Clear();
        PackageGraphEdges.Clear();
        OnPropertyChanged(nameof(HasGraphResult));

        if (_engine == null)
        {
            GraphError = "Select an engine version in the launcher before refreshing the package graph.";
            GraphStatus = "Package graph refresh failed.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedGraphProfile))
        {
            GraphError = "Select a workspace profile before refreshing the package graph.";
            GraphStatus = "Package graph refresh failed.";
            return;
        }

        if (!File.Exists(_manifestPath))
        {
            GraphError = $"Workspace manifest not found: {_manifestPath}";
            GraphStatus = "Package graph refresh failed.";
            return;
        }

        IsGraphLoading = true;
        try
        {
            SaveManifest();
            GraphStatus = "Restoring remote packages...";
            await RestoreRemotePackagesForSelectedProfileAsync();

            string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
            string outputDir = Path.Combine(projectDir, ".arisen");
            Directory.CreateDirectory(outputDir);

            GraphStatus = "Refreshing package graph...";
            string outputPath = Path.Combine(outputDir, $"package-graph.{SanitizeFileName(SelectedGraphProfile)}.json");
            string args = $"graph --manifest \"{_manifestPath}\" --engine \"{_engine.InstallPath}\" --profile \"{SelectedGraphProfile}\" --format json --output \"{outputPath}\"";
            var result = await RunBuildToolCaptureAsync(args, _engine.InstallPath, TimeSpan.FromSeconds(60));

            if (!result.Succeeded)
            {
                GraphError = BuildToolOutputToMessage(result);
                GraphStatus = "Package graph refresh failed.";
                return;
            }

            if (!File.Exists(outputPath))
            {
                GraphError = $"ArisenBuildTool completed but did not write graph output: {outputPath}";
                GraphStatus = "Package graph refresh failed.";
                return;
            }

            string json = await File.ReadAllTextAsync(outputPath);
            var graph = JsonSerializer.Deserialize<PackageGraphDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (graph == null)
            {
                GraphError = "ArisenBuildTool wrote an empty package graph.";
                GraphStatus = "Package graph refresh failed.";
                return;
            }

            foreach (var package in graph.Packages.OrderBy(x => x.Order))
            {
                PackageGraphPackages.Add(new PackageGraphPackageViewModel
                {
                    Order = package.Order,
                    Id = package.Id,
                    Type = package.Type,
                    Path = package.Path,
                    DependenciesDisplay = package.Dependencies.Count == 0
                        ? "<none>"
                        : string.Join(", ", package.Dependencies.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key} {x.Value}"))
                });
            }

            foreach (var edge in graph.Edges.OrderBy(x => x.From, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.To, StringComparer.OrdinalIgnoreCase))
            {
                PackageGraphEdges.Add(new PackageGraphEdgeViewModel
                {
                    From = edge.From,
                    To = edge.To,
                    Version = edge.Version
                });
            }

            GraphStatus = $"Validated {PackageGraphPackages.Count} packages and {PackageGraphEdges.Count} dependency edges for {graph.Profile}.";
            OnPropertyChanged(nameof(HasGraphResult));
        }
        catch (Exception ex)
        {
            GraphError = $"Failed to refresh package graph: {ex.Message}";
            GraphStatus = "Package graph refresh failed.";
        }
        finally
        {
            IsGraphLoading = false;
        }
    }

    [RelayCommand]
    private async Task RegenerateProjectFiles()
    {
        GenerateError = string.Empty;
        GenerateStatus = "Regenerating project files...";

        if (_engine == null)
        {
            GenerateError = "Select an engine version in the launcher before regenerating project files.";
            GenerateStatus = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedGraphProfile))
        {
            GenerateError = "Select a workspace profile before regenerating project files.";
            GenerateStatus = string.Empty;
            return;
        }

        if (!File.Exists(_manifestPath))
        {
            GenerateError = $"Workspace manifest not found: {_manifestPath}";
            GenerateStatus = string.Empty;
            return;
        }

        IsGeneratingProjects = true;
        try
        {
            SaveManifest();
            GenerateStatus = "Restoring remote packages...";
            await RestoreRemotePackagesForSelectedProfileAsync();

            GenerateStatus = "Regenerating project files...";
            string args = $"generate --manifest \"{_manifestPath}\" --engine \"{_engine.InstallPath}\" --profile \"{SelectedGraphProfile}\"";
            var result = await RunBuildToolCaptureAsync(args, _engine.InstallPath, TimeSpan.FromSeconds(120));

            if (!result.Succeeded)
            {
                GenerateError = BuildToolOutputToMessage(result);
                GenerateStatus = string.Empty;
                return;
            }

            GenerateStatus = $"Project files regenerated for {SelectedGraphProfile}.";
        }
        catch (Exception ex)
        {
            GenerateError = $"Failed to regenerate project files: {ex.Message}";
            GenerateStatus = string.Empty;
        }
        finally
        {
            IsGeneratingProjects = false;
        }
    }

    [RelayCommand]
    private async Task ValidateProfile()
    {
        ValidationError = string.Empty;
        ValidationStatus = "Validating profile...";

        if (_engine == null)
        {
            ValidationError = "Select an engine version in the launcher before validating a profile.";
            ValidationStatus = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedGraphProfile))
        {
            ValidationError = "Select a workspace profile before validating.";
            ValidationStatus = string.Empty;
            return;
        }

        if (!File.Exists(_manifestPath))
        {
            ValidationError = $"Workspace manifest not found: {_manifestPath}";
            ValidationStatus = string.Empty;
            return;
        }

        IsValidatingProfile = true;
        try
        {
            SaveManifest();
            ValidationStatus = "Restoring remote packages...";
            await RestoreRemotePackagesForSelectedProfileAsync();

            ValidationStatus = "Validating profile...";
            string args = $"validate --manifest \"{_manifestPath}\" --engine \"{_engine.InstallPath}\" --profile \"{SelectedGraphProfile}\"";
            var result = await RunBuildToolCaptureAsync(args, _engine.InstallPath, TimeSpan.FromSeconds(60));

            if (!result.Succeeded)
            {
                ValidationError = BuildToolOutputToMessage(result);
                ValidationStatus = string.Empty;
                return;
            }

            ValidationStatus = $"Profile {SelectedGraphProfile} is valid.";
        }
        catch (Exception ex)
        {
            ValidationError = $"Failed to validate profile: {ex.Message}";
            ValidationStatus = string.Empty;
        }
        finally
        {
            IsValidatingProfile = false;
        }
    }

    [RelayCommand]
    private async Task LaunchSelectedProfile()
    {
        LaunchError = string.Empty;
        LaunchStatus = "Launching profile...";

        if (_projectService == null)
        {
            LaunchError = "Package Manager was opened without launch support.";
            LaunchStatus = string.Empty;
            return;
        }

        if (_engine == null)
        {
            LaunchError = "Select an engine version in the launcher before launching.";
            LaunchStatus = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedGraphProfile))
        {
            LaunchError = "Select a workspace profile before launching.";
            LaunchStatus = string.Empty;
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedLaunchConfiguration))
        {
            LaunchError = "Select a build configuration before launching.";
            LaunchStatus = string.Empty;
            return;
        }

        if (!File.Exists(_manifestPath))
        {
            LaunchError = $"Workspace manifest not found: {_manifestPath}";
            LaunchStatus = string.Empty;
            return;
        }

        IsLaunchingProject = true;
        try
        {
            SaveManifest();

            _project.SelectedProfile = SelectedGraphProfile;
            _project.SelectedConfiguration = SelectedLaunchConfiguration;

            bool success = await _projectService.LaunchProjectAsync(_project, _engine);
            if (!success)
            {
                LaunchError = "Failed to launch profile. Check the launcher log for build or boot details.";
                LaunchStatus = string.Empty;
                return;
            }

            LaunchStatus = $"Launching {SelectedGraphProfile} [{SelectedLaunchConfiguration}].";
        }
        catch (Exception ex)
        {
            LaunchError = $"Failed to launch profile: {ex.Message}";
            LaunchStatus = string.Empty;
        }
        finally
        {
            IsLaunchingProject = false;
        }
    }

    private async Task<BuildToolRunResult> RunBuildToolCaptureAsync(string commandArgs, string workingDirectory, TimeSpan timeout)
    {
        string buildToolExecutable = Path.Combine(workingDirectory, "External", "ArisenBuildTool", "bin", "Debug", "net9.0", "ArisenBuildTool.dll");
        if (!File.Exists(buildToolExecutable))
        {
            string rootDll = Path.Combine(workingDirectory, "ArisenBuildTool.dll");
            if (File.Exists(rootDll)) buildToolExecutable = rootDll;
        }

        string buildToolProject = Path.Combine(workingDirectory, "External", "ArisenBuildTool", "ArisenBuildTool.csproj");
        string toolArgs;
        if (File.Exists(buildToolExecutable))
        {
            toolArgs = $"\"{buildToolExecutable}\" {commandArgs}";
        }
        else if (File.Exists(buildToolProject))
        {
            toolArgs = $"run --project \"{buildToolProject}\" -- {commandArgs}";
        }
        else
        {
            return new BuildToolRunResult(false, -1, string.Empty, "ArisenBuildTool not found for the selected engine.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = toolArgs,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return new BuildToolRunResult(false, -1, string.Empty, "Failed to start ArisenBuildTool.");
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new System.Threading.CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new BuildToolRunResult(false, -1, await outputTask, $"ArisenBuildTool timed out after {timeout.TotalSeconds:0} seconds.");
        }

        string output = await outputTask;
        string error = await errorTask;
        return new BuildToolRunResult(process.ExitCode == 0, process.ExitCode, output, error);
    }

    private static string BuildToolOutputToMessage(BuildToolRunResult result)
    {
        string combined = string.Join(Environment.NewLine, new[] { result.StandardError, result.StandardOutput }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(combined))
            return $"ArisenBuildTool failed with exit code {result.ExitCode}.";

        return combined;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }
    
    [RelayCommand]
    private void AddPackage()
    {
        var package = new PackageRequirementViewModel
        {
            Id = string.Empty,
            Url = string.Empty,
            Version = string.Empty,
            DisplayName = "Remote Package",
            IsUnresolved = true,
            UnresolvedMessage = "Enter a direct package .zip URL, or a registry.json URL to inspect available package versions.",
            ParentContext = this
        };

        Packages.Add(package);
        SelectedPackage = package;
    }
    
    [RelayCommand]
    private void BeginCreatePackage()
    {
        CreatePackageError = string.Empty;
        IsCreatingPackage = true;

        NewPackageServices.Clear();
        foreach (var cap in Capabilities) 
        {
            NewPackageServices.Add(new ServiceSelectionViewModel { ContractName = cap.ContractName, FriendlyName = cap.FriendlyName });
        }

        SelectedPackageTemplate = PackageTemplates.FirstOrDefault();
        if (SelectedPackageTemplate != null)
        {
            ApplyPackageTemplate(SelectedPackageTemplate);
        }
    }

    [RelayCommand]
    private void CancelCreatePackage()
    {
        IsCreatingPackage = false;
    }

    [RelayCommand]
    private void ConfirmCreatePackage()
    {
        CreatePackageError = string.Empty;
        var template = SelectedPackageTemplate ?? PackageTemplates.First();

        // Validation
        if (string.IsNullOrWhiteSpace(NewPackageId))
        {
            CreatePackageError = "Package ID cannot be empty.";
            return;
        }

        if (PackageIdExists(NewPackageId))
        {
            CreatePackageError = $"A package with ID '{NewPackageId}' is already installed.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPackageName) || string.IsNullOrWhiteSpace(NewPackageVersion))
        {
            CreatePackageError = "Package Name and Version are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPackageType))
        {
            CreatePackageError = "Package Type is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPackageLayer))
        {
            CreatePackageError = "Package Layer is required.";
            return;
        }

        if (GeneratePackageEntry && (string.IsNullOrWhiteSpace(NewPackageAssemblyEntry) || string.IsNullOrWhiteSpace(NewPackageEntryClass)))
        {
            CreatePackageError = "Entry Assembly and Entry Class are required when generating a package entry.";
            return;
        }

        string localDir = Path.Combine(Path.GetDirectoryName(_project.ProjectPath)!, "Local");
        Directory.CreateDirectory(localDir); // Ensure Local dir exists

        string packageDir = Path.Combine(localDir, NewPackageId);
        if (Directory.Exists(packageDir))
        {
            CreatePackageError = $"Directory '{NewPackageId}' already exists in the Local workspace. Please manually import it or use a different ID.";
            return;
        }

        try
        {
            // Scaffold Directory
            Directory.CreateDirectory(packageDir);

            // Parse Dependencies
            var depsDict = new System.Collections.Generic.Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(NewPackageDependencies))
            {
                var depsList = NewPackageDependencies.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var dep in depsList)
                {
                    string cleanDep = dep.Trim();
                    if (!string.IsNullOrEmpty(cleanDep))
                    {
                        depsDict[cleanDep] = "1.0.0"; // Default to 1.0.0 for missing deps placeholder
                    }
                }
            }

            // Create package.json adhering to Engine standard
            var providesArr = NewPackageServices.Where(s => s.IsProvided).Select(s => s.ContractName).ToArray();
            var requiresArr = NewPackageServices.Where(s => s.IsRequired).Select(s => s.ContractName).ToArray();

            object? servicesObj = null;
            if (providesArr.Length > 0 || requiresArr.Length > 0)
            {
                servicesObj = new
                {
                    provides = providesArr,
                    requires = requiresArr
                };
            }

            string jsonPath = Path.Combine(packageDir, "package.json");
            var packageJson = new JsonObject
            {
                ["$schema"] = "https://arisen.dev/schemas/package-v2.json",
                ["id"] = NewPackageId,
                ["name"] = NewPackageName,
                ["version"] = NewPackageVersion,
                ["layer"] = NewPackageLayer,
                ["type"] = NewPackageType,
                ["dependencies"] = JsonSerializer.SerializeToNode(depsDict) as JsonObject ?? new JsonObject()
            };

            if (!string.IsNullOrWhiteSpace(NewPackageAuthor))
            {
                packageJson["author"] = NewPackageAuthor;
            }

            if (GeneratePackageEntry)
            {
                packageJson["entry"] = new JsonObject
                {
                    ["assembly"] = NewPackageAssemblyEntry,
                    ["class"] = NewPackageEntryClass
                };
            }

            if (servicesObj != null)
            {
                packageJson["services"] = JsonSerializer.SerializeToNode(servicesObj);
            }

            File.WriteAllText(jsonPath, packageJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            // Generate IPackageEntry C# Hook if requested and managed
            if (NewPackageType == "managed" && GeneratePackageEntry)
            {
                string entryNamespace = GetNamespaceFromQualifiedClass(NewPackageEntryClass);
                string entryClassName = GetClassNameFromQualifiedClass(NewPackageEntryClass);
                string csCode = CreatePackageEntrySource(template, entryNamespace, entryClassName, providesArr);
                File.WriteAllText(Path.Combine(packageDir, "PackageEntry.cs"), csCode);
            }
            else if (NewPackageType == "managed")
            {
                string markerNamespace = ToGeneratedNamespace(NewPackageId);
                File.WriteAllText(Path.Combine(packageDir, "PackageMarker.cs"), $$"""
namespace {{markerNamespace}};

public sealed class PackageMarker
{
}
""");
            }

            if (template.AddToBaseManifest)
            {
                AddLocalPackageRecursive(packageDir);
            }
            else
            {
                AddProfilePackageRequirement(template.TargetProfile, packageDir, NewPackageId, NewPackageVersion);
            }

            // Close creation panel
            IsCreatingPackage = false;
        }
        catch (Exception ex)
        {
            CreatePackageError = $"Failed to perform package scaffolding: {ex.Message}";
        }
    }

    private void ApplyPackageTemplate(PackageTemplateViewModel template)
    {
        NewPackageId = template.PackageId;
        NewPackageName = template.PackageName;
        NewPackageVersion = "1.0.0";
        NewPackageType = template.Type;
        NewPackageLayer = template.Layer;
        NewPackageDependencies = template.Dependencies;
        GeneratePackageEntry = template.GenerateEntry;
        NewPackageAuthor = string.Empty;
        RefreshGeneratedEntryMetadata();
    }

    private bool PackageIdExists(string packageId)
    {
        return Packages.Any(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase))
            || Profiles.SelectMany(p => p.Nodes).Any(p => string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshGeneratedEntryMetadata()
    {
        if (!GeneratePackageEntry)
        {
            NewPackageAssemblyEntry = string.Empty;
            NewPackageEntryClass = string.Empty;
            return;
        }

        NewPackageAssemblyEntry = $"{ToGeneratedProjectName(NewPackageId)}.dll";
        NewPackageEntryClass = $"{ToGeneratedNamespace(NewPackageId)}.PackageEntry";
    }

    private static string CreatePackageEntrySource(PackageTemplateViewModel template, string entryNamespace, string entryClassName, string[] providesArr)
    {
        string loadBody = template.Id == "service-provider" && providesArr.Length > 0
            ? "        // Register the selected service implementations here."
            : "        // Initialize package services here.";

        if (template.Id == "editor-tool")
        {
            loadBody = "        // Register editor tools or editor-facing services here.";
        }

        return $$"""
using ArisenKernel.Diagnostics;
using ArisenKernel.Packages;
using ArisenKernel.Services;

namespace {{entryNamespace}};

public sealed class {{entryClassName}} : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        KernelLog.Info("[{{template.Name}}] Package loaded.");
{{loadBody}}
    }

    public void OnUnload(IServiceRegistry services)
    {
        KernelLog.Info("[{{template.Name}}] Package unloaded.");
    }
}
""";
    }

    private void AddProfilePackageRequirement(string profileName, string packageDir, string packageId, string version)
    {
        string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
        string relativePath = Path.GetRelativePath(projectDir, packageDir).Replace('\\', '/');
        string packageUrl = $"file://{relativePath}";

        var profile = Profiles.FirstOrDefault(x => string.Equals(x.Name, profileName, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            profile = new WorkspaceProfileViewModel { Name = profileName, IsEditor = true };
            Profiles.Add(profile);
            if (!GraphProfiles.Contains(profileName))
            {
                GraphProfiles.Add(profileName);
            }
        }

        var existing = profile.Nodes.FirstOrDefault(x => string.Equals(x.Id, packageId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            profile.Nodes.Add(new PackageRequirementViewModel
            {
                Id = packageId,
                Url = packageUrl,
                Version = version,
                DisplayName = NewPackageName,
                Type = NewPackageType,
                IsLocal = true,
                ParentContext = this,
                CachedManifest = ParsePackageManifest(Path.Combine(packageDir, "package.json"))
            });
        }
        else
        {
            existing.Url = packageUrl;
            existing.Version = version;
        }

        RefreshProfilePackageOptions();
    }

    private static string GetNamespaceFromQualifiedClass(string qualifiedClass)
    {
        int lastDot = qualifiedClass.LastIndexOf('.');
        return lastDot <= 0 ? "ArisenEngine.Generated" : qualifiedClass[..lastDot];
    }

    private static string GetClassNameFromQualifiedClass(string qualifiedClass)
    {
        int lastDot = qualifiedClass.LastIndexOf('.');
        return lastDot < 0 || lastDot == qualifiedClass.Length - 1 ? "PackageEntry" : qualifiedClass[(lastDot + 1)..];
    }

    private static string ToGeneratedProjectName(string packageId)
    {
        return string.Join(".", packageId.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(ToPascalCase));
    }

    private static string ToGeneratedNamespace(string packageId)
    {
        return $"ArisenEngine.Generated.{string.Join(".", packageId.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(ToPascalCase))}";
    }

    private static string ToPascalCase(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        if (chars.Length == 0) return "Package";

        string cleaned = new string(chars);
        return char.ToUpperInvariant(cleaned[0]) + (cleaned.Length == 1 ? string.Empty : cleaned[1..]);
    }
    
    [RelayCommand]
    private async Task AddLocalPackage()
    {
        if (RequestFolderPickerAsync != null)
        {
            var folder = await RequestFolderPickerAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                string packageJsonPath = Path.Combine(folder, "package.json");
                if (!File.Exists(packageJsonPath))
                {
                    string id = "com.user." + Path.GetFileName(folder).ToLower().Replace(" ", "");
                    var defaultPkg = new PackageManifest
                    {
                        Id = id,
                        Name = Path.GetFileName(folder),
                        Version = "1.0.0",
                        Type = "managed",
                        Dependencies = new System.Collections.Generic.Dictionary<string, string>()
                    };
                    File.WriteAllText(packageJsonPath, JsonSerializer.Serialize(defaultPkg, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
                    // Provide a generic valid C# namespace compliant class
                    string safeName = Path.GetFileName(folder).Replace(".", "").Replace(" ", "");
                    File.WriteAllText(Path.Combine(folder, "Class1.cs"), $"namespace ArisenEngine.{safeName} {{\n    public class Class1 {{ }}\n}}\n");
                }

                AddLocalPackageRecursive(folder);
            }
        }
    }

    private bool TryVerifyPackageSchema(string folder, out string id, out string version, out string description, out string author, out string assemblyEntry, out string provides, out string requires, out System.Collections.Generic.Dictionary<string, string> deps)
    {
        id = string.Empty;
        version = string.Empty;
        description = string.Empty;
        author = string.Empty;
        assemblyEntry = string.Empty;
        provides = string.Empty;
        requires = string.Empty;
        deps = new System.Collections.Generic.Dictionary<string, string>();
        VerificationError = string.Empty;

        string packageJsonPath = Path.Combine(folder, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            VerificationError = $"Validation Failed: Folder '{Path.GetFileName(folder)}' is missing a package.json file.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            
            if (doc.RootElement.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                description = descProp.GetString() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("author", out var authProp) && authProp.ValueKind == JsonValueKind.String)
                author = authProp.GetString() ?? string.Empty;
            
            if (doc.RootElement.TryGetProperty("entry", out var entryObj) && entryObj.ValueKind == JsonValueKind.Object)
            {
                if (entryObj.TryGetProperty("assembly", out var assemblyProp) && assemblyProp.ValueKind == JsonValueKind.String)
                    assemblyEntry = assemblyProp.GetString() ?? string.Empty;
            }
            if (doc.RootElement.TryGetProperty("services", out var srvProp))
            {
                if (srvProp.TryGetProperty("provides", out var pProp) && pProp.ValueKind == JsonValueKind.Array)
                    provides = string.Join(", ", pProp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)));
                if (srvProp.TryGetProperty("requires", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
                    requires = string.Join(", ", rProp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)));
            }

            // 1. Identity Verification
            if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                id = idProp.GetString() ?? string.Empty;
            else if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                id = nameProp.GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(id))
            {
                VerificationError = $"Schema Error: Package at '{Path.GetFileName(folder)}' has a null or empty 'id' field.";
                return false;
            }

            // 2. Version Verification
            if (doc.RootElement.TryGetProperty("version", out var versionProp) && versionProp.ValueKind == JsonValueKind.String)
                version = versionProp.GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(version))
            {
                VerificationError = $"Schema Error: Package '{id}' is missing a valid 'version' string.";
                return false;
            }

            // 3. Type Constraint
            if (doc.RootElement.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                var typeStr = typeProp.GetString() ?? string.Empty;
                var validTypes = new[] { "managed", "native", "asset", "module" };
                if (!validTypes.Contains(typeStr))
                {
                    VerificationError = $"Schema Error: '{id}' defines an invalid type '{typeStr}'. Must be managed, native, asset, or module.";
                    return false;
                }
            }

            // 4. Dependencies Graph Verification
            if (doc.RootElement.TryGetProperty("dependencies", out var depsProp))
            {
                if (depsProp.ValueKind != JsonValueKind.Object)
                {
                    VerificationError = $"Schema Error: '{id}' 'dependencies' field is structured incorrectly. Must be a JSON Object.";
                    return false;
                }

                foreach (var dep in depsProp.EnumerateObject())
                {
                    if (dep.Value.ValueKind != JsonValueKind.String)
                    {
                        VerificationError = $"Schema Error: '{id}' dependency '{dep.Name}' specifies an invalid formulation (needs string version).";
                        return false;
                    }
                    if (dep.Name == id)
                    {
                        VerificationError = $"Cyclic Error: '{id}' erroneously imports itself as a dependency.";
                        return false;
                    }

                    deps[dep.Name] = dep.Value.GetString()!;
                }
            }

            // removed strict resolution check to permit Unresolved UI functionality

            return true;
        }
        catch (JsonException ex)
        {
            VerificationError = $"Malformed JSON: '{Path.GetFileName(folder)}/package.json' contains syntax errors. {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            VerificationError = $"Fatal Verification Error: {ex.Message}";
            return false;
        }
    }

    private void AddLocalPackageRecursive(string folder)
    {
        string cleanFolder = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        
        if (!TryVerifyPackageSchema(cleanFolder, out string id, out string version, out string description, out string author, out string assemblyEntry, out string provides, out string requires, out var deps))
        {
            return; // Abort silently - the ViewModel has already surfaced the error to VerificationError binding
        }

        if (Packages.Any(p => p.Id == id))
        {
            return; // Already safely inside Manifest cache
        }

        // Generate perfect engine URL
        string fileUri = new Uri(cleanFolder).AbsoluteUri;
        if (!fileUri.EndsWith("/")) fileUri += "/";

        Packages.Add(new PackageRequirementViewModel
        {
            Id = id,
            Url = fileUri,
            Version = version,
            Description = description,
            Author = author,
            AssemblyEntry = assemblyEntry,
            ServicesProvides = provides,
            ServicesRequires = requires,
            IsLocal = true,
            ParentContext = this
        });

        // Trigger deep resolution recursively since verification guaranteed local sibiling paths exist safely
        string? parentDir = Path.GetDirectoryName(cleanFolder);
        if (!string.IsNullOrEmpty(parentDir))
        {
            foreach (var dep in deps)
            {
                if (Packages.Any(p => p.Id == dep.Key)) continue;

                string siblingFolder = Path.Combine(parentDir, dep.Key);
                if (Directory.Exists(siblingFolder) && File.Exists(Path.Combine(siblingFolder, "package.json")))
                {
                    AddLocalPackageRecursive(siblingFolder);
                }
                else
                {
                    Packages.Add(new PackageRequirementViewModel
                    {
                        Id = dep.Key,
                        Url = string.Empty,
                        Version = dep.Value,
                        IsUnresolved = true,
                        UnresolvedMessage = "Missing Dependency. Please assign a Url or Browse Local Folder."
                    });
                }
            }
        }
    }

    [RelayCommand]
    private void RemovePackage(PackageRequirementViewModel vm)
    {
        VerificationError = string.Empty;
        
        // 1. Strict Blocking: Check if any other package depends on this one
        foreach (var p in Packages)
        {
            if (p == vm) continue;

            var pData = p.CachedManifest;
            if (pData?.Dependencies != null)
            {
                if (pData.Dependencies.TryGetValue(vm.Id, out _))
                {
                    VerificationError = $"Cannot remove '{vm.Id}' because installed package '{p.Id}' depends on it. Remove '{p.Id}' first.";
                    return; // Block removal completely
                }
            }
        }

        Packages.Remove(vm);
        RefreshServiceStatus();
    }

    [RelayCommand]
    private async Task BrowseResolvePackage(PackageRequirementViewModel vm)
    {
        if (RequestFolderPickerAsync != null)
        {
            var folder = await RequestFolderPickerAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                string fileUri = new Uri(folder).AbsoluteUri;
                if (!fileUri.EndsWith("/")) fileUri += "/";
                vm.Url = fileUri;
                await ConfirmResolvePackage(vm); // auto-verify upon browse
            }
        }
    }

    [RelayCommand]
    private async Task ConfirmResolvePackage(PackageRequirementViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Url))
        {
            vm.UnresolvedMessage = "URL cannot be conceptually empty.";
            return;
        }

        // 1. Check Local Path Schema
        if (vm.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            string? localPath = ResolveFileUrl(vm.Url);
            if (localPath == null) { vm.UnresolvedMessage = "Failed to resolve file URL."; return; }
            string packageJsonPath = Path.Combine(localPath, "package.json");
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
                    string parsedId = string.Empty;
                    if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                        parsedId = idProp.GetString() ?? string.Empty;
                    else if (doc.RootElement.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        parsedId = nameProp.GetString() ?? string.Empty;

                    if (parsedId == vm.Id)
                    {
                        if (doc.RootElement.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                            vm.Description = descProp.GetString() ?? string.Empty;
                        if (doc.RootElement.TryGetProperty("author", out var authProp) && authProp.ValueKind == JsonValueKind.String)
                            vm.Author = authProp.GetString() ?? string.Empty;
                        
                        if (doc.RootElement.TryGetProperty("entry", out var entryObj2) && entryObj2.ValueKind == JsonValueKind.Object)
                        {
                            if (entryObj2.TryGetProperty("assembly", out var assemblyProp2) && assemblyProp2.ValueKind == JsonValueKind.String)
                                vm.AssemblyEntry = assemblyProp2.GetString() ?? string.Empty;
                            if (entryObj2.TryGetProperty("class", out var classProp2) && classProp2.ValueKind == JsonValueKind.String)
                                vm.EntryClass = classProp2.GetString() ?? string.Empty;
                        }
                        if (doc.RootElement.TryGetProperty("services", out var srvProp))
                        {
                            if (srvProp.TryGetProperty("provides", out var pProp) && pProp.ValueKind == JsonValueKind.Array)
                            {
                                var names = new List<string>();
                                foreach (var p in pProp.EnumerateArray())
                                {
                                    if (p.ValueKind == JsonValueKind.String) names.Add(p.GetString()!);
                                    else if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("interface", out var iface))
                                        names.Add(iface.GetString()!);
                                }
                                vm.ServicesProvides = string.Join(", ", names.Where(x => !string.IsNullOrEmpty(x)));
                            }
                            if (srvProp.TryGetProperty("requires", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
                            {
                                var names = new List<string>();
                                foreach (var r in rProp.EnumerateArray())
                                {
                                    if (r.ValueKind == JsonValueKind.String) names.Add(r.GetString()!);
                                    else if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("interface", out var iface))
                                        names.Add(iface.GetString()!);
                                }
                                vm.ServicesRequires = string.Join(", ", names.Where(x => !string.IsNullOrEmpty(x)));
                            }
                        }

                        vm.IsLocal = true;
                        vm.IsUnresolved = false;
                        vm.ClearRegistryOptions();
                        vm.UnresolvedMessage = string.Empty;
                        // Synchronize deep dependencies silently if valid
                        ConfirmResolveRecursive(localPath);
                    }
                    else
                    {
                        vm.UnresolvedMessage = $"Mismatch: Expected '{vm.Id}', but selected folder contains '{parsedId}'.";
                    }
                }
                catch { vm.UnresolvedMessage = "Invalid package.json selected."; }
            }
            else
            {
                vm.UnresolvedMessage = "Chosen directory does not contain a package.json.";
            }
        }
        else if (vm.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (IsRegistryIndexUrl(vm.Url))
            {
                await ConfirmRegistryPackageAsync(vm);
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.Id))
            {
                vm.UnresolvedMessage = "Package ID is required for direct remote package archives.";
                return;
            }

            if (string.IsNullOrWhiteSpace(vm.Version))
            {
                vm.UnresolvedMessage = "Exact Version is required for direct remote package archives.";
                return;
            }

            vm.ClearRegistryOptions();
            vm.IsLocal = false;
            vm.IsUnresolved = false;
            vm.UnresolvedMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(vm.DisplayName) || vm.DisplayName == "Remote Package")
            {
                vm.DisplayName = vm.Id;
            }
        }
        else
        {
            vm.UnresolvedMessage = "Invalid URL Scheme. Must be file:// or http://";
        }
    }

    private async Task ConfirmRegistryPackageAsync(PackageRequirementViewModel vm)
    {
        try
        {
            var registryPackages = await new PackageRegistryClient().GetPackagesAsync(vm.Url);
            if (registryPackages.Count == 0)
            {
                vm.UnresolvedMessage = "Registry index contains no package versions.";
                vm.ClearRegistryOptions();
                return;
            }

            vm.SetRegistryOptions(registryPackages);
            var selectedPackage = PackageRegistryClient.SelectPackageVersion(registryPackages, vm.Id, vm.Version, out string rangeError);
            if (!string.IsNullOrWhiteSpace(rangeError))
            {
                vm.UnresolvedMessage = $"Invalid version range: {rangeError}";
                return;
            }

            if (selectedPackage == null)
            {
                if (vm.SelectedRegistryPackageOption == null)
                {
                    vm.SelectedRegistryPackageOption = vm.RegistryPackageOptions.FirstOrDefault();
                }

                vm.UnresolvedMessage = "Select a package version from the registry, or enter a matching semantic version range, then confirm again.";
                return;
            }

            vm.ApplyRegistryPackage(selectedPackage);
            vm.IsLocal = false;
            vm.IsUnresolved = false;
            vm.UnresolvedMessage = string.Empty;
        }
        catch (Exception ex)
        {
            vm.UnresolvedMessage = $"Failed to inspect registry: {ex.Message}";
            vm.ClearRegistryOptions();
        }
    }

    private static bool IsRegistryIndexUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.LocalPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRemoteUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetLocalPackageOverride(string packageId, out string packageDir)
    {
        string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
        packageDir = Path.Combine(projectDir, "Local", packageId);
        return !string.IsNullOrWhiteSpace(packageId)
            && Directory.Exists(packageDir)
            && File.Exists(Path.Combine(packageDir, "package.json"));
    }

    [RelayCommand]
    private async Task UseLocalOverride(PackageRequirementViewModel vm)
    {
        if (!TryGetLocalPackageOverride(vm.Id, out var packageDir))
        {
            vm.UnresolvedMessage = $"No local override package found at Local/{vm.Id}.";
            vm.IsUnresolved = true;
            return;
        }

        string projectDir = Path.GetDirectoryName(_project.ProjectPath)!;
        string relativePath = Path.GetRelativePath(projectDir, packageDir).Replace('\\', '/');
        vm.Url = $"file://{relativePath}";
        vm.HasLocalOverrideCandidate = false;
        await ConfirmResolvePackage(vm);
    }

    private void ConfirmResolveRecursive(string folder)
    {
        string packageJsonPath = Path.Combine(folder, "package.json");
        var deps = new System.Collections.Generic.Dictionary<string, string>();
        if (File.Exists(packageJsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
                if (doc.RootElement.TryGetProperty("dependencies", out var depsProp) && depsProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var dep in depsProp.EnumerateObject())
                    {
                        if (dep.Value.ValueKind == JsonValueKind.String) deps[dep.Name] = dep.Value.GetString()!;
                    }
                }
            } catch { return; }
        }

        string? parentDir = Path.GetDirectoryName(folder);
        if (!string.IsNullOrEmpty(parentDir))
        {
            foreach (var dep in deps)
            {
                if (Packages.Any(p => p.Id == dep.Key)) continue;

                string siblingFolder = Path.Combine(parentDir, dep.Key);
                if (Directory.Exists(siblingFolder) && File.Exists(Path.Combine(siblingFolder, "package.json")))
                {
                    AddLocalPackageRecursive(siblingFolder);
                }
                else
                {
                    Packages.Add(new PackageRequirementViewModel
                    {
                        Id = dep.Key,
                        Url = string.Empty,
                        Version = dep.Value,
                        IsUnresolved = true,
                        UnresolvedMessage = "Missing Dependency. Please assign a Url or Browse Local Folder."
                    });
                }
            }
        }
        
        RefreshServiceStatus();
    }

    [RelayCommand]
    public void AddDependency(PackageRequirementViewModel vm)
    {
        vm.Dependencies.Add(new DependencyViewModel { Id = "com.new.package", Version = "1.0.0" });
    }

    [RelayCommand]
    public void RemoveDependency(DependencyViewModel dep)
    {
        if (SelectedPackage != null)
        {
            SelectedPackage.Dependencies.Remove(dep);
        }
    }

    [RelayCommand]
    private void SaveLocalPackage(PackageRequirementViewModel vm)
    {
        if (vm.IsLocal && vm.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (vm.HasEditableServicesLoaded && vm.EditableServices != null)
            {
                vm.ServicesProvides = string.Join(", ", vm.EditableServices.Where(x => x.IsProvided).Select(x => x.ContractName));
                vm.ServicesRequires = string.Join(", ", vm.EditableServices.Where(x => x.IsRequired).Select(x => x.ContractName));
            }

            try
            {
                string? localPath = ResolveFileUrl(vm.Url);
                if (localPath == null) { VerificationError = "Failed to resolve package URL."; return; }
                string packageJsonPath = Path.Combine(localPath, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    string jsonContent = File.ReadAllText(packageJsonPath);
                    var node = JsonNode.Parse(jsonContent);
                    if (node is JsonObject obj)
                    {
                        // Update core fields
                        obj["name"] = vm.DisplayName;
                        obj["version"] = vm.Version;
                        obj["description"] = vm.Description;
                        obj["author"] = vm.Author;

                        // Entry
                        if (!string.IsNullOrEmpty(vm.AssemblyEntry) || !string.IsNullOrEmpty(vm.EntryClass))
                        {
                            var entryObj = obj["entry"] as JsonObject ?? new JsonObject();
                            if (!string.IsNullOrEmpty(vm.AssemblyEntry)) entryObj["assembly"] = vm.AssemblyEntry;
                            if (!string.IsNullOrEmpty(vm.EntryClass)) entryObj["class"] = vm.EntryClass;
                            obj["entry"] = entryObj;
                        }
                        else
                        {
                            obj.Remove("entry");
                        }

                        // Dependencies
                        if (vm.Dependencies.Count > 0)
                        {
                            var depsObj = new JsonObject();
                            foreach (var d in vm.Dependencies) depsObj[d.Id] = d.Version;
                            obj["dependencies"] = depsObj;
                        }
                        else
                        {
                            obj.Remove("dependencies");
                        }

                        // Services
                        var providesArr = vm.ServicesProvides?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList() ?? new List<string>();
                        var requiresArr = vm.ServicesRequires?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList() ?? new List<string>();
                        
                        if (providesArr.Count > 0 || requiresArr.Count > 0)
                        {
                            var servicesObj = new JsonObject();
                            if (providesArr.Count > 0)
                            {
                                var pArr = new JsonArray();
                                foreach (var p in providesArr) pArr.Add(p);
                                servicesObj["provides"] = pArr;
                            }
                            if (requiresArr.Count > 0)
                            {
                                var rArr = new JsonArray();
                                foreach (var r in requiresArr) rArr.Add(r);
                                servicesObj["requires"] = rArr;
                            }
                            obj["services"] = servicesObj;
                        }
                        else
                        {
                            obj.Remove("services");
                        }

                        var options = new JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText(packageJsonPath, obj.ToJsonString(options));
                        
                        // Sync UI and Cache
                        vm.CachedManifest = ParsePackageManifest(packageJsonPath);
                        RefreshServiceStatus();

                        SaveFeedback = "Package saved successfully!";
                        Task.Delay(3000).ContinueWith(_ => SaveFeedback = string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                VerificationError = $"Failed to save package.json: {ex.Message}";
                SaveFeedback = string.Empty;
            }
        }
    }
}

public partial class PackageRequirementViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    private string _description = string.Empty;
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(HasAuthor))]
    private string _author = string.Empty;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemote))]
    private bool _isLocal;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _type = "managed";
    [ObservableProperty] private string _engineVersion = "Current";
    [ObservableProperty] private string _assemblyEntry = string.Empty;
    [ObservableProperty] private string _entryClass = string.Empty;
    [ObservableProperty] private string _servicesProvides = string.Empty;
    [ObservableProperty] private string _servicesRequires = string.Empty;
    [ObservableProperty] private string _nativeRuntimesDisplay = string.Empty;

    partial void OnDisplayNameChanged(string value) { }
    partial void OnVersionChanged(string value) { }
    partial void OnDescriptionChanged(string value) { }
    partial void OnAuthorChanged(string value) { }
    partial void OnAssemblyEntryChanged(string value) { }
    partial void OnEntryClassChanged(string value) { }
    partial void OnTypeChanged(string value) { }
    partial void OnServicesProvidesChanged(string value) { }
    partial void OnServicesRequiresChanged(string value) { }

    public ObservableCollection<DependencyViewModel> Dependencies { get; } = new();

    public PackageManagerViewModel? ParentContext { get; set; }
    public bool HasEditableServicesLoaded => _editableServices != null;

    [ObservableProperty] private bool _isEditingServices;
    partial void OnIsEditingServicesChanged(bool value)
    {
        if (value && _editableServices == null)
        {
            _editableServices = new ObservableCollection<ServiceSelectionViewModel>();
            if (ParentContext != null)
            {
                var provSet = new System.Collections.Generic.HashSet<string>(ServicesProvides.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                var reqSet = new System.Collections.Generic.HashSet<string>(ServicesRequires.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                foreach (var cap in ParentContext.Capabilities)
                {
                    _editableServices.Add(new ServiceSelectionViewModel
                    {
                        ContractName = cap.ContractName,
                        FriendlyName = cap.FriendlyName,
                        IsProvided = provSet.Contains(cap.ContractName),
                        IsRequired = reqSet.Contains(cap.ContractName)
                    });
                }
            }
            OnPropertyChanged(nameof(HasEditableServicesLoaded));
        }
    }

    private ObservableCollection<ServiceSelectionViewModel>? _editableServices;
    public ObservableCollection<ServiceSelectionViewModel>? EditableServices => _editableServices;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemote))]
    private bool _isUnresolved;

    [ObservableProperty] private string _unresolvedMessage = string.Empty;
    [ObservableProperty] private RegistryPackageOptionViewModel? _selectedRegistryPackageOption;
    [ObservableProperty] private bool _hasLocalOverrideCandidate;

    public bool IsRemote => !IsLocal && !IsUnresolved;
    public bool HasRegistryPackageOptions => RegistryPackageOptions.Count > 0;
    public ObservableCollection<RegistryPackageOptionViewModel> RegistryPackageOptions { get; } = new();

    partial void OnSelectedRegistryPackageOptionChanged(RegistryPackageOptionViewModel? value)
    {
        if (value == null)
            return;

        ApplyRegistryPackage(value.Package);
        IsUnresolved = true;
    }

    public void SetRegistryOptions(IReadOnlyList<PackageRegistryPackageVersion> packages)
    {
        SelectedRegistryPackageOption = null;
        RegistryPackageOptions.Clear();
        foreach (var package in packages)
        {
            RegistryPackageOptions.Add(new RegistryPackageOptionViewModel(package));
        }

        OnPropertyChanged(nameof(HasRegistryPackageOptions));
    }

    public void ClearRegistryOptions()
    {
        RegistryPackageOptions.Clear();
        SelectedRegistryPackageOption = null;
        OnPropertyChanged(nameof(HasRegistryPackageOptions));
    }

    public void ApplyRegistryPackage(PackageRegistryPackageVersion package)
    {
        Id = package.Id;
        Version = package.Version;
        DisplayName = string.IsNullOrWhiteSpace(package.Name) ? package.Id : package.Name;
        Description = package.Description;
        Type = string.IsNullOrWhiteSpace(package.Type) ? "managed" : package.Type;
    }

    // P5/P6: Cache the parsed manifest for performance
    private PackageJsonManifest? _cachedManifest;
    public PackageJsonManifest? CachedManifest 
    { 
        get => _cachedManifest;
        set => SetProperty(ref _cachedManifest, value);
    }
}

public sealed class RegistryPackageOptionViewModel
{
    public RegistryPackageOptionViewModel(PackageRegistryPackageVersion package)
    {
        Package = package;
        DisplayName = $"{package.Id} @ {package.Version}";
        Details = string.IsNullOrWhiteSpace(package.Description)
            ? package.ArchiveUrl
            : package.Description;
    }

    public PackageRegistryPackageVersion Package { get; }
    public string DisplayName { get; }
    public string Details { get; }
}

public partial class ServiceSelectionViewModel : ObservableObject
{
    public string ContractName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    
    [ObservableProperty] private bool _isProvided;
    [ObservableProperty] private bool _isRequired;
}

public sealed class PackageTemplateViewModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string PackageId { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public string Type { get; init; } = "managed";
    public string Layer { get; init; } = "user";
    public string Dependencies { get; init; } = string.Empty;
    public bool GenerateEntry { get; init; }
    public bool AddToBaseManifest { get; init; }
    public string TargetProfile { get; init; } = "Development";
}


public partial class DependencyViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
}

public partial class PackageGraphPackageViewModel : ObservableObject
{
    [ObservableProperty] private int _order;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private string _dependenciesDisplay = string.Empty;
}

public partial class PackageGraphEdgeViewModel : ObservableObject
{
    [ObservableProperty] private string _from = string.Empty;
    [ObservableProperty] private string _to = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
}

internal sealed record BuildToolRunResult(bool Succeeded, int ExitCode, string StandardOutput, string StandardError);

internal sealed class PackageGraphDocument
{
    public string Profile { get; set; } = string.Empty;
    public List<PackageGraphPackageDocument> Packages { get; set; } = new();
    public List<PackageGraphEdgeDocument> Edges { get; set; } = new();
}

internal sealed class PackageGraphPackageDocument
{
    public int Order { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Dependencies { get; set; } = new();
}

internal sealed class PackageGraphEdgeDocument
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

public class PackageJsonEntry
{
    [JsonPropertyName("assembly")]
    public string Assembly { get; set; } = string.Empty;
    [JsonPropertyName("class")]
    public string Class { get; set; } = string.Empty;
}

public class PackageJsonServices
{
    public List<JsonElement>? Provides { get; set; }
    public List<JsonElement>? Requires { get; set; }
}

public class PackageJsonManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public string Type { get; set; } = "managed";
    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; set; } = "Current";
    [JsonPropertyName("entry")]
    public PackageJsonEntry? Entry { get; set; }
    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }
    [JsonPropertyName("services")]
    public PackageJsonServices? Services { get; set; }
    [JsonPropertyName("nativeRuntimes")] // A7: Corrected from PascalCase to camelCase
    public Dictionary<string, List<string>>? NativeRuntimes { get; set; }
}
