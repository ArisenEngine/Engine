using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NebulaEditor.ViewModels;
using System;
using System.Diagnostics;

namespace NebulaEditor.Views
{
    public partial class SceneHierarchyView : UserControl
    {
        private SceneHierarchyViewModel viewModel
        {
            get
            {
                return DataContext as SceneHierarchyViewModel;
            }
        }

        public SceneHierarchyView()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

           
        }

    }
}
