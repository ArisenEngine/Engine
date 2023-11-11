using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;

namespace NebulaEditor.ViewModels

{
    public class NTreeItemViewModel : ViewModelBase
    {
        public string Title { get; set; } = "TreeItem";
        private bool m_Expanded = false;

        public bool IsExpanded
        {
            get
            {
                return this.m_TreeViewItem.IsExpanded;
            }

            set
            {
                this.RaiseAndSetIfChanged(ref m_Expanded, value);
                Debug.WriteLine($"TreeItem:{IsExpanded}");
            }
        }

        private TreeViewItem m_TreeViewItem;
        

        public NTreeItemViewModel()
        {
            
        }

        public void BindTreeViewItem(TreeViewItem viewItem)
        {
            this.m_TreeViewItem = viewItem;
           
        }
        
    }
}