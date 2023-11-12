using NebulaEditor.Models;

namespace NebulaEditor.ViewModels;

public class SceneTreeNode : TreeNodeBase
{
    public SceneTreeNode(string name, string path, bool isBranch, bool isRoot = false) : base(name, path, isBranch, isRoot)
    {
        
    }
}