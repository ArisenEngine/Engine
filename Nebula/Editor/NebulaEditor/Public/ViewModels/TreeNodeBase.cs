using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace NebulaEditor.ViewModels;

public abstract class TreeNodeBase : ReactiveObject, IEditableObject
{
    private bool m_IsExpanded;
    public bool IsExpanded
    {
        get => m_IsExpanded;
        set 
        {
            this.RaiseAndSetIfChanged(ref m_IsExpanded, value && HasChildren);
        }
    }
    
    public bool IsBranch { get; }

    public bool IsRoot { get; private set; }

    public bool IsChecked { get; set; }

    private string m_Name = "TreeNode";
    public string Name 
    {
        get => m_Name;
        set => this.RaiseAndSetIfChanged(ref m_Name, value);
    }
    
    private long? m_Size;
    public long? Size 
    {
        get => m_Size;
        set => this.RaiseAndSetIfChanged(ref m_Size, value);
    }
    
    private string m_Path;
    public string Path 
    {
        get => m_Path;
        set => this.RaiseAndSetIfChanged(ref m_Path, value);
    }
    
    private DateTimeOffset? m_Modified;
    public DateTimeOffset? Modified 
    {
        get => m_Modified;
        set => this.RaiseAndSetIfChanged(ref m_Modified, value);
    }
    public abstract IReadOnlyList<T> Children<T>() where T : TreeNodeBase;
    
    public abstract bool HasChildren { get; }

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
    
    protected virtual string LeafIconPath => "";
    protected virtual string BranchIconPath => "";
    protected virtual string BranchOpenIconPath => "";

    protected virtual string RootIconPath => "";

    private bool m_IsImmutable;

    public bool Immutable => m_IsImmutable;
    
    public TreeNodeBase(
        string name, string path, bool isBranch, bool isRoot = false, bool isImmutable = false)
    {
        m_Path = path;
        m_Name = name;
        IsRoot = isRoot;
        m_IsExpanded = isRoot;
        IsBranch = isBranch;
        m_IsImmutable = isImmutable;
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
        OnBeginEdit();
    }

    protected virtual void OnBeginEdit()
    {
        
    }

    public void CancelEdit()
    {
        OnCancelEdit();
    }

    protected virtual void OnCancelEdit()
    {
        
    }

    public void EndEdit()
    {
        OnEndEdit();
    }

    protected virtual void OnEndEdit()
    {
        
    }
    
    #endregion
    
}