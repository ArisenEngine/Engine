
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using NebulaEditor.Utilities;

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

        private object value;

        private string m_IconPath;
        public string IconPath
        {
            get => m_IconPath;
            
            set
            {
                if (!string.IsNullOrEmpty(value) && value != m_IconPath)
                {
                    m_IconPath = value;
                    m_Icon = ImageHelper.LoadFromResource(m_IconPath);
                }
            }
        }

        private Bitmap m_Icon;
        public Bitmap Icon
        {
            get
            {
                return m_Icon;
            }
        }

        public TreeNode(string title, string icon, ObservableCollection<TreeNode> subNodes)
        {
            Title = title;
            IconPath = icon;
            SubNodes = subNodes;
        }

        public TreeNode(string title, string icon)
        {
            Title = title;
            IconPath = icon;
            SubNodes= new ObservableCollection<TreeNode>();
        }

        public T GetUserData<T>()
        {
            return (T)value;
        }

        public void SetUserData(object userData)
        {
            value = userData;
        }

    }
}
