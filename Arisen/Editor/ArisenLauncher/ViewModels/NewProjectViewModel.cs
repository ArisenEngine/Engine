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
    private readonly ILogService _logService;
    private readonly EngineInstance _engine;

    [ObservableProperty]
    private string _projectName = "MyNewProject";

    [ObservableProperty]
    private string _projectLocation;

    [ObservableProperty]
    private string _defaultPackageId = "com.user.mynewproject";

    private bool _isPackageIdModifiedByUser = false;

    [ObservableProperty]
    private string? _selectedTemplate = "Blank Project";

    [ObservableProperty]
    private string _wizardError = string.Empty;

    public bool HasWizardError => !string.IsNullOrEmpty(WizardError);
    public bool HasTemplates => Templates.Count > 0;

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
            OnPropertyChanged(nameof(HasTemplates));
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to load templates.", ex);
            WizardError = "Failed to load project templates from engine.";
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
        UpdatePackageId();
    }

    partial void OnDefaultPackageIdChanged(string value)
    {
        if (value != $"com.user.{ProjectName.ToLower().Replace(" ", "")}")
        {
            _isPackageIdModifiedByUser = true;
        }
    }

    partial void OnProjectNameChanged(string value)
    {
        UpdatePackageId();
    }

    private void UpdatePackageId()
    {
        if (_isPackageIdModifiedByUser) return;

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            DefaultPackageId = $"com.user.{ProjectName.ToLower().Replace(" ", "")}";
        }
    }

    [RelayCommand]
    private void Create()
    {
        if (string.IsNullOrWhiteSpace(DefaultPackageId))
        {
            WizardError = "Default Package ID cannot be empty.";
            return;
        }
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            WizardError = "Project name cannot be empty.";
            return;
        }

        if (ProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            WizardError = $"Project name contains invalid characters.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectLocation) || !Directory.Exists(ProjectLocation))
        {
            WizardError = "Invalid project location.";
            return;
        }

        WizardError = string.Empty;

        string fullPath = Path.Combine(ProjectLocation, ProjectName);
        _logService.Info($"Creating new project: {ProjectName} at {fullPath} with template {SelectedTemplate} and package {DefaultPackageId}");
        
        if (_projectService.CreateProject(fullPath, ProjectName, _engine, SelectedTemplate, DefaultPackageId))
        {
            RequestClose?.Invoke(true);
        }
        else
        {
            WizardError = "Failed to create project files. Check logs for details.";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
