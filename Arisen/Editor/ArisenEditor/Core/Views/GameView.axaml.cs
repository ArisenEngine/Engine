using ArisenEditor.ViewModels;
using ArisenEngine.Views.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArisenEditor.Views;

public partial class GameView : UserControl
{
    public GameView()
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
        GameViewContainer.Children.Add(new RenderSurfaceView()
        {
            ParentWindow = Parent as Window,
            IsSceneView = false,
            SurfaceType = ArisenEngine.Rendering.SurfaceType.GameView,
            DataContext = new RenderSurfaceViewModel(false)
        });
    }
}