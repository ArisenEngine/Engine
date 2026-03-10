using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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

    [ObservableProperty]
    private EngineInstance? _selectedEngine;

    public ObservableCollection<EngineInstance> Engines { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    // UI Communication Events
    public event Action<bool>? RequestWindowStateChange; // true = show, false = minimize
    public Func<Task<string?>>? RequestFolderPickerAsync;

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

        _processService.AllInstancesClosed += OnAllInstancesClosed;
        _processService.ProcessStarted += OnProcessStarted;

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
        foreach (var proj in _configService.Settings.RecentProjects)
        {
            RecentProjects.Add(proj);
        }
        
        _logService.Info($"UI Refreshed: {Engines.Count} engines, {RecentProjects.Count} projects found. Selected: {SelectedEngine?.Version}");
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
    private void LaunchProject(string projectPath)
    {
        var engine = SelectedEngine;
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
