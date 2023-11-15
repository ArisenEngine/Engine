
using Avalonia.Controls;
using Avalonia.Interactivity;
using NebulaEditor.ViewModels;
using NebulaEngine;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using NebulaEditor.Utilities;
using NebulaEditor.ViewModels.Startup;
using NebulaEngine.Debugger;

namespace NebulaEditor.Windows.MainEditor
{
    public partial class MainEditorWindow : Window
    {
        private NebulaFileSystemWatcher m_FileSystemWatcher;

        private ConsoleViewModel m_ConsoleViewModel;

        internal MainEditorWindowViewModel viewModel
        {
            get { return DataContext as MainEditorWindowViewModel; }
        }

        public MainEditorWindow()
        {
            InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            this.Title = GameApplication.projectName;
            
            // World Hierarchy
            WorldHierarchyView.TreeGridViewer.ShowColumnHeaders = true;
            WorldHierarchyView.TreeGridViewer.CanUserSortColumns = false;
            // var worldViewModel = new WorldHierarchyViewModel();
            // this.WorldHierarchyView.TreeGridViewer.DataContext = worldViewModel;
            // this.WorldHierarchyView.TreeGridViewer.Bind(TreeDataGrid.SourceProperty, new Binding("Source"));

            // Project Hierarchy
            var projectViewModel = new ProjectHierarchyViewModel();
            // ProjectHierarchyView.TreeGridViewer.CanUserResizeColumns = true;
            this.ProjectHierarchyView.TreeGridViewer.DataContext = projectViewModel;
            this.ProjectHierarchyView.TreeGridViewer.Bind(TreeDataGrid.SourceProperty, new Binding("Source"));

            // Console Hierarchy
            m_ConsoleViewModel = new ConsoleViewModel();
            m_ConsoleViewModel.Messages.Clear();
            ConsoleHierarchyView.MessageList.AutoScrollToSelectedItem = false;
            ConsoleHierarchyView.MessageList.DataContext = m_ConsoleViewModel;
            Logger.MessageAdded += OnLogMessageAdd;
            
            // File Watcher
            m_FileSystemWatcher = new NebulaFileSystemWatcher();

            // Menu Items
            HeaderMenuContainer.Children.Clear();
            HeaderMenuContainer.Children.Add(ControlsFactory.CreateMenu(ControlsFactory.MenuType.Header));

            ProjectHierarchyView.TreeGridViewer.RowPrepared += OnProjectTreeViewRowPrepared;
            ProjectHierarchyView.TreeGridViewer.RowClearing += OnProjectTreeViewRowClearing;

            WorldHierarchyView.TreeGridViewer.ContextMenu =
                ControlsFactory.CreateContextMenu(ControlsFactory.MenuType.Hierarchy);

            int logCount = 0;
            Task.Run(async () =>
            {
                while (logCount < 10000)
                {
                    ++logCount;
                    Logger.Log($"Log:{logCount}");
                    Logger.Info($"Log:{logCount}");
                    Logger.Warning($"Log:{logCount}");
                    Logger.Error($"Log:{logCount}");
                    await Task.Delay(300);
                }
            });
        }

        private void OnLogMessageAdd(Logger.LogMessage message)
        {
            m_ConsoleViewModel.OnAddMessage(message);
        }
        
        private void OnProjectTreeViewRowPrepared(object? sender, TreeDataGridRowEventArgs args)
        {
            if (args != null && args.Row != null && args.Row.ContextMenu == null)
            {
                args.Row.ContextMenu = ControlsFactory.CreateContextMenu(ControlsFactory.MenuType.Project);
            }
        }
        
        private void OnProjectTreeViewRowClearing(object? sender, TreeDataGridRowEventArgs args)
        {
            if (args != null && args.Row != null)
            {
                args.Row.ContextMenu = null;
            }
        }

        private async Task<int> LoadEditorConfigAsync(MainWindowViewModel mainWindowViewModel)
        {
            int code = await mainWindowViewModel.LoadEditorConfigAsync();
            return code;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            m_FileSystemWatcher.Dispose();
            m_FileSystemWatcher = null;

            ProjectHierarchyView.TreeGridViewer.RowPrepared -= OnProjectTreeViewRowPrepared;
            ProjectHierarchyView.TreeGridViewer.RowClearing -= OnProjectTreeViewRowClearing;
            
            Logger.MessageAdded -= OnLogMessageAdd;
            
            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindowViewModel = new MainWindowViewModel();

                int resultCode = LoadEditorConfigAsync(mainWindowViewModel).Result;
                if (resultCode != 0)
                {
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