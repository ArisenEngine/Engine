using ArisenEditor.ViewModels.Startup;
using Avalonia.Controls;

namespace ArisenEditor.Views
{
    public partial class ProjectListView : UserControl
    {
        internal ProjectListViewModel ViewModel
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
            ViewModel?.OnSelectionChanged?.Invoke(CurrProjectList.SelectedIndex);
        }
    }
}
