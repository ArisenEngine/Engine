using System;
using System.Linq;
using System.Threading.Tasks;
using ArisenLauncher.ViewModels;
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
        }
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
