using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ArisenLauncher.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class NewProjectViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly LogService _logService;
    private readonly EngineInstance _engine;

    [ObservableProperty]
    private string _projectName = "MyNewProject";

    [ObservableProperty]
    private string _projectLocation;

    [ObservableProperty]
    private string? _selectedTemplate = "Blank Project";

    public ObservableCollection<string> Templates { get; } = new() 
    { 
        "Blank Project", 
        "3D Core", 
        "2D Lite", 
        "Raytracing Starter" 
    };

    public event Action<bool>? RequestClose;
    public Func<Task<string?>>? RequestFolderPickerAsync;

    public NewProjectViewModel(ProjectService projectService, LogService logService, EngineInstance engine)
    {
        _projectService = projectService;
        _logService = logService;
        _engine = engine;
        
        _projectLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ArisenProjects");
    }

    [RelayCommand]
    private async Task BrowseLocation()
    {
        if (RequestFolderPickerAsync != null)
        {
            string? path = await RequestFolderPickerAsync();
            if (!string.IsNullOrEmpty(path))
            {
                ProjectLocation = path;
            }
        }
    }

    [RelayCommand]
    private void Create()
    {
        string fullPath = Path.Combine(ProjectLocation, ProjectName);
        _logService.Info($"Creating new project: {ProjectName} at {fullPath}");
        
        if (_projectService.CreateProject(fullPath, ProjectName, _engine))
        {
            RequestClose?.Invoke(true);
        }
        else
        {
            _logService.Error("Wizard: Failed to create project.");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
