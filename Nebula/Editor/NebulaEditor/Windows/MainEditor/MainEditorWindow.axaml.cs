using Avalonia.Controls;
using Avalonia.Interactivity;
using NebulaEditor.GameDev;
using NebulaEditor.ViewModels;
using NebulaEditor.Views;
using NebulaEngine;
using System.Threading.Tasks;

namespace NebulaEditor.Windows.MainEditor
{
    public partial class MainEditorWindow : Window
    {
        public MainEditorWindowViewModel viewModel
        {
            get
            {
                return DataContext as MainEditorWindowViewModel;
            }
        }

        public MainEditorWindow()
        {
            InitializeComponent();

        }
/// <inheritdoc/>

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

        }
       
        private void OpenProjectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Task.Run(()=> {

                ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));

            });
        }
    }
}
