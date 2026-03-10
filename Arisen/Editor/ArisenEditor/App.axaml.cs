
using System;
using System.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using ArisenEditor.Core.Views;
using ArisenEditor.Themes;
using ArisenEditorFramework.Core;
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
                    LoadProjectAndLaunch(desktop, projectPath);
                }
                else
                {
                    // Fallback to a default or show error
                    Console.WriteLine("No valid project path provided via -project argument.");
                    desktop.Shutdown();
                }
            }
            
            base.OnFrameworkInitializationCompleted();
        }

        private void LoadProjectAndLaunch(IClassicDesktopStyleApplicationLifetime desktop, string projectPath)
        {
            try 
            {
                // This is a simplified initialization. In a full system, 
                // we'd use a ProjectService to load the metadata.
                var metadata = new ProjectMetadata 
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
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                desktop.MainWindow.Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch editor: {ex.Message}");
                desktop.Shutdown();
            }
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