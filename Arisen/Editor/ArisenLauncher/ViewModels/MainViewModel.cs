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

    [ObservableProperty]
    private string _statusText = "Ready";

    public ObservableCollection<EngineInstance> Engines { get; } = new();
    public ObservableCollection<string> RecentProjects { get; } = new();

    public MainViewModel(
        ConfigService configService, 
        EngineDiscoveryService discoveryService, 
        LauncherProcessService processService)
    {
        _configService = configService;
        _discoveryService = discoveryService;
        _processService = processService;

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
    }

    [RelayCommand]
    private void AddEngine(string path)
    {
        if (_discoveryService.ValidateAndAdd(path, "Manual"))
        {
            StatusText = "Engine added successfully.";
            RefreshLists();
        }
        else
        {
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
    }
}
