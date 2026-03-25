using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArisenLauncher.Services;
using ArisenLauncher.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly EngineDiscoveryService _discoveryService;
    private readonly LauncherProcessService _processService;
    private readonly ProjectService _projectService;
    private readonly ILogService _logService;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private EngineInstance? _selectedEngine;

    public ObservableCollection<EngineInstance> Engines { get; } = new();
    public ObservableCollection<LauncherProjectMetadata> RecentProjects { get; } = new();

    // UI Communication Events
    public event Action<bool>? RequestWindowStateChange; // true = show, false = minimize
    public Func<Task<string?>>? RequestFolderPickerAsync;
    public Func<Task<string?>>? RequestFilePickerAsync; // For .arisenproj
    public Func<EngineInstance, Task<bool>>? RequestNewProjectWizardAsync;
    public Func<LauncherProjectMetadata, Task>? RequestPackageManagerAsync;

    public MainViewModel(
        ConfigService configService, 
        EngineDiscoveryService discoveryService, 
        LauncherProcessService processService,
        ProjectService projectService,
        ILogService logService)
    {
        _configService = configService;
        _discoveryService = discoveryService;
        _processService = processService;
        _projectService = projectService;
        _logService = logService;

        _processService.AllInstancesClosed += OnAllInstancesClosed;
        _processService.ProcessStarted += OnProcessStarted;

        _logService.Info("MainViewModel initialized with ProjectService.");
        LoadData();
    }

    private void LoadData()
    {
        _configService.Load();
        _discoveryService.Discover();

        RefreshLists();
    }

    private void RefreshLists()
    {
        Engines.Clear();
        foreach (var engine in _configService.Settings.EngineVersions)
        {
            Engines.Add(engine);
        }

        // Restore selection
        if (!string.IsNullOrEmpty(_configService.Settings.LastUsedEngineId))
        {
            SelectedEngine = Engines.FirstOrDefault(e => e.Id == _configService.Settings.LastUsedEngineId);
        }
        
        if (SelectedEngine == null && Engines.Count > 0)
        {
            SelectedEngine = Engines[0];
        }

        RecentProjects.Clear();
        foreach (var projPath in _configService.Settings.RecentProjects)
        {
            var metadata = _projectService.LoadProject(projPath);
            if (metadata != null)
            {
                RecentProjects.Add(metadata);
            }
            else
            {
                _logService.Warning($"Project file missing or invalid: {projPath}");
            }
        }
        
        _logService.Info($"UI Refreshed: {Engines.Count} engines, {RecentProjects.Count} valid projects found. Selected: {SelectedEngine?.Version}");
    }

    partial void OnSelectedEngineChanged(EngineInstance? value)
    {
        if (value != null && _configService.Settings.LastUsedEngineId != value.Id)
        {
            _configService.Settings.LastUsedEngineId = value.Id;
            _configService.Save();
        }
    }

    private void OnProcessStarted()
    {
        _logService.Info("Editor started, minimizing launcher.");
        RequestWindowStateChange?.Invoke(false);
    }

    private void OnAllInstancesClosed()
    {
        _logService.Info("No more editors running, restoring launcher.");
        RequestWindowStateChange?.Invoke(true);
    }

    [RelayCommand]
    private void RemoveFromRecent(LauncherProjectMetadata project)
    {
        if (_configService.Settings.RecentProjects.Contains(project.ProjectPath))
        {
            _configService.Settings.RecentProjects.Remove(project.ProjectPath);
            _configService.Save();
            RefreshLists();
            StatusText = $"Removed from recent: {project.Name}";
        }
    }

    [RelayCommand]
    private async Task BrowseProject()
    {
        if (RequestFilePickerAsync != null)
        {
            string? path = await RequestFilePickerAsync();
            if (!string.IsNullOrEmpty(path))
            {
                _logService.Info($"User browsed for project: {path}");
                
                string projectDir = System.IO.Path.GetDirectoryName(path)!;
                string manifestPath = System.IO.Path.Combine(projectDir, "manifest.json");
                
                if (!System.IO.File.Exists(manifestPath))
                {
                    StatusText = "Invalid project: Missing manifest.json";
                    _logService.Warning($"Project validation failed. Missing manifest.json in {projectDir}");
                    return;
                }

                var metadata = _projectService.LoadProject(path);
                if (metadata != null)
                {
                    if (!_configService.Settings.RecentProjects.Contains(path))
                    {
                        _configService.Settings.RecentProjects.Insert(0, path);
                        _configService.Save();
                        StatusText = $"Added project: {metadata.Name}";
                    }
                    else
                    {
                        StatusText = "Project is already in the list.";
                    }
                    RefreshLists();
                }
                else
                {
                    StatusText = "Invalid project file selected or corrupted.";
                }
            }
        }
    }

    [RelayCommand]
    private async Task AddEngine()
    {
        if (RequestFolderPickerAsync != null)
        {
            string? path = await RequestFolderPickerAsync();
            if (!string.IsNullOrEmpty(path))
            {
                _logService.Info($"User selected engine folder: {path}");
                if (_discoveryService.ValidateAndAdd(path, "Manual", isManual: true))
                {
                    StatusText = "Engine added successfully.";
                    RefreshLists();
                }
                else
                {
                    _logService.Warning($"Failed to validate engine folder: {path}");
                    StatusText = "Invalid engine folder. Ensure ArisenEngine.dll is present.";
                }
            }
        }
    }

    [RelayCommand]
    private async Task LaunchProject(LauncherProjectMetadata project)
    {
        var engine = SelectedEngine;
        if (engine != null)
        {
            StatusText = $"Launching {project.Name}...";
            bool success = await _projectService.LaunchProjectAsync(project, engine);
            if (!success) StatusText = "Failed to launch project. Check logs.";
            else StatusText = "Ready";
        }
        else
        {
            _logService.Error("Cannot launch project: No engine version selected.");
            StatusText = "Error: No engine selected.";
        }
    }

    [RelayCommand]
    private async Task ManagePackages(LauncherProjectMetadata project)
    {
        if (RequestPackageManagerAsync != null)
        {
            await RequestPackageManagerAsync(project);
        }
    }

    [RelayCommand]
    private async Task NewProject()
    {
        var engine = SelectedEngine;
        if (engine == null)
        {
            StatusText = "Error: No engine selected for new project.";
            return;
        }

        if (RequestNewProjectWizardAsync != null)
        {
            bool success = await RequestNewProjectWizardAsync(engine);
            if (success)
            {
                StatusText = "Project created successfully.";
                RefreshLists();
            }
        }
    }

    public NewProjectViewModel CreateNewProjectViewModel(EngineInstance engine)
    {
        return new NewProjectViewModel(_projectService, _logService, engine);
    }
}
