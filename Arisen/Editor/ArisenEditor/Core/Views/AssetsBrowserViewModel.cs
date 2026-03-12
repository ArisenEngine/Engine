using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using ArisenEngine;
using ReactiveUI;
using ArisenEngine.Core.Lifecycle;
using ArisenEditorFramework.Core;
using ArisenEditor.Views;
using ArisenEditor.Core.Services;
using ArisenEditor.Core.Views;

namespace ArisenEditor.ViewModels;

internal class AssetsBrowserViewModel : EditorPanelBase
{
    private string m_AssetsSearchText = String.Empty;

    public override string Title => "Assets Browser";
    public override string Id => "AssetsBrowser";
    public override object Content => new AssetsBrowserView { DataContext = this };

    public string AssetsSearchText
    {
        get => m_AssetsSearchText;
        set => this.RaiseAndSetIfChanged(ref m_AssetsSearchText, value);
    }

    public HierarchicalTreeDataGridSource<FileTreeNode> FolderSource { get; private set; }
    public FlatTreeDataGridSource<FileTreeNode> AssetsSource { get; private set; }
    
    private readonly ObservableCollection<FileTreeNode> m_AssetsItems = new();
    public ObservableCollection<FileTreeNode> AssetsItems => m_AssetsItems;

    private FileTreeNode[] m_FolderSelections = Array.Empty<FileTreeNode>();
    public FileTreeNode[] FolderSelections
    {
        get => m_FolderSelections;
        set => this.RaiseAndSetIfChanged(ref m_FolderSelections, value);
    }

    private FileTreeNode[] m_AssetSelections = Array.Empty<FileTreeNode>();
    public FileTreeNode[] AssetSelections
    {
        get => m_AssetSelections;
        set => this.RaiseAndSetIfChanged(ref m_AssetSelections, value);
    }

    public AssetsBrowserViewModel()
    {
        InitializeFolderSource();
        InitializeAssetsSource();
        
        this.WhenAnyValue(x => x.AssetsSearchText)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => RefreshAssetsList());
    }

    private void InitializeFolderSource()
    {
        FolderSource = new HierarchicalTreeDataGridSource<FileTreeNode>(Array.Empty<FileTreeNode>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileTreeNode>(
                    new TemplateColumn<FileTreeNode>(
                        "Project",
                        "NameCell",
                        "NameEditCell",
                        new GridLength(1, GridUnitType.Star),
                        new TemplateColumnOptions<FileTreeNode>
                        {
                            CompareAscending = FileTreeNode.SortAscending(x => x.Name),
                            CompareDescending = FileTreeNode.SortDescending(x => x.Name),
                            IsTextSearchEnabled = true,
                            TextSearchValueSelector = x => x.Name
                        }),
                    x => x.Children.OfType<FileTreeNode>(),
                    x => x.HasChildren,
                    x => x.IsExpanded
                ),
            }
        };

        FolderSource.RowSelection!.SingleSelect = true;
        FolderSource.RowSelection.SelectionChanged += FolderSelectionChanged;

        if (Design.IsDesignMode) return;

        FolderSource.Items = new[]
        {
            new FileTreeNode("Content", Path.Combine(ArisenApplication.s_ProjectRoot, "Content"), true, isRoot: true, true)
            {
                AllowDrag = false,
                AllowDrop = false
            }
        };
    }

    private void InitializeAssetsSource()
    {
        AssetsSource = new FlatTreeDataGridSource<FileTreeNode>(m_AssetsItems)
        {
            Columns =
            {
                new TemplateColumn<FileTreeNode>(
                    "Name",
                    "AssetIconNameCell",
                    "AssetIconNameEditCell",
                    new GridLength(1, GridUnitType.Star),
                    new TemplateColumnOptions<FileTreeNode>
                    {
                        CompareAscending = FileTreeNode.SortAscending(x => x.Name),
                        CompareDescending = FileTreeNode.SortDescending(x => x.Name),
                        IsTextSearchEnabled = true,
                        TextSearchValueSelector = x => x.Name
                    }),
                new TextColumn<FileTreeNode, string>("Size", x => x.SizeString),
                new TextColumn<FileTreeNode, DateTimeOffset>("Modified", x => x.Modified),
            }
        };

        AssetsSource.RowSelection!.SingleSelect = false;
        AssetsSource.RowSelection.SelectionChanged += AssetsSelectionChanged;
    }

    private void FolderSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        FolderSelections = FolderSource.RowSelection!.SelectedItems.ToArray();
        RefreshAssetsList();
    }

    private void AssetsSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileTreeNode> e)
    {
        AssetSelections = AssetsSource.RowSelection!.SelectedItems.ToArray();
    }

    private void RefreshAssetsList()
    {
        m_AssetsItems.Clear();
        
        foreach (var folder in FolderSelections)
        {
            if (!Directory.Exists(folder.Path)) continue;

            var entries = Directory.EnumerateFileSystemEntries(folder.Path, "*", SearchOption.TopDirectoryOnly);
            foreach (var entry in entries)
            {
                if (entry.EndsWith(".meta")) continue;

                bool isBranch = Directory.Exists(entry);
                var name = Path.GetFileName(entry);

                if (!string.IsNullOrEmpty(AssetsSearchText) && 
                    !name.Contains(AssetsSearchText, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var node = new FileTreeNode(name, entry, isBranch)
                {
                    AssetGuid = AssetDatabaseService.Instance.GetGuidFromPath(entry)
                };
                m_AssetsItems.Add(node);
            }
        }
    }
}
