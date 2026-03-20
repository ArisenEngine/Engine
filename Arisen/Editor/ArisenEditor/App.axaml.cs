
using System;
using System.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using ArisenEditor.Core.Views;
using ArisenEditorFramework.Services;
using ArisenEditor.Core.Models;
using ArisenEditor.Core.Lifecycle.BootSteps;
using ArisenEditorFramework.Lifecycle;
using ArisenEditorFramework.Utilities;
using ArisenEditorFramework.UI.Common;
using ArisenEditor.ViewModels;
using ArisenEditor.Core.Factory;
using Avalonia.Controls;
using ArisenEngine;
using ReactiveUI;
using System.IO;
using ArisenEditor.Core.Services;

namespace ArisenEditor
{
    public partial class App : Application
    {
        internal static IThemeManager? ThemeManager;
        private static ArisenEditor.Core.Lifecycle.EditorEngineRunner? s_EngineRunner;
        
        public override void Initialize()
        {
            ThemeManager = new ArisenEditorFramework.Services.ThemeManager(this);
            
            // Add Arisen Theme Resources
            Styles.Add((Avalonia.Styling.IStyle)AvaloniaXamlLoader.Load(new Uri("avares://ArisenEditorFramework/Resources/ArisenThemeResources.axaml")));
            
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Platform is now set in EngineConfig inside Program.cs entry points

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Explicitly tie the application's lifespan to the active MainWindow.
                // If the user clicks the 'X' button on the MainWindow (Splash or Editor), the app will shut down.
                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                desktop.Exit += (sender, args) => 
                {
                    s_EngineRunner?.Stop();
                    ArisenEngine.Core.Lifecycle.ArisenApplication.ShutdownEngine();
                };

                // Global UI exception handler
                Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (sender, args) => 
                {
                    EditorLog.Error($"[DispatcherException] {args.Exception}");
                    
                    // Show message box on UI thread
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () => 
                    {
                        await MessageBoxUtility.ShowMessageBoxStandard("UI Error", 
                            $"An internal UI error occurred:\n{args.Exception.Message}\n\nThe application will try to continue, but some features might be unstable.");
                    });

                    // Mark as handled to prevent process termination
                    args.Handled = true;
                };

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
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            await ExecuteBootstrapSequence(desktop, projectPath);
                        }
                        catch (Exception ex)
                        {
                            EditorLog.Error($"[Startup] Unhandled exception: {ex}");
                            await MessageBoxUtility.ShowMessageBoxStandard("Fatal Error", 
                                $"An unexpected error occurred during startup:\n{ex.Message}");
                            desktop.Shutdown();
                        }
                    });
                }
                else
                {
                    // Use Post to ensure we've entered the main loop before showing UI
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            await ShowPickerAndLaunch(desktop);
                        }
                        catch (Exception ex)
                        {
                            EditorLog.Error($"[Startup] Unhandled exception: {ex}");
                            await MessageBoxUtility.ShowMessageBoxStandard("Fatal Error", 
                                $"An unexpected error occurred during startup:\n{ex.Message}");
                            desktop.Shutdown();
                        }
                    });
                }
            }
            
            base.OnFrameworkInitializationCompleted();
        }

        private async Task ShowPickerAndLaunch(IClassicDesktopStyleApplicationLifetime desktop)
        {
            var loadingWindow = new LoadingWindow();
            desktop.MainWindow = loadingWindow;
            
            var stageText = loadingWindow.FindControl<TextBlock>("StageText");
            var statusText = loadingWindow.FindControl<TextBlock>("StatusText");
            var progressBar = loadingWindow.FindControl<ArisenEditorFramework.UI.Controls.LoadingBar>("ProgressBar");
            
            if (stageText != null) stageText.Text = "Arisen Engine";
            if (statusText != null) statusText.Text = "Waiting for project selection...";
            if (progressBar != null) progressBar.IsVisible = false;

            loadingWindow.Show();

            while (true)
            {
                var paths = await FileSystemUtilities.BrowserDirectory("Select Arisen Project Folder");
                if (paths == null || paths.Count == 0)
                {
                    // Picker was dismissed (e.g. clicking outside or canceled), terminate Editor directly
                    desktop.Shutdown();
                    return;
                }

                string selectedFolder = paths[0];
                string[] projectFiles = Directory.GetFiles(selectedFolder, "*.arisenproj");

                if (projectFiles.Length == 0)
                {
                    await MessageBoxUtility.ShowMessageBoxStandard(loadingWindow, "Error", 
                        "No .arisenproj file found in the selected folder.\nPlease select a valid Arisen project folder.");
                    // Let the user pick again
                    continue;
                }

                string projectPath = projectFiles[0];
                
                // Reset UI for Bootstrap Sequence
                if (stageText != null) stageText.Text = "Initializing Arisen Engine...";
                if (statusText != null) statusText.Text = "Loading core modules...";
                if (progressBar != null) progressBar.IsVisible = true;

                await ExecuteBootstrapSequence(desktop, projectPath, loadingWindow);
                return;
            }
        }

        private async Task ExecuteBootstrapSequence(IClassicDesktopStyleApplicationLifetime desktop, string projectPath, LoadingWindow? existingWindow = null)
        {
            var loadingWindow = existingWindow ?? new LoadingWindow();
            
            if (existingWindow == null)
            {
                desktop.MainWindow = loadingWindow;
                loadingWindow.Show();
            }

            var bootstrapper = new Bootstrapper();
            bootstrapper.AddStep(new EnvironmentValidationStep());
            bootstrapper.AddStep(new EngineInitializationStep());
            bootstrapper.AddStep(new AssetDatabaseInitializationStep());
            bootstrapper.AddStep(new ProjectSynthesisStep());
            bootstrapper.AddStep(new DependencyConvergenceStep());
            bootstrapper.AddStep(new DataFabricStep());
            bootstrapper.AddStep(new HardwareWarmupStep());
            bootstrapper.AddStep(new StateReconstructionStep());
            bootstrapper.AddStep(new ExecutionHandoverStep());

            bootstrapper.ProgressChanged += (stage, status, progress) => {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                    EditorLog.Log($"Bootstrapper::Execute {stage}, status: {status}, progress: {progress}");
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
                EditorLog.Error($"[Bootstrap] Failed: {context.ErrorMessage}");
                
                // Show the error message BEFORE closing the loading window, using it as owner.
                // This ensures the message box always has a valid parent window.
                await MessageBoxUtility.ShowMessageBoxStandard(loadingWindow, 
                    "Bootstrap Failed", context.ErrorMessage ?? "An unknown error occurred during bootstrap.");
                
                loadingWindow.Close();
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
            
            Core.EditorProjectContext.Initialize(metadata);
            
            var panelFactory = new ArisenPanelFactory();
            panelFactory.Initialize();
            
            var viewModel = new MainDockViewModel(panelFactory);
            var window = new Window
            {
                Title = $"Arisen Editor - {metadata.Name}",
                Content = new MainDockView { DataContext = viewModel },
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowState = WindowState.Maximized
            };
            // Attach the AI/Command-friendly Global Input Manager interceptor to the window root
            window.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, ArisenEditorFramework.Services.EditorInputManager.Instance.OnGlobalPreviewKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            
            // Register global shortcuts
            ArisenEditorFramework.Services.EditorInputManager.Instance.RegisterShortcut(
                new ArisenEditorFramework.Services.EditorShortcut(
                    Avalonia.Input.Key.S, 
                    Avalonia.Input.KeyModifiers.Control, 
                    (System.Windows.Input.ICommand)viewModel.SaveSceneCommand, 
                    bypassTextInput: true // Save works regardless of text focus
                )
            );

            // Register Undo (Ctrl+Z)
            ArisenEditorFramework.Services.EditorInputManager.Instance.RegisterShortcut(
                new ArisenEditorFramework.Services.EditorShortcut(
                    Avalonia.Input.Key.Z, 
                    Avalonia.Input.KeyModifiers.Control, 
                    ReactiveCommand.Create(() => 
                    {
                        if (ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.CanUndo)
                            ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Undo();
                    }), 
                    bypassTextInput: false // Don't undo globally if the text box natively undoes text
                )
            );

            // Register Redo (Ctrl+Y)
            ArisenEditorFramework.Services.EditorInputManager.Instance.RegisterShortcut(
                new ArisenEditorFramework.Services.EditorShortcut(
                    Avalonia.Input.Key.Y, 
                    Avalonia.Input.KeyModifiers.Control, 
                    ReactiveCommand.Create(() => 
                    {
                        if (ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.CanRedo)
                            ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Redo();
                    }), 
                    bypassTextInput: false
                )
            );

            // Register Delete / Backspace for Entities
            var deleteCmd = ReactiveCommand.Create(() => 
            {
                if (panelFactory is ArisenPanelFactory apf)
                {
                    if (apf.SelectionService.CurrentSelection is ArisenEditor.ViewModels.EntityNodeViewModel entityNode)
                    {
                        var svc = ArisenEditor.Core.Services.SceneManagerService.Instance;
                        if (svc.ActiveScene != null)
                        {
                            ArisenKernel.Lifecycle.EngineKernel.Instance.Services.GetService<ICommandManager>()!.Execute(
                                new ArisenEditor.Core.Commands.DeleteEntityCommand(entityNode.Entity, entityNode.Name)
                            );
                        }
                    }
                }
            });

            ArisenEditorFramework.Services.EditorInputManager.Instance.RegisterShortcut(
                new ArisenEditorFramework.Services.EditorShortcut(Avalonia.Input.Key.Delete, Avalonia.Input.KeyModifiers.None, deleteCmd, bypassTextInput: false));
            ArisenEditorFramework.Services.EditorInputManager.Instance.RegisterShortcut(
                new ArisenEditorFramework.Services.EditorShortcut(Avalonia.Input.Key.Back, Avalonia.Input.KeyModifiers.None, deleteCmd, bypassTextInput: false));

            desktop.MainWindow = window;
            desktop.MainWindow.Show();
            
            // Start the background Engine loop now that the UI is up
            s_EngineRunner = new ArisenEditor.Core.Lifecycle.EditorEngineRunner();
            s_EngineRunner.Start();
        }
        
        private static bool IsProduction()
        {
#if DEBUG
            return false;
#else
        return true;
#endif
        }
    }
}



