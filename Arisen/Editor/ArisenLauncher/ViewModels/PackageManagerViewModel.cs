using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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

    [ObservableProperty]
    private string _verificationError = string.Empty;

    partial void OnVerificationErrorChanged(string value) => OnPropertyChanged(nameof(HasVerificationError));
    public bool HasVerificationError => !string.IsNullOrEmpty(VerificationError);

    public enum PackageSortMode { Name, Id }
    [ObservableProperty] private PackageSortMode _sortMode = PackageSortMode.Id;

    partial void OnSortModeChanged(PackageSortMode value) => UpdateFilteredPackages();


    // Create Package State
    [ObservableProperty] private bool _isCreatingPackage;
    [ObservableProperty] private string _newPackageId = "com.mycompany.mypackage";
    [ObservableProperty] private string _newPackageName = "My Package";
    [ObservableProperty] private string _newPackageVersion = "1.0.0";
    [ObservableProperty] private string _newPackageType = "managed";
    [ObservableProperty] private bool _generatePackageEntry = true;
    [ObservableProperty] private string _newPackageAuthor = string.Empty;
    [ObservableProperty] private string _newPackageDependencies = string.Empty;
    [ObservableProperty] private string _newPackageAssemblyEntry = string.Empty;
    [ObservableProperty] private string _createPackageError = string.Empty;
    
    public ObservableCollection<ServiceSelectionViewModel> NewPackageServices { get; } = new();
    
    public System.Collections.Generic.List<string> AvailablePackageTypes { get; } = new() { "managed", "native", "asset", "module" };

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

        if (SortMode == PackageSortMode.Name)
            query = query.OrderBy(p => p.Description ?? p.Id);
        else
            query = query.OrderBy(p => p.Id);

        FilteredPackages.Clear();
        foreach (var p in query)
        {
            FilteredPackages.Add(p);
        }
        
        if (SelectedPackage != null && !Packages.Contains(SelectedPackage))
        {
            SelectedPackage = null;
        }
    }

    public Func<Task<string?>>? RequestFolderPickerAsync;

    public PackageManagerViewModel(LauncherProjectMetadata project)
    {
        _project = project;
        _manifestPath = Path.Combine(Path.GetDirectoryName(project.ProjectPath)!, "manifest.json");
        
        Packages.CollectionChanged += (s, e) => UpdateFilteredPackages();

        InitializeServices();
        
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
                if (!Packages.Any(p => p.Id == req.Id))
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

                    if (!isMissing && (req.Url ?? "").StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string localPath = Uri.UnescapeDataString(new Uri(req.Url ?? "").LocalPath);
                            string pkgJson = Path.Combine(localPath, "package.json");
                            if (File.Exists(pkgJson))
                            {
                                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                                var pData = JsonSerializer.Deserialize<PackageJsonManifest>(File.ReadAllText(pkgJson), options);
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
                                            prov = string.Join(", ", pData.Services.Provides.Where(x => !string.IsNullOrEmpty(x)));
                                        if (pData.Services.Requires != null)
                                            reqsStr = string.Join(", ", pData.Services.Requires.Where(x => !string.IsNullOrEmpty(x)));
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
                            }
                        }
                        catch (Exception ex)
                        { 
                            VerificationError += $"Failed to parse {req.Id}/package.json: {ex.Message}; ";
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
                        ParentContext = this
                    };
                    foreach (var d in depsList) vm.Dependencies.Add(d);
                    Packages.Add(vm);
                }
            }
        }

        InitializeProfiles();
        RefreshServiceStatus();
    }

    [RelayCommand]
    public void SaveManifest()
    {
        Manifest.Packages = Packages.Select(x => new PackageRequirement { Id = x.Id, Url = x.Url, Version = string.IsNullOrEmpty(x.Version) ? null : x.Version }).ToList();
        
        // Save Profiles
        Manifest.Profiles = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<PackageRequirement>>();
        foreach (var profile in Profiles)
        {
            var reqs = new System.Collections.Generic.List<PackageRequirement>();
            foreach (var node in profile.Nodes)
            {
                reqs.Add(new PackageRequirement { Id = node.Id, Url = node.Url, Version = string.IsNullOrEmpty(node.Version) ? null : node.Version });
            }
            Manifest.Profiles[profile.Name] = reqs;
        }

        string json = JsonSerializer.Serialize(Manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }
    
    [RelayCommand]
    private void AddPackage()
    {
        string id = "com.new.package";
        if (!Packages.Any(p => p.Id == id))
        {
            Packages.Add(new PackageRequirementViewModel { Id = id, Url = "https://github.com/...", Version = "1.0.0" });
        }
    }
    
    [RelayCommand]
    private void BeginCreatePackage()
    {
        CreatePackageError = string.Empty;
        IsCreatingPackage = true;
        
        // Reset defaults
        NewPackageType = "managed";
        GeneratePackageEntry = true;
        NewPackageAssemblyEntry = string.Empty;
        
        NewPackageServices.Clear();
        foreach (var cap in Capabilities) 
        {
            NewPackageServices.Add(new ServiceSelectionViewModel { ContractName = cap.ContractName, FriendlyName = cap.FriendlyName });
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

        // Validation
        if (string.IsNullOrWhiteSpace(NewPackageId))
        {
            CreatePackageError = "Package ID cannot be empty.";
            return;
        }

        if (Packages.Any(p => p.Id == NewPackageId))
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

            var newPkg = new
            {
                // Must use lowercase schema
                schema = "https://arisen.dev/schemas/package-v2.json",
                id = NewPackageId,
                name = NewPackageName,
                version = NewPackageVersion,
                type = NewPackageType,
                author = NewPackageAuthor,
                entry = string.IsNullOrWhiteSpace(NewPackageAssemblyEntry) ? null : new { assembly = NewPackageAssemblyEntry },
                services = servicesObj,
                // MUST NOT provide subsystems
                dependencies = depsDict
            };

            string jsonPath = Path.Combine(packageDir, "package.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(newPkg, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }).Replace("\"schema\":", "\"$schema\":"));

            // Generate IPackageEntry C# Hook if requested and managed
            if (NewPackageType == "managed" && GeneratePackageEntry)
            {
                string safeNamespace = "ArisenEngine." + string.Join("", NewPackageId.Split('.').Select(part => char.ToUpper(part[0]) + part.Substring(1)));
                string csCode = $$"""
using ArisenKernel.Packages;
using ArisenKernel.Contracts;

namespace {{safeNamespace}}
{
    public class PackageEntry : IPackageEntry
    {
        public void OnLoad(IServiceRegistry registry)
        {
            // Initialize your package services here
        }

        public void OnUnload()
        {
            // Cleanup your package resources here
        }
    }
}
""";
                File.WriteAllText(Path.Combine(packageDir, "PackageEntry.cs"), csCode);
            }

            // Import generated package automatically
            AddLocalPackageRecursive(packageDir);

            // Close creation panel
            IsCreatingPackage = false;
        }
        catch (Exception ex)
        {
            CreatePackageError = $"Failed to perform package scaffolding: {ex.Message}";
        }
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

            if (p.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                string localPath = Uri.UnescapeDataString(new Uri(p.Url).LocalPath);
                string packageJsonPath = Path.Combine(localPath, "package.json");
                
                if (File.Exists(packageJsonPath))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
                        if (doc.RootElement.TryGetProperty("dependencies", out var depsProp) && depsProp.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var dep in depsProp.EnumerateObject())
                            {
                                if (dep.Name == vm.Id)
                                {
                                    VerificationError = $"Cannot remove '{vm.Id}' because installed package '{p.Id}' depends on it. Remove '{p.Id}' first.";
                                    return; // Block removal completely
                                }
                            }
                        }
                    }
                    catch { /* Ignore parsing errors during removal check */ }
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
                ConfirmResolvePackage(vm); // auto-verify upon browse
            }
        }
    }

    [RelayCommand]
    private void ConfirmResolvePackage(PackageRequirementViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Url))
        {
            vm.UnresolvedMessage = "URL cannot be conceptually empty.";
            return;
        }

        // 1. Check Local Path Schema
        if (vm.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            string localPath = Uri.UnescapeDataString(new Uri(vm.Url).LocalPath);
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
                        }
                        if (doc.RootElement.TryGetProperty("services", out var srvProp))
                        {
                            if (srvProp.TryGetProperty("provides", out var pProp) && pProp.ValueKind == JsonValueKind.Array)
                                vm.ServicesProvides = string.Join(", ", pProp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)));
                            if (srvProp.TryGetProperty("requires", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
                                vm.ServicesRequires = string.Join(", ", rProp.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)));
                        }

                        vm.IsLocal = true;
                        vm.IsUnresolved = false;
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
            // For Remote HTTP, we grant resolution instantly
            vm.IsUnresolved = false;
            vm.UnresolvedMessage = string.Empty;
        }
        else
        {
            vm.UnresolvedMessage = "Invalid URL Scheme. Must be file:// or http://";
        }
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
                string localPath = Uri.UnescapeDataString(new Uri(vm.Url).LocalPath);
                string packageJsonPath = Path.Combine(localPath, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    string oldJsonText = File.ReadAllText(packageJsonPath);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var doc = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(oldJsonText) ?? new();

                    doc["name"] = vm.DisplayName;
                    doc["version"] = vm.Version;
                    doc["description"] = vm.Description;
                    doc["author"] = vm.Author;

                    // Entry
                    var entry = new System.Collections.Generic.Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(vm.AssemblyEntry)) entry["assembly"] = vm.AssemblyEntry;
                    if (!string.IsNullOrEmpty(vm.EntryClass)) entry["class"] = vm.EntryClass;
                    if (entry.Count > 0) doc["entry"] = entry;
                    else doc.Remove("entry");

                    // Dependencies
                    var deps = new System.Collections.Generic.Dictionary<string, string>();
                    foreach (var d in vm.Dependencies) deps[d.Id] = d.Version;
                    if (deps.Count > 0) doc["dependencies"] = deps;
                    else doc.Remove("dependencies");

                    // Services
                    var providesArr = vm.ServicesProvides?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();
                    var requiresArr = vm.ServicesRequires?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();
                    if (providesArr.Length > 0 || requiresArr.Length > 0)
                    {
                        var servicesDict = new System.Collections.Generic.Dictionary<string, string[]>();
                        if (providesArr.Length > 0) servicesDict["provides"] = providesArr;
                        if (requiresArr.Length > 0) servicesDict["requires"] = requiresArr;
                        doc["services"] = servicesDict;
                    }
                    else doc.Remove("services");

                    string newJson = JsonSerializer.Serialize(doc, options);
                    newJson = newJson.Replace("\"schema\":", "\"$schema\":");
                    File.WriteAllText(packageJsonPath, newJson);
                    RefreshServiceStatus();
                }
            }
            catch (Exception ex)
            {
                VerificationError = $"Failed to save package.json: {ex.Message}";
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

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _type = "managed";
    [ObservableProperty] private string _engineVersion = "Current";
    [ObservableProperty] private string _entryClass = string.Empty;
    [ObservableProperty] private string _assemblyEntry = string.Empty;
    [ObservableProperty] private string _servicesProvides = string.Empty;
    [ObservableProperty] private string _servicesRequires = string.Empty;
    [ObservableProperty] private string _nativeRuntimesDisplay = string.Empty;
    [ObservableProperty] private bool _isLocal;

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
            OnPropertyChanged(nameof(EditableServices));
            OnPropertyChanged(nameof(HasEditableServicesLoaded));
        }
    }

    private ObservableCollection<ServiceSelectionViewModel>? _editableServices;
    public ObservableCollection<ServiceSelectionViewModel>? EditableServices => _editableServices;

    [ObservableProperty] private bool _isUnresolved;
    [ObservableProperty] private string _unresolvedMessage = string.Empty;

    public bool IsRemote => !IsLocal && !IsUnresolved;
}

public partial class ServiceSelectionViewModel : ObservableObject
{
    public string ContractName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    
    [ObservableProperty] private bool _isProvided;
    [ObservableProperty] private bool _isRequired;
}


public partial class DependencyViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
}

internal class PackageJsonEntry
{
    [JsonPropertyName("assembly")]
    public string Assembly { get; set; } = string.Empty;
    [JsonPropertyName("class")]
    public string Class { get; set; } = string.Empty;
}

internal class PackageJsonServices
{
    public List<string>? Provides { get; set; }
    public List<string>? Requires { get; set; }
}

internal class PackageJsonManifest
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
    [JsonPropertyName("NativeRuntimes")]
    public Dictionary<string, List<string>>? NativeRuntimes { get; set; }
}
