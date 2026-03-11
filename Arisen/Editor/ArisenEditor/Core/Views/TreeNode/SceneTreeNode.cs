using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using ArisenEditorFramework.Hierarchy;
using Avalonia.Media.Imaging;
using ArisenEditor.Models;
using ArisenEngine.Core.Diagnostics;
using ArisenEditor.Core.Services;

namespace ArisenEditor.ViewModels;

internal class SceneTreeNode : TreeNodeBase
{
    private bool m_IsLoaded = false;

    internal SceneTreeNode(string name, string path, bool isBranch, bool isRoot = false) : base(name, path, isBranch, isRoot, false)
    {
        if (isBranch)
        {
            // Add a placeholder to show the expansion arrow if it's a branch
            // In a real implementation, we would check if it actually has children
        }
    }

    private void LoadChildren()
    {
        if (!IsBranch || m_IsLoaded) return;
        
        Children.Clear();
        
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true ,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        try
        {
            if (Directory.Exists(Path))
            {
                foreach (var d in Directory.EnumerateDirectories(Path, "*", options))
                {
                    var name = d.Split(System.IO.Path.DirectorySeparatorChar)[^1];
                    var newNode = new SceneTreeNode(name, d, true, false) { Parent = this };
                    Children.Add(newNode);
                }
            }
        }
        catch (Exception ex)
        {
            EditorLog.Error($"Failed to load children for {Path}: {ex.Message}");
        }
        
        m_IsLoaded = true;
    }

    public override bool HasChildren => IsBranch;
    
    // We handle expansion trigger separately or via a property change
    protected override void OnBeginEdit()
    {
        base.OnBeginEdit();
    }

    protected override string LeafIconPath => "avares://ArisenEditor/Assets/Icons/entity-icon.png";
    protected override string BranchIconPath => "avares://ArisenEditor/Assets/Icons/entity-icon.png";
    protected override string BranchOpenIconPath => "avares://ArisenEditor/Assets/Icons/entity-icon.png";
    protected override string RootIconPath => "avares://ArisenEditor/Assets/Icons/clapperboard.png";
}