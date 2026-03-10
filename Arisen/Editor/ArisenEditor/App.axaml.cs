
using System;
using System.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using ArisenEditor.Core.Views;
using ArisenEditor.Themes;
using ArisenEditor.Core.Models;
using ArisenEditor.Core.Lifecycle.BootSteps;
using ArisenEditorFramework.Lifecycle;
using ArisenEditorFramework.Utilities;
using ArisenEditorFramework.UI.Common;
using ArisenEditor.ViewModels;
using Avalonia.Controls;
using ArisenEngine;
using ReactiveUI;
using System.IO;

namespace ArisenEditor
{
    public partial class App : Application
    {
        internal static ThemeManager? ThemeManager;
        public override void Initialize()
        {
            ThemeManager = new ThemeManager();
            ThemeManager.Initialize(this);
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (OperatingSystem.IsWindows())
            {
                ArisenApplication.s_Platform = RuntimePlatform.Windows;
                    
            } else if (OperatingSystem.IsMacOS())
            {
                ArisenApplication.s_Platform = RuntimePlatform.MacOS;
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string[] args = Environment.GetCommandLineArgs();
                string? projectPath = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-project" && i + 1 < args.Length)
                    {
                        projectPath = args[i + 1];
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(projectPath) && File.Exists(projectPath))
                {
                    _ = ExecuteBootstrapSequence(desktop, projectPath);
                }
                else
                {
                    // Use Post to ensure we've entered the main loop before showing UI
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = ShowPickerAndLaunch(desktop));
                }
            }
            
            base.OnFrameworkInitializationCompleted();
        }

        private async Task ShowPickerAndLaunch(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Use native folder picker for project selection
            try 
            {
                var paths = await ArisenEditorFramework.Utilities.FileSystemUtilities.BrowserDirectory("Select Arisen Project Folder");
                if (paths == null || paths.Count == 0)
                {
                    desktop.Shutdown();
                    return;
                }

                string selectedFolder = paths[0];
                string[] projectFiles = Directory.GetFiles(selectedFolder, "*.arisenproj");

                if (projectFiles.Length == 0)
                {
                    await ArisenEditorFramework.Utilities.MessageBoxUtility.ShowMessageBoxStandard("Error", "No .arisenproj file found in the selected folder.");
                    desktop.Shutdown();
                    return;
                }

                string projectPath = projectFiles[0];
                await ExecuteBootstrapSequence(desktop, projectPath);
            }
            catch (Exception ex)
            {
                await ArisenEditorFramework.Utilities.MessageBoxUtility.ShowMessageBoxStandard("Error", $"An error occurred during project selection: {ex.Message}");
                desktop.Shutdown();
            }
        }

        private async Task ExecuteBootstrapSequence(IClassicDesktopStyleApplicationLifetime desktop, string projectPath)
        {
            var loadingWindow = new LoadingWindow();
            desktop.MainWindow = loadingWindow;
            loadingWindow.Show();

            var bootstrapper = new Bootstrapper();
            bootstrapper.AddStep(new EnvironmentValidationStep());
            bootstrapper.AddStep(new ProjectSynthesisStep());
            bootstrapper.AddStep(new DependencyConvergenceStep());
            bootstrapper.AddStep(new DataFabricStep());
            bootstrapper.AddStep(new HardwareWarmupStep());
            bootstrapper.AddStep(new StateReconstructionStep());
            bootstrapper.AddStep(new ExecutionHandoverStep());

            bootstrapper.ProgressChanged += (stage, status, progress) => {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    var stageText = loadingWindow.FindControl<TextBlock>("StageText");
                    var statusText = loadingWindow.FindControl<TextBlock>("StatusText");
                    var progressBar = loadingWindow.FindControl<ArisenEditorFramework.UI.Controls.LoadingBar>("ProgressBar");

                    if (stageText != null) stageText.Text = stage;
                    if (statusText != null) statusText.Text = status;
                    if (progressBar != null) progressBar.Progress = progress;
                });
            };

            var context = await bootstrapper.RunAsync(projectPath);

            if (context.Success)
            {
                LaunchEditor(desktop, projectPath);
                loadingWindow.Close();
            }
            else
            {
                await ArisenEditorFramework.Utilities.MessageBoxUtility.ShowMessageBoxStandard("Bootstrap Failed", context.ErrorMessage);
                desktop.Shutdown();
            }
        }

        private void LaunchEditor(IClassicDesktopStyleApplicationLifetime desktop, string projectPath)
        {
            var metadata = new EngineProjectMetadata 
            { 
                Name = Path.GetFileNameWithoutExtension(projectPath),
                ProjectPath = projectPath 
            };
            
            ArisenEditor.Core.EditorProjectContext.Initialize(metadata);
            
            var viewModel = new MainEditorHostViewModel();
            desktop.MainWindow = new Window
            {
                Title = $"Arisen Editor - {metadata.Name}",
                Content = new MainDockView { DataContext = viewModel },
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowState = WindowState.Maximized
            };
            desktop.MainWindow.Show();
        }

        public object CreateView(Window window)
        {
            throw new System.NotImplementedException();
        }
        
        private static bool IsProduction()
        {
#if DEBUG
            return false;
#else
        return true;
#endif
        }

        public static void Shutdown(IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ArisenInstance.DisposeLogger();
            desktop.Shutdown();
        }
    }
}