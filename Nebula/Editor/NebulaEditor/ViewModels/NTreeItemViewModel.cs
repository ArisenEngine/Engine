using System.Diagnostics;
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
                return m_Expanded;
            }

            set
            {
                this.RaiseAndSetIfChanged(ref m_Expanded, value);
                Debug.WriteLine($"TreeItem:{IsExpanded}");
            }
        }
    }
}