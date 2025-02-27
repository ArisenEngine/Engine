using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ArisenEditor.Extensions.GameView;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

internal class GameViewModel : BaseDocumentViewModel
{
    private ObservableCollection<GameViewResolutionConfig> m_GameViewResolutionConfigs = new();
    private readonly CompositeDisposable m_Disposables = new(); // 统一管理所有的订阅
    
    public ObservableCollection<GameViewResolutionConfig> GameViewResolutionConfigs
    {
        get => m_GameViewResolutionConfigs;
        set => this.RaiseAndSetIfChanged(ref m_GameViewResolutionConfigs, value);
    }
    
    private int m_GameViewResolutionSelectedIndex = 0;

    public int GameViewResolutionSelectedIndex
    {
        get => m_GameViewResolutionSelectedIndex;
        set => this.RaiseAndSetIfChanged(ref m_GameViewResolutionSelectedIndex, value);
    }
    
    private float m_GameViewScaleValue = 1.0f;

    public float GameViewScaleValue
    {
        get => m_GameViewScaleValue;
        set => this.RaiseAndSetIfChanged(ref m_GameViewScaleValue, value);
    }
    
    private GameViewResolutionConfig? m_SelectedResolution;
    public GameViewResolutionConfig? SelectedResolution
    {
        get => m_SelectedResolution;
        set => this.RaiseAndSetIfChanged(ref m_SelectedResolution, value);
    }
    
    internal GameViewModel()
    {
        Id = "GameView";
        Title = "Game";
        
        this.WhenAnyValue(x => x.SelectedResolution)
            .Where(res => res != null)
            .Subscribe(OnResolutionConfigChanged).DisposeWith(m_Disposables);
        
        this.WhenAnyValue(x=>x.GameViewScaleValue)
            .Subscribe(OnGameViewScaleChanged).DisposeWith(m_Disposables);
    }

    internal void OnUnloaded()
    {
        DetachActions();
        m_Disposables.Dispose();
    }

    internal void OnLoaded()
    {
        AttachActions();
        InitDefaultResolutionConfig();
    }

    private void OnResolutionConfigChanged(GameViewResolutionConfig? config)
    {
        GameViewResolution.s_OnResolutionChanged?.Invoke(config);
    }

    void OnGameViewScaleChanged(float value)
    {
        if (MathF.Abs(value - GameViewResolution.GameViewScale) < float.Epsilon)
        {
            return;
        }
        
        // GameViewResolution.GameViewScale = value;
    }

    void OnGameViewScaleUpdated(float value)
    {
        if (MathF.Abs(value - GameViewScaleValue) < float.Epsilon)
        {
            return;
        }
        
        GameViewScaleValue = value;
    }
    
    private void AttachActions()
    {
        GameViewResolution.s_OnResolutionListAdded -= OnResolutionListAdded;
        GameViewResolution.s_OnResolutionListAdded += OnResolutionListAdded;
        GameViewResolution.s_OnResolutionListRemoved -= OnResolutionListRemoved;
        GameViewResolution.s_OnResolutionListRemoved += OnResolutionListRemoved;
        GameViewResolution.s_OnGameViewScaleChanged -= OnGameViewScaleUpdated;
        GameViewResolution.s_OnGameViewScaleChanged += OnGameViewScaleUpdated;
    }

    private void DetachActions()
    {
        GameViewResolution.s_OnResolutionListAdded -= OnResolutionListAdded;
        GameViewResolution.s_OnResolutionListRemoved -= OnResolutionListRemoved;
        GameViewResolution.s_OnGameViewScaleChanged -= OnGameViewScaleUpdated;
    }
    
    private void OnResolutionListAdded(GameViewResolutionConfig config)
    {
        GameViewResolutionConfigs.Add(config);
    }

    private void OnResolutionListRemoved(GameViewResolutionConfig config)
    {
        
    }
    
    private void InitDefaultResolutionConfig()
    {
        GameViewResolution.AddGameViewResolutionConfig(
            new GameViewResolutionConfig("Free Aspect", 0, 0));
        
        GameViewResolution.AddGameViewResolutionConfig(
            new GameViewResolutionConfig("2560x1440 Landscape", 2560, 1440));
        
        GameViewResolution.AddGameViewResolutionConfig(
            new GameViewResolutionConfig("2560x1440 Portrait", 1440, 2560));
        
    }
    
}