using Avalonia.Controls;
using Avalonia.Interactivity;
using NebulaEditor.GameDev;
using NebulaEditor.ViewModels;
using NebulaEditor.Views;
using NebulaEngine;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data;
using NebulaEditor.Utilities;
using NebulaEditor.ViewModels.Startup;
using ReactiveUI;
using MenuItem = NebulaEditor.Attributes.MenuItem;

namespace NebulaEditor.Windows.MainEditor
{
    internal partial class MainEditorWindow : Window
    {
        private NebulaFileSystemWatcher m_FileSystemWatcher;

        internal MainEditorWindowViewModel viewModel
        {
            get { return DataContext as MainEditorWindowViewModel; }
        }

        internal MainEditorWindow()
        {
            InitializeComponent();
        }

        /// <inheritdoc/>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            this.Title = GameApplication.projectName;

            WorldHierarchyView.TreeGridViewer.ShowColumnHeaders = false;
            WorldHierarchyView.TreeGridViewer.CanUserSortColumns = false;
            // var worldViewModel = new WorldHierarchyViewModel();
            // this.WorldHierarchyView.TreeGridViewer.DataContext = worldViewModel;
            // this.WorldHierarchyView.TreeGridViewer.Bind(TreeDataGrid.SourceProperty, new Binding("Source"));

            var projectViewModel = new ProjectHierarchyViewModel();
            this.ProjectHierarchyView.TreeGridViewer.DataContext = projectViewModel;
            this.ProjectHierarchyView.TreeGridViewer.Bind(TreeDataGrid.SourceProperty, new Binding("Source"));

            // File Watcher
            m_FileSystemWatcher = new NebulaFileSystemWatcher();

            // Menu Items
            HeaderMenuContainer.Children.Clear();
            HeaderMenuContainer.Children.Add(ControlsFactory.CreateMenu(ControlsFactory.MenuType.Header));

            ProjectHierarchyView.TreeGridViewer.RowPrepared += OnProjectTreeViewRowPrepared;
            ProjectHierarchyView.TreeGridViewer.RowClearing += OnProjectTreeViewRowClearing;

            WorldHierarchyView.TreeGridViewer.ContextMenu =
                ControlsFactory.CreateContextMenu(ControlsFactory.MenuType.Hierarchy);
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