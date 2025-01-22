using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Media.Imaging;
using ArisenEditor.Models;

namespace ArisenEditor.ViewModels;

public class SceneTreeNode : TreeNodeBase
{
    public SceneTreeNode(string name, string path, bool isBranch, bool isRoot = false) : base(name, path, isBranch, isRoot, false)
    {
        
    }

    private ObservableCollection<SceneTreeNode> m_Children;
    private ObservableCollection<SceneTreeNode> LoadChildren()
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
        var result = new ObservableCollection<SceneTreeNode>();

        foreach (var d in Directory.EnumerateDirectories(Path, "*", options))
        {
            var name = d.Split(System.IO.Path.DirectorySeparatorChar)[^1];
            result.Add(new SceneTreeNode(name, d, true, false));
        }
      
        return result;
    }
    
    public override IReadOnlyList<SceneTreeNode> Children<SceneTreeNode>()
    {
        if (m_Children == null)
        {
            m_Children = LoadChildren();
        }

        return (IReadOnlyList<SceneTreeNode>) m_Children;
    }

    public override bool HasChildren => true;
    
    protected override string LeafIconPath => "avares://ArisenEditor/Assets/Icons/entity-icon.png";
    protected override string BranchIconPath => "avares://ArisenEditor/Assets/Icons/entity-icon.png";
    protected override string BranchOpenIconPath => "avares://ArisenEditor/Assets/Icons/entity-icon.png";
    protected override string RootIconPath => "avares://ArisenEditor/Assets/Icons/clapperboard.png";
    
}