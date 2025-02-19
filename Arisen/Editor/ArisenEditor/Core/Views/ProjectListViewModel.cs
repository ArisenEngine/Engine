using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using ArisenEditor.Models.Startup;

namespace ArisenEditor.ViewModels.Startup
{
    internal class ProjectListViewModel : ReactiveObject
    {
        private int m_SelectedIndex = 0;
        internal int SelectedIndex 
        {
            get { return m_SelectedIndex; }
            set { this.RaiseAndSetIfChanged(ref m_SelectedIndex, value); }
        }

        internal ObservableCollection<ProjectInfo> ProjectsList { get; set; } = new ObservableCollection<ProjectInfo>()
        {
            new ProjectInfo(),
            new ProjectInfo()
        };

        internal Action<int> OnSelectionChanged;

    }
}
