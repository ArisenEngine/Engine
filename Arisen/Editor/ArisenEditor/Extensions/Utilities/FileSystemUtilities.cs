using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia;
using System.Threading.Tasks;
using System.Collections.Generic;
using DynamicData;
using System.IO;

namespace ArisenEditor.Utilities
{
    public static partial class FileSystemUtilities
    {
        public static async Task<List<string>?> BrowserDirectory(string Title, bool isAllowMultiple = false)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);

                if (topLevel != null)
                {
                    var location = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions()
                    {
                        AllowMultiple = isAllowMultiple,
                        Title = Title
                    });

                    if (location != null && location.Count > 0)
                    {
                        var pathList = new List<string>();
                        foreach (var item in location)
                        {
                            pathList.Add(item.Path.AbsolutePath.Replace('/', Path.DirectorySeparatorChar));
                        }
                        
                        return pathList;
                    }
                } 
                
                _ = MessageBoxUtility.ShowMessageBoxStandard("Error", "topLevel is null.");
                return null;
            }
            
            _ = MessageBoxUtility.ShowMessageBoxStandard("Error", "Application is not a desktop style app.");
            return null;
        }

        public static bool IsDirectoryEmpty(string path)
        {
            var files = Directory.GetFiles(path);
            var directories = Directory.GetDirectories(path);
            return directories.Length == 0 && files.Length == 0;
        }

    }
}
