using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArisenLauncher.Views;

public partial class PackageManagerWindow : Window
{
    public PackageManagerWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ArisenLauncher.ViewModels.PackageManagerViewModel vm)
        {
            vm.RequestFolderPickerAsync = async () =>
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                    {
                        Title = "Select Arisen Package Folder",
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        return folders[0].Path.LocalPath;
                    }
                }
                return null;
            };

            vm.RequestClose = () => Close();
        }
    }


    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        // View model command handles save, we just close or show a message
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
