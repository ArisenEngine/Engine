using NebulaEditor.Models.Startup;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.ViewModels.Startup
{
    public class ProjectListViewModel : ViewModelBase
    {
        private int m_SelectedIndex = 0;
        internal int SelectedIndex 
        {
            get { return m_SelectedIndex; }
            set { this.RaiseAndSetIfChanged(ref m_SelectedIndex, value); }
        }

        public ObservableCollection<ProjectInfo> ProjectsList { get; set; } = new ObservableCollection<ProjectInfo>()
        {
            new ProjectInfo(),
            new ProjectInfo()
        };

        internal Action<int> OnSelectionChanged;

    }
}
