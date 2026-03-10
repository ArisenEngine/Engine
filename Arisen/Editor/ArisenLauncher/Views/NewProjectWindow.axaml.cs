using System;
using System.Linq;
using System.Threading.Tasks;
using ArisenLauncher.ViewModels;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ArisenLauncher.Views;

public partial class NewProjectWindow : Window
{
    public NewProjectWindow()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is NewProjectViewModel vm)
        {
            vm.RequestClose += (success) => {
                Close(success);
            };
            vm.RequestFolderPickerAsync = OnRequestFolderPickerAsync;
        }
    }

    private async Task<string?> OnRequestFolderPickerAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Project Parent Directory",
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
