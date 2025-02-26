using System.Collections.ObjectModel;
using ArisenEditor.Extensions.GameView;
using ArisenEditor.ViewModels;
using ArisenEngine.Views.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ArisenEditor.Views;

/// <summary>
/// 
/// </summary>
public partial class GameView : UserControl
{
    private GameViewModel m_GameViewModel { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public GameView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        m_GameViewModel = (DataContext as GameViewModel)!;
        LoadNativeRenderWindow();
        m_GameViewModel.OnLoaded();
        ResolutionComboBox.SelectedIndex = 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        m_GameViewModel.OnUnloaded();
    }
    
    private void LoadNativeRenderWindow()
    {
        GameViewContainer.Children.Add(new RenderSurfaceView()
        {
            SurfaceType = ArisenEngine.Rendering.SurfaceType.GameView,
            DataContext = new RenderSurfaceViewModel(false)
        });
    }

    
}