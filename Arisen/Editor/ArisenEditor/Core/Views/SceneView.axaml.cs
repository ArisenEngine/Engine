using ArisenEditor.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ArisenEditor.Views;

public partial class SceneView : UserControl
{
    public SceneView()
    {
        InitializeComponent();
        
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        LoadViewport();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        SceneViewContainer.Children.Clear();
    }
    
    private void LoadViewport()
    {
        SceneViewContainer.Children.Add(new EditorViewportView()
        {
            DataContext = new EditorViewportViewModel(isSceneView: true)
        });
    }
}