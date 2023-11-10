using Avalonia.Controls;
using Avalonia.Interactivity;
using NebulaEditor.GameDev;
using NebulaEditor.ViewModels;
using NebulaEditor.Views;
using NebulaEngine;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using NebulaEditor.ViewModels.Startup;

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

        private async Task<int> LoadEditorConfigAsync(MainWindowViewModel mainWindowViewModel)
        {
            int code = await mainWindowViewModel.LoadEditorConfigAsync();
            return code;
        }
        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindowViewModel = new MainWindowViewModel();
                
                int resultCode = LoadEditorConfigAsync(mainWindowViewModel).Result;
                if ( resultCode != 0 ) {
                    
                   desktop.Shutdown();
                   
                   return;
                }
                
                desktop.MainWindow = new MainWindow()
                {
                    DataContext = mainWindowViewModel
                };
                
                desktop.MainWindow.Show();
                
            }
        }
    }
}
