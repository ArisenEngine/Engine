using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace NebulaEditor.Models;

public class TreeNodeBase : ReactiveObject, IEditableObject
{
    private bool m_IsExpanded;
    public bool IsExpanded
    {
        get => m_IsExpanded;
        set => this.RaiseAndSetIfChanged(ref m_IsExpanded, value);
    }
    
    public bool IsBranch { get; }

    public bool IsRoot { get; private set; }

    public bool IsChecked { get; set; }

    private string m_Name = "TreeNode";
    public string Name 
    {
        get => m_Name;
        private set => this.RaiseAndSetIfChanged(ref m_Name, value);
    }
    
    private long? m_Size;
    public long? Size 
    {
        get => m_Size;
        private set => this.RaiseAndSetIfChanged(ref m_Size, value);
    }
    
    private string m_Path;
    public string Path 
    {
        get => m_Path;
        private set => this.RaiseAndSetIfChanged(ref m_Path, value);
    }
    
    private DateTimeOffset? m_Modified;
    public DateTimeOffset? Modified 
    {
        get => m_Modified;
        private set => this.RaiseAndSetIfChanged(ref m_Modified, value);
    }

    private ObservableCollection<TreeNodeBase>? m_Children;
    public IReadOnlyList<TreeNodeBase> Children => m_Children ??= LoadChildren();
    
    private bool m_HasChildren = true;
    public bool HasChildren
    {
        get => m_HasChildren;
        private set => this.RaiseAndSetIfChanged(ref m_HasChildren, value);
    }

    private Bitmap m_LeafIcon;

    public Bitmap LeafIcon
    {
        get
        {
            if (m_LeafIcon == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri(LeafIconPath)))
                {
                    m_LeafIcon = new Bitmap(fileStream);
                }
            }

            return m_LeafIcon;
        }
    }
    
    private Bitmap m_BranchIcon;
    
    public Bitmap BranchIcon
    {
        get
        {
            if (m_BranchIcon == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri(BranchIconPath)))
                {
                    m_BranchIcon = new Bitmap(fileStream);
                }
            }

            return m_BranchIcon;
        }
    }
    
    private Bitmap m_BranchOpenIcon;
    
    public Bitmap BranchOpenIcon
    {
        get
        {
            if (m_BranchOpenIcon == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri(BranchOpenIconPath)))
                {
                    m_BranchOpenIcon = new Bitmap(fileStream);
                }
            }

            return m_BranchOpenIcon;
        }
    }
    
    private Bitmap m_RootIcon;
    
    public Bitmap RootIcon
    {
        get
        {
            if (m_RootIcon == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri(RootIconPath)))
                {
                    m_RootIcon = new Bitmap(fileStream);
                }
            }

            return m_RootIcon;
        }
    }
    
    protected virtual string LeafIconPath => "avares://NebulaEditor/Assets/Icons/file.png";
    protected virtual string BranchIconPath => "avares://NebulaEditor/Assets/Icons/folder.png";
    protected virtual string BranchOpenIconPath => "avares://NebulaEditor/Assets/Icons/folder-open.png";

    protected virtual string RootIconPath => "avares://NebulaEditor/Assets/Icons/AssetsRoot.png";
    
    public TreeNodeBase(
        string name,
        string path,
        bool isBranch,
        bool isRoot = false)
    {
        m_Path = path;
        m_Name = name;
        IsExpanded = isRoot;
        IsRoot = isRoot;
        IsBranch = isBranch;
        HasChildren = IsBranch;
        
        // TOOD: get file info ?
    }


    protected virtual ObservableCollection<TreeNodeBase> LoadChildren()
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
        var result = new ObservableCollection<TreeNodeBase>();

        foreach (var d in Directory.EnumerateDirectories(Path, "*", options))
        {
            var name = d.Split(System.IO.Path.DirectorySeparatorChar)[^1];
            result.Add(new TreeNodeBase(name, d, true));
        }

        // foreach (var f in Directory.EnumerateFiles(Path, "*", options))
        // {
        //     result.Add(new TreeNodeBase(f, f,false));
        // }

        var _watcher = new FileSystemWatcher
        {
            Path = Path,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
        };

        // _watcher.Changed += OnChanged;
        // _watcher.Created += OnCreated;
        // _watcher.Deleted += OnDeleted;
        // _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;

        if (result.Count == 0)
            HasChildren = false;

        return result;
    }

    #region Sort

    public static Comparison<TreeNodeBase?> SortAscending<T>(Func<TreeNodeBase, T> selector)
    {
        return (x, y) =>
        {
            if (x is null && y is null)
                return 0;
            else if (x is null)
                return -1;
            else if (y is null)
                return 1;
            if (x.IsBranch == y.IsBranch)
                return Comparer<T>.Default.Compare(selector(x), selector(y));
            else if (x.IsBranch)
                return -1;
            else
                return 1;
        };
    }
    
    public static Comparison<TreeNodeBase?> SortDescending<T>(Func<TreeNodeBase, T> selector)
    {
        return (x, y) =>
        {
            if (x is null && y is null)
                return 0;
            else if (x is null)
                return 1;
            else if (y is null)
                return -1;
            if (x.IsBranch == y.IsBranch)
                return Comparer<T>.Default.Compare(selector(y), selector(x));
            else if (x.IsBranch)
                return -1;
            else
                return 1;
        };
    }

    #endregion
    
    
    #region IEditableObject

    public void BeginEdit()
    {
        throw new NotImplementedException();
    }

    public void CancelEdit()
    {
        throw new NotImplementedException();
    }

    public void EndEdit()
    {
        throw new NotImplementedException();
    }

    #endregion
}