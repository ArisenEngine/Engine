using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using ArisenLauncher.Services;
using ArisenEditorFramework.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArisenLauncher.ViewModels;

public partial class NewProjectViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly ILogService _logService;
    private readonly EngineInstance _engine;

    [ObservableProperty]
    private string _projectName = "MyNewProject";

    [ObservableProperty]
    private string _projectLocation;

    [ObservableProperty]
    private string? _selectedTemplate = "Blank Project";

    public ObservableCollection<string> Templates { get; } = new();

    public event Action<bool>? RequestClose;
    public Func<Task<string?>>? RequestFolderPickerAsync;

    public NewProjectViewModel(ProjectService projectService, ILogService logService, EngineInstance engine)
    {
        _projectService = projectService;
        _logService = logService;
        _engine = engine;
        
        _projectLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ArisenProjects");
        
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        try
        {
            Templates.Clear();
            string templatesPath = Path.Combine(_engine.InstallPath, "Templates");
            if (Directory.Exists(templatesPath))
            {
                foreach (var dir in Directory.GetDirectories(templatesPath))
                {
                    Templates.Add(Path.GetFileName(dir));
                }
            }

            if (Templates.Count > 0)
            {
                SelectedTemplate = Templates[0];
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to load templates.", ex);
        }
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
        _logService.Info($"Creating new project: {ProjectName} at {fullPath} with template {SelectedTemplate}");
        
        if (_projectService.CreateProject(fullPath, ProjectName, _engine, SelectedTemplate))
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
