
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;
using NebulaEditor.Models.Startup;
using NebulaEditor.Utilities;
using NebulaEditor.Windows.MainEditor;
using NebulaEditor.Windows.Startup;
using NebulaEngine;
using ReactiveUI;

namespace NebulaEditor.ViewModels.Startup
{
    public class OpeningProjectViewModel : StartupSubViewBaseViewModel
    {
        internal ICommand OpenProjectCommand { get; }
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

            });
            
            
        }



        internal static async void OpenProject(ProjectInfo currenrProject, string copyFromPath = "")
        {
            Debug.WriteLine("Open Project:" + currenrProject.ProjectName);

            NebulaApplication.s_DataPath = System.IO.Path.Combine(currenrProject.ProjectFullPath(), "Assets");
            NebulaApplication.s_ProjectRoot = currenrProject.ProjectFullPath();
            NebulaApplication.s_ProjectName = currenrProject.ProjectName;

            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var openingWindowViewModel = new OpeningWindowViewModel();
                var openingWindow = new OpeningWindow()
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
                var mainEditorWindow = new MainEditorWindow()
                {

                };
                desktop.MainWindow = mainEditorWindow;
                mainEditorWindow.Show();

                openingWindow.Close();
            }
        }
    }
}