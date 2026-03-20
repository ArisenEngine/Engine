using System;
using System.Linq;
using System.Threading.Tasks;
using ArisenLauncher.ViewModels;
using ArisenLauncher.Services;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ArisenLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Subscribe to ViewModel requests
            vm.RequestWindowStateChange += OnRequestWindowStateChange;
            vm.RequestFolderPickerAsync = OnRequestFolderPickerAsync;
            vm.RequestFilePickerAsync = OnRequestFilePickerAsync;
            vm.RequestNewProjectWizardAsync = OnRequestNewProjectWizardAsync;
            vm.RequestPackageManagerAsync = OnRequestPackageManagerAsync;
        }
    }

    private async Task OnRequestPackageManagerAsync(Models.LauncherProjectMetadata project)
    {
        var pmConfig = new PackageManagerViewModel(project);
        var window = new PackageManagerWindow { DataContext = pmConfig };
        await window.ShowDialog(this);
    }

    private async Task<bool> OnRequestNewProjectWizardAsync(EngineInstance engine)
    {
        if (DataContext is MainViewModel vm)
        {
            var wizardVm = vm.CreateNewProjectViewModel(engine);
            var wizardWindow = new NewProjectWindow
            {
                DataContext = wizardVm
            };

            var result = await wizardWindow.ShowDialog<bool>(this);
            return result;
        }
        return false;
    }

    private async Task<string?> OnRequestFilePickerAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Arisen Project File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Arisen Project") { Patterns = new[] { "*.arisenproj" } }
                }
            });

            if (files.Any())
            {
                return files[0].Path.LocalPath;
            }
        }
        return null;
    }

    private void OnRequestWindowStateChange(bool show)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (show)
            {
                WindowState = WindowState.Normal;
                Show();
                Activate(); // Bring to front
            }
            else
            {
                WindowState = WindowState.Minimized;
            }
        });
    }

    private async Task<string?> OnRequestFolderPickerAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Arisen Engine Install Folder",
                AllowMultiple = false
            });

            if (folders.Any())
            {
                return folders[0].Path.LocalPath;
            }
        }
        return null;
    }
}
