
using System.Collections.ObjectModel;

namespace NebulaEditor.Models
{
    public enum NodeType
    {
        Unknow,
        Entity,
        Folder,
        File
    }

    public class TreeNode
    {
        public ObservableCollection<TreeNode> SubNodes { get; }
        public string Title { get; set; } = string.Empty;

        public NodeType NodeType { get; set; } = NodeType.Unknow;

        public object Value { get; set; }

        public string Icon { get; set; } = string.Empty;

        public TreeNode(string title, string icon, ObservableCollection<TreeNode> subNodes)
        {
            Title = title;
            Icon = icon;
            SubNodes = subNodes;
        }

        public TreeNode(string title, string icon)
        {
            Title = title;
            Icon = icon;
            SubNodes= new ObservableCollection<TreeNode>();
        }

    }
}
