using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Reactive.Linq;
using ArisenEngine.Core.Diagnostics;
using ArisenEditor.Core.Services;
using ArisenEditor.Utilities;
using ArisenEditorFramework.Hierarchy;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ArisenEditor.ViewModels;

internal class FileTreeNode : TreeNodeBase
{
    private string? m_UndoName;
    private bool m_IsLoaded = false;
    public Guid AssetGuid { get; set; }

    public ObservableCollection<FileTreeNode> Folders { get; } = new();

    internal FileTreeNode(string name, string path, bool isBranch, bool isRoot = false, bool isImmutable = false) : base(name, path, isBranch, isRoot, isImmutable)
    {
        if (isBranch)
        {
            Size = ArisenEngine.FileSystem.FileSystemUtilities.GetFolderSize(path);
            Modified = new DirectoryInfo(path).LastWriteTimeUtc;
            if (isRoot)
            {
                LoadChildren();
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(path))
            {
                var info = new FileInfo(path);
                Size = info.Length;
                Modified = info.LastWriteTimeUtc;
            }
            else
            {
                Size = 0;
                Modified = DateTimeOffset.MinValue;
            }
        }
    }

    private void LoadChildren()
    {
        if (!IsBranch || m_IsLoaded) return;
        
        Children.Clear();
        Folders.Clear();
        
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true ,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        try
        {
            if (Directory.Exists(Path))
            {
                foreach (var fullPath in Directory.EnumerateDirectories(Path, "*", options))
                {
                    var name = fullPath.Split(System.IO.Path.DirectorySeparatorChar)[^1];
                    // Create the child node and trigger its own dummy-child logic
                    var childNode = new FileTreeNode(name, fullPath, true, false) { Parent = this };
                    Children.Add(childNode);
                    Folders.Add(childNode);
                }
            }
        }
        catch (Exception ex)
        {
             EditorLog.Error($"Failed to load children for {Path}: {ex.Message}");
        }
        
        var watcher = ArisenEditorFramework.Utilities.ArisenFileSystemWatcher.Current;
        if (watcher != null)
        {
            watcher.Changed += OnChanged;
            watcher.Created += OnCreated;
            watcher.Deleted += OnDeleted;
            watcher.Renamed += OnRenamed;
        }

        m_IsLoaded = true;
    }

    public override bool HasChildren => IsBranch;

    // Trigger lazy load on expansion
    public override bool IsExpanded
    {
        get => base.IsExpanded;
        set
        {
            if (value && !m_IsLoaded)
            {
                LoadChildren();
            }
            base.IsExpanded = value;
        }
    }

    protected override string LeafIconPath => "avares://ArisenEditor/Assets/Icons/file.png";
    protected override string BranchIconPath => "avares://ArisenEditor/Assets/Icons/folder.png";
    protected override string BranchOpenIconPath => "avares://ArisenEditor/Assets/Icons/folder-open.png";
    protected override string RootIconPath => "avares://ArisenEditor/Assets/Icons/AssetsRoot.png";

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed && File.Exists(e.FullPath))
        {
            foreach (var childObj in Children)
            {
                if (childObj is FileTreeNode child && child.Path == e.FullPath)
                {
                    if (child.IsBranch)
                    {
                        var info = new DirectoryInfo(e.FullPath);
                        child.Size = ArisenEngine.FileSystem.FileSystemUtilities.GetFolderSize(e.FullPath);
                        child.Modified = info.LastWriteTimeUtc;
                    }
                    else
                    {
                        var info = new FileInfo(e.FullPath);
                        child.Size = info.Length;
                        child.Modified = info.LastWriteTimeUtc;
                    }
                    break;
                }
            }
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        bool isBranch = File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory);
        if (!isBranch)
        {
            return;
        }
        
        var name = e.FullPath.Split(System.IO.Path.DirectorySeparatorChar)[^1];
        var parentPath = e.FullPath.Substring(0, e.FullPath.Length - name.Length - 1);
        if (string.Equals(parentPath, this.Path))
        {
            var node = new FileTreeNode(
                name,
                e.FullPath,
                true,
                false) { Parent = this };
            Children.Add(node);
            Folders.Add(node);
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        for (var i = 0; i < Children.Count; ++i)
        {
            if (Children[i] is FileTreeNode child && child.Path == e.FullPath)
            {
                Children.RemoveAt(i);
                break;
            }
        }
        for (var i = 0; i < Folders.Count; ++i)
        {
            if (Folders[i].Path == e.FullPath)
            {
                Folders.RemoveAt(i);
                break;
            }
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        foreach (var childObj in Children)
        {
            if (childObj is FileTreeNode child && child.Path == e.OldFullPath)
            {
                child.Path = e.FullPath;
                child.Name = e.Name.Split(System.IO.Path.DirectorySeparatorChar)[^1] ?? child.Name;
                break;
            }
        }
    }
    
    protected override void OnBeginEdit()
    {
        m_UndoName = Name;
    }

    protected override void OnCancelEdit()
    {
        Name = m_UndoName;
    }

    protected override void OnEndEdit()
    {
        if (Immutable)
        {
            Name = m_UndoName;
        }
        else if (Name != m_UndoName)
        {
            var oldPath = Path;
            try
            {
                Path = Path.Replace(m_UndoName, Name);
                if (IsBranch)
                {
                    var dir = new DirectoryInfo(oldPath);
                    dir.MoveTo(Path);
                }
                else
                {
                    File.Move(oldPath, Path);
                }
            }
            catch (Exception e)
            {
                Name = m_UndoName;
                Path = oldPath;
                var _ = ArisenEditorFramework.Utilities.MessageBoxUtility.ShowMessageBoxStandard("Rename failed", $"{e.Message}");
            }
        }

        m_UndoName = null;
    }
}