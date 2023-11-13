using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Threading;
using NebulaEngine;

namespace NebulaEditor.Utilities;

public partial class NebulaFileSystemWatcher : IDisposable
{
    private FileSystemWatcher? m_Watcher;
    
    internal NebulaFileSystemWatcher()
    {
        m_Watcher = new FileSystemWatcher()
        {
            Path = GameApplication.dataPath,
            NotifyFilter =
                NotifyFilters.Attributes
                | NotifyFilters.Security
                | NotifyFilters.Size
                | NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastAccess
                | NotifyFilters.LastWrite
        };

        m_Watcher.IncludeSubdirectories = true;
        m_Watcher.Changed += OnChangedInternal;
        m_Watcher.Renamed += OnRenamedInternal;
        m_Watcher.Deleted += OnDeletedInternal;
        m_Watcher.Created += OnCreatedInternal;
        m_Watcher.Error += OnErrorInternal;
        m_Watcher.EnableRaisingEvents = true;
    }

    private void OnErrorInternal(object sender, ErrorEventArgs e)
    {
        var _ = MessageBoxUtility.ShowMessageBoxStandard("Error", e.GetException().Message);
    }
    
    private void OnChangedInternal(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var fileInfo = new FileInfo(e.FullPath);
            if (fileInfo.Extension == ".meta")
            {
                // TODO: handle meta file 
            }
            else
            {
                // TODO: reimport
            }
            
            Changed?.Invoke(sender, e);
            
        });
    }

    private void OnRenamedInternal(object sender, RenamedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var fileInfo = new FileInfo(e.FullPath);
            if (fileInfo.Extension == ".meta")
            {
                // TODO: handle meta file 
            }
            else
            {
                // TODO: reimport
            }
            
            Renamed?.Invoke(sender, e);
            
        });
    }

    private void OnDeletedInternal(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var fileInfo = new FileInfo(e.FullPath);
            if (fileInfo.Extension == ".meta")
            {
                // TODO: handle meta file 
            }
            else
            {
                // TODO: reimport
            }
            
            Deleted?.Invoke(sender, e);
            
        });
    }

    private void OnCreatedInternal(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var fileInfo = new FileInfo(e.FullPath);
            if (fileInfo.Extension == ".meta")
            {
                // TODO: handle meta file 
            }
            else
            {
                // TODO: reimport
            }
            
            Created?.Invoke(sender, e);
            
        });
    }

    public void Dispose()
    {
        m_Watcher.Changed -= OnChangedInternal;
        m_Watcher.Renamed -= OnRenamedInternal;
        m_Watcher.Deleted -= OnDeletedInternal;
        m_Watcher.Created -= OnCreatedInternal;
        m_Watcher.Error -= OnErrorInternal;
    }
}