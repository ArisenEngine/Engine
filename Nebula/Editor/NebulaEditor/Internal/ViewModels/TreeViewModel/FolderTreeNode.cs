using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NebulaEditor.Utilities;

namespace NebulaEditor.ViewModels;

public class FolderTreeNode : TreeNodeBase
{
    private string? m_UndoName;
    public FolderTreeNode(string name, string path, bool isBranch, bool isRoot = false, bool isImmutable = false) : base(name, path, isBranch, isRoot, isImmutable)
    {
       
    }

    private ObservableCollection<FolderTreeNode>? m_Children;
    
    private ObservableCollection<FolderTreeNode> LoadChildren()
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
        var result = new ObservableCollection<FolderTreeNode>();

        foreach (var d in Directory.EnumerateDirectories(Path, "*", options))
        {
            var name = d.Split(System.IO.Path.DirectorySeparatorChar)[^1];
            result.Add(new FolderTreeNode(name, d, true, false));
        }
        
        NebulaFileSystemWatcher.Changed += OnChanged;
        NebulaFileSystemWatcher.Created += OnCreated;
        NebulaFileSystemWatcher.Deleted += OnDeleted;
        NebulaFileSystemWatcher.Renamed += OnRenamed;

        return result;
    }

    public override bool ShowExpander => true;

    public override IReadOnlyList<FolderTreeNode> Children<FolderTreeNode>()
    {
        if (m_Children == null)
        {
            m_Children = LoadChildren();
        }
        return (IReadOnlyList<FolderTreeNode>)m_Children;
    }

    public override bool HasChildren => Children<FolderTreeNode>().Count > 0;

    protected override string LeafIconPath => "avares://NebulaEditor/Assets/Icons/file.png";
    protected override string BranchIconPath => "avares://NebulaEditor/Assets/Icons/folder.png";
    protected override string BranchOpenIconPath => "avares://NebulaEditor/Assets/Icons/folder-open.png";
    protected override string RootIconPath => "avares://NebulaEditor/Assets/Icons/AssetsRoot.png";

    

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Changed && File.Exists(e.FullPath))
        {
            foreach (var child in m_Children!)
            {
                if (child.Path == e.FullPath)
                {
                    if (!child.IsBranch)
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
            var node = new FolderTreeNode(
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
        else
        {
            var oldPath = Path;
            try
            {
                var dir = new DirectoryInfo(Path);
                Path = Path.Replace(m_UndoName, Name);
                dir.MoveTo(Path);
            }
            catch (Exception e)
            {
                Name = m_UndoName;
                Path = oldPath;
                var _ = MessageBoxUtility.ShowMessageBoxStandard("Rename folder failed", $"{e.Message}");
            }
        }

        m_UndoName = null;
    }
}