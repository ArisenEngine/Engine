using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using ArisenEditorFramework.Hierarchy;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

internal abstract class TreeNodeBase : ReactiveObject, IHierarchyItem, IEditableObject
{
    private bool m_IsExpanded;

    public virtual bool IsExpanded
    {
        get => m_IsExpanded;
        set { this.RaiseAndSetIfChanged(ref m_IsExpanded, value && HasChildren); }
    }

    private bool m_IsSelected;
    public bool IsSelected
    {
        get => m_IsSelected;
        set => this.RaiseAndSetIfChanged(ref m_IsSelected, value);
    }

    private bool m_IsEditing;
    public bool IsEditing
    {
        get => m_IsEditing;
        set => this.RaiseAndSetIfChanged(ref m_IsEditing, value);
    }

    public bool IsLeaf => !IsBranch;

    public object? Tag { get; set; }

    private IHierarchyItem? m_Parent;
    public IHierarchyItem? Parent
    {
        get => m_Parent;
        set => this.RaiseAndSetIfChanged(ref m_Parent, value);
    }

    private ObservableCollection<IHierarchyItem> m_Children = new();
    public ObservableCollection<IHierarchyItem> Children => m_Children;

    public System.Windows.Input.ICommand? BeginRenameCommand { get; protected set; }
    public System.Windows.Input.ICommand? EndRenameCommand { get; protected set; }
    public System.Windows.Input.ICommand? DeleteCommand { get; protected set; }

    private bool m_AllowDrag = true;

    public bool AllowDrag
    {
        get => m_AllowDrag;
        set => this.RaiseAndSetIfChanged(ref m_AllowDrag, value);
    }


    private bool m_AllowDrop = true;

    public bool AllowDrop
    {
        get => m_AllowDrop;
        set => this.RaiseAndSetIfChanged(ref m_AllowDrop, value);
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

    private long m_Size = 0;

    public long Size
    {
        get => m_Size;
        set
        {
            this.RaiseAndSetIfChanged(ref m_Size, value);
        }
    }

    public string SizeString
    {
        get
        {
            var bytes = Size;
            string[] sizes = { "Bytes", "KB", "MB", "GB", "TB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }

            return $"{bytes:0.##} {sizes[order]}";
        }
    }

    private string m_Path;

    public string Path
    {
        get => m_Path;
        set => this.RaiseAndSetIfChanged(ref m_Path, value);
    }

    private DateTimeOffset m_Modified;

    public DateTimeOffset Modified
    {
        get => m_Modified;
        set => this.RaiseAndSetIfChanged(ref m_Modified, value);
    }

    public abstract bool HasChildren { get; }

    private Bitmap? m_Icon;
    public Bitmap? Icon
    {
        get
        {
            if (m_Icon == null)
            {
                string path = IsRoot ? RootIconPath : (IsBranch ? (IsExpanded ? BranchOpenIconPath : BranchIconPath) : LeafIconPath);
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        using (var fileStream = AssetLoader.Open(new Uri(path)))
                        {
                            m_Icon = new Bitmap(fileStream);
                        }
                    }
                    catch
                    {
                        // Fallback or log error
                    }
                }
            }
            return m_Icon;
        }
        set => this.RaiseAndSetIfChanged(ref m_Icon, value);
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

        BeginRenameCommand = ReactiveCommand.Create(() => IsEditing = true);
        EndRenameCommand = ReactiveCommand.Create(() => IsEditing = false);
    }

    public virtual bool CanAcceptDrop(IHierarchyItem sourceItem) => AllowDrop;
    public virtual void AcceptDrop(IHierarchyItem sourceItem) { }

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
