using ArisenEditor.ViewModels;
using ArisenEngine.Views.Rendering;
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
        LoadNativeRenderWindow();
    }

    private void LoadNativeRenderWindow()
    {
        // Preview
        SceneViewContainer.Children.Add(new RenderSurfaceView()
        {
            ParentWindow = Parent as Window,
            IsSceneView = true,
            SurfaceType = ArisenEngine.Rendering.SurfaceType.SceneView,
            DataContext = new RenderSurfaceViewModel(true)
        });
    }
}