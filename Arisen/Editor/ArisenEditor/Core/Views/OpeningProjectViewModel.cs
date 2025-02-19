
using System.Diagnostics;
using System.Windows.Input;
using ArisenEditor.Core.Views;
using ArisenEditor.Models.Startup;
using ArisenEditor.Utilities;
using ArisenEditor.Windows.Startup;
using Avalonia.Controls.ApplicationLifetimes;
using ArisenEngine;
using Avalonia.ReactiveUI;
using ReactiveUI;

namespace ArisenEditor.ViewModels.Startup
{
    public class OpeningProjectViewModel : StartupSubViewBaseViewModel
    {
        public ICommand OpenProjectCommand { get; }
        public OpeningProjectViewModel(): base()
        {
            OpenProjectCommand = ReactiveCommand.Create(() =>
            {
                string errorMsg = string.Empty;
               if (this.ProjectListViewModel == null)
                {
                    errorMsg = "Got an error in project list";
                }
                else if (this.ProjectListViewModel.SelectedIndex < 0 || this.ProjectListViewModel.SelectedIndex > this.ProjectListViewModel.ProjectsList.Count - 1)
                {
                    errorMsg = "Please select a project";
                }

               if (string.IsNullOrEmpty(errorMsg))
                {
                    OpenProject(currenrProject: this.ProjectListViewModel.ProjectsList[ProjectListViewModel.SelectedIndex]);
                } 
                else
                {
                    _ = MessageBoxUtility.ShowMessageBoxStandard("Error", errorMsg);
                }

            }, outputScheduler: AvaloniaScheduler.Instance);
            
            
        }



        internal static async void OpenProject(ProjectInfo currenrProject, string copyFromPath = "")
        {
            Debug.WriteLine("Open Project:" + currenrProject.ProjectName);

            ArisenApplication.s_DataPath = System.IO.Path.Combine(currenrProject.ProjectFullPath(), "Content");
            ArisenApplication.s_ProjectRoot = currenrProject.ProjectFullPath();
            ArisenApplication.s_ProjectName = currenrProject.ProjectName;

            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var openingWindowViewModel = new OpeningWindowViewModel();
                var openingWindow = new OpeningWindowView()
                {
                    DataContext = openingWindowViewModel,
                };

                var oldWindow = desktop.MainWindow;
                
                // loading window 
                desktop.MainWindow = openingWindow;
                openingWindow.Show();

                if (oldWindow != null)
                {
                    oldWindow.Close();
                }

                await openingWindowViewModel.UpdateProgress();

                // main editor window
                var mainEditorWindow = new MainEditorHostView()
                {
                    DataContext = new MainEditorHostViewModel()
                };
                desktop.MainWindow = mainEditorWindow;
                mainEditorWindow.Show();

                openingWindow.Close();
            }
        }
    }
}