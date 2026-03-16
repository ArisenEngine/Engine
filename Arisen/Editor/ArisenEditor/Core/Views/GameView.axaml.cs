using System.Collections.ObjectModel;
using ArisenEditor.Extensions.GameView;
using ArisenEditor.ViewModels;
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
        LoadViewport();
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
        GameViewContainer.Children.Clear();
    }
    
    private void LoadViewport()
    {
        GameViewContainer.Children.Add(new EditorViewportView()
        {
            DataContext = new EditorViewportViewModel(isSceneView: false)
        });
    }

    
}