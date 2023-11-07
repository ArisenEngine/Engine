using Avalonia.Controls;
using NebulaEditor.GameDev;
using NebulaEngine;

namespace NebulaEditor.Windows.MainEditor
{
    public partial class MainEditorWindow : Window
    {
        public MainEditorWindow()
        {
            InitializeComponent();
        }

        private void OpenProjectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ProjectSolution.OpenVisualStudio(System.IO.Path.Combine(GameApplication.projectRoot, GameApplication.projectName + @".sln"));
        }
    }
}
