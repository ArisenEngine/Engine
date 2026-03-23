using System;
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

    // Create Package State
    [ObservableProperty] private bool _isCreatingPackage;
    [ObservableProperty] private string _newPackageId = "com.mycompany.mypackage";
    [ObservableProperty] private string _newPackageName = "My Package";
    [ObservableProperty] private string _newPackageVersion = "1.0.0";
    [ObservableProperty] private string _newPackageType = "managed";
    [ObservableProperty] private bool _generatePackageEntry = true;
    [ObservableProperty] private string _newPackageAuthor = string.Empty;
    [ObservableProperty] private string _newPackageDependencies = string.Empty;
    [ObservableProperty] private string _createPackageError = string.Empty;
    
    public System.Collections.Generic.List<string> AvailablePackageTypes { get; } = new() { "managed", "native", "asset", "module" };

    partial void OnCreatePackageErrorChanged(string value) => OnPropertyChanged(nameof(HasCreatePackageError));
    public bool HasCreatePackageError => !string.IsNullOrEmpty(CreatePackageError);

    public ObservableCollection<PackageRequirementViewModel> Packages { get; } = new();

    public Func<Task<string?>>? RequestFolderPickerAsync;

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
                if (!Packages.Any(p => p.Id == req.Id))
                {
                    bool isMissing = string.IsNullOrWhiteSpace(req.Url);
                    string desc = string.Empty;
                    string auth = string.Empty;

                    if (!isMissing && req.Url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string localPath = Uri.UnescapeDataString(new Uri(req.Url).LocalPath);
                            string pkgJson = Path.Combine(localPath, "package.json");
                            if (File.Exists(pkgJson))
                            {
                                using var doc = JsonDocument.Parse(File.ReadAllText(pkgJson));
                                if (doc.RootElement.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                                    desc = descProp.GetString() ?? string.Empty;
                                if (doc.RootElement.TryGetProperty("author", out var authProp) && authProp.ValueKind == JsonValueKind.String)
                                    auth = authProp.GetString() ?? string.Empty;
                            }
                        }
                        catch { /* Ignore parsing errors during fallback load */ }
                    }

                    Packages.Add(new PackageRequirementViewModel { 
                        Id = req.Id, 
                        Url = req.Url ?? string.Empty, 
                        Version = req.Version ?? string.Empty,
                        Description = desc,
                        Author = auth,
                        IsUnresolved = isMissing,
                        UnresolvedMessage = isMissing ? "URL is completely missing in manifest." : string.Empty
                    });
                }
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
            var newPkg = new
            {
                // Must use lowercase schema
                schema = "https://arisen.dev/schemas/package-v2.json",
                id = NewPackageId,
                name = NewPackageName,
                version = NewPackageVersion,
                type = NewPackageType,
                author = NewPackageAuthor,
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

    private bool TryVerifyPackageSchema(string folder, out string id, out string version, out string description, out string author, out System.Collections.Generic.Dictionary<string, string> deps)
    {
        id = string.Empty;
        version = string.Empty;
        description = string.Empty;
        author = string.Empty;
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
        
        if (!TryVerifyPackageSchema(cleanFolder, out string id, out string version, out string description, out string author, out var deps))
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
            Author = author
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

    [ObservableProperty] private bool _isUnresolved;
    [ObservableProperty] private string _unresolvedMessage = string.Empty;
}
