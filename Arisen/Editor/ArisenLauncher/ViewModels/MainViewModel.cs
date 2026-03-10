using System.Collections.ObjectModel;
using System.Linq;
using ArisenLauncher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly EngineDiscoveryService _discoveryService;
    private readonly LauncherProcessService _processService;
    private readonly LogService _logService;

    [ObservableProperty]
    private string _statusText = "Ready";

    public ObservableCollection<EngineInstance> Engines { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    public MainViewModel(
        ConfigService configService, 
        EngineDiscoveryService discoveryService, 
        LauncherProcessService processService,
        LogService logService)
    {
        _configService = configService;
        _discoveryService = discoveryService;
        _processService = processService;
        _logService = logService;

        _logService.Info("MainViewModel initialized.");
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

        RecentProjects.Clear();
        foreach (var proj in _configService.Settings.RecentProjects)
        {
            RecentProjects.Add(proj);
        }
        
        _logService.Info($"UI Refreshed: {Engines.Count} engines, {RecentProjects.Count} projects found.");
    }

    [RelayCommand]
    private void AddEngine(string path)
    {
        _logService.Info($"Manually adding engine from path: {path}");
        if (_discoveryService.ValidateAndAdd(path, "Manual"))
        {
            StatusText = "Engine added successfully.";
            RefreshLists();
        }
        else
        {
            _logService.Warning($"Failed to validate engine folder: {path}");
            StatusText = "Invalid engine folder.";
        }
    }

    [RelayCommand]
    private void LaunchProject(string projectPath)
    {
        var engine = _configService.Settings.EngineVersions.FirstOrDefault();
        if (engine != null)
        {
            _processService.LaunchEditor(engine, projectPath);
        }
        else
        {
            _logService.Error("Cannot launch project: No engine version selected.");
            StatusText = "Error: No engine selected.";
        }
    }
}
