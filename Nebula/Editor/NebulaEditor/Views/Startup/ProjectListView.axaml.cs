using Avalonia.Controls;
using NebulaEditor.ViewModels.Startup;

namespace NebulaEditor.Views
{
    public partial class ProjectListView : UserControl
    {
        public ProjectListViewModel ViewModel
        {
            get
            {
                return DataContext as ProjectListViewModel;
            }
        }
        public ProjectListView()
        {
            InitializeComponent();
        }

        private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            ViewModel?.OnSelectionChanged?.Invoke(ProjectList.SelectedIndex);
        }
    }
}
