using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NebulaEditor.Models.Startup;
using NebulaEditor.ViewModels;
using NebulaEditor.ViewModels.Startup;
using System.Diagnostics;

namespace NebulaEditor.Windows
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        private void TabControl_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            var vm = (this.DataContext as MainWindowViewModel);
            if (vm != null && EditorConfig.Instance != null && EditorConfig.Instance.Projects != null && EditorConfig.Instance.Projects.Count > 0)
            {
                vm.OpeningProjectViewModel?.SetProjectsList(EditorConfig.Instance.Projects);
            }
            
            e.Handled= true;
        }
    }
}

