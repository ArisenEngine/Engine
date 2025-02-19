using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Linq;
using ArisenEditor.Utilities;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ArisenEditor.ViewModels;

internal class FileTreeNode : TreeNodeBase
{
    private string? m_UndoName;
    internal FileTreeNode(string name, string path, bool isBranch, bool isRoot = false, bool isImmutable = false) : base(name, path, isBranch, isRoot, isImmutable)
    {
        if (isBranch)
        {
            Size = ArisenEngine.FileSystem.FileSystemUtilities.GetFolderSize(path);
            Modified = new DirectoryInfo(path).LastWriteTimeUtc;
        }
        else
        {
            var info = new FileInfo(path);
            Size = info.Length;
            Modified = info.LastWriteTimeUtc;
        }
    }

    private ObservableCollection<FileTreeNode>? m_Children;
    
    private ObservableCollection<FileTreeNode> LoadChildren()
    {
        if (!IsBranch)
        {
            throw new NotSupportedException();
        }
        
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true ,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };
        var result = new ObservableCollection<FileTreeNode>();

        foreach (var fullPath in Directory.EnumerateDirectories(Path, "*", options))
        {
            var name = fullPath.Split(System.IO.Path.DirectorySeparatorChar)[^1];
            result.Add(new FileTreeNode(name, fullPath, true, false));

        }
        
        ArisenFileSystemWatcher.Changed += OnChanged;
        ArisenFileSystemWatcher.Created += OnCreated;
        ArisenFileSystemWatcher.Deleted += OnDeleted;
        ArisenFileSystemWatcher.Renamed += OnRenamed;

        return result;
    }

    public override IReadOnlyList<FolderTreeNode> Children<FolderTreeNode>()
    {
        if (m_Children == null)
        {
            m_Children = LoadChildren();
        }
        return (IReadOnlyList<FolderTreeNode>)m_Children;
    }

    public override bool HasChildren => Children<FileTreeNode>().Count > 0;

    protected override string LeafIconPath => "avares://ArisenEditor/Assets/Icons/file.png";
    protected override string BranchIconPath => "avares://ArisenEditor/Assets/Icons/folder.png";
    protected override string BranchOpenIconPath => "avares://ArisenEditor/Assets/Icons/folder-open.png";
    protected override string RootIconPath => "avares://ArisenEditor/Assets/Icons/AssetsRoot.png";

    

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed && File.Exists(e.FullPath))
        {
            foreach (var child in m_Children!)
            {
                if (child.Path == e.FullPath)
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
                false);
            m_Children.Add(node);
            if (m_Children.Count == 1)
            {
                // first add child, force refresh the expander
                // TODO: this is a bug
                this.IsExpanded = true;
                this.IsExpanded = false;
            }
        }
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        for (var i = 0; i < m_Children!.Count; ++i)
        {
            if (m_Children[i].Path == e.FullPath)
            {
                m_Children.RemoveAt(i);
                if (m_Children.Count <= 0)
                { 
                    this.IsExpanded = true;
                    this.IsExpanded = false;
                }
                break;
            }
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        foreach (var child in m_Children!)
        {
            if (child.Path == e.OldFullPath)
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
                    var dir = new DirectoryInfo(Path);
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
                var _ = MessageBoxUtility.ShowMessageBoxStandard("Rename failed", $"{e.Message}");
            }
        }

        m_UndoName = null;
    }
}