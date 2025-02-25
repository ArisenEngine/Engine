using System;
using System.Collections.ObjectModel;
using ArisenEditor.Extensions.GameView;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

internal class GameViewModel : BaseDocumentViewModel
{
    private ObservableCollection<GameViewResolutionConfig> m_GameViewResolutionConfigs = new();

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
    
    internal GameViewModel()
    {
        Id = "GameView";
        Title = "Game";
        if (Design.IsDesignMode)
        {
            GameViewResolutionSelectedIndex = 0;
            GameViewResolutionConfigs.Add(new GameViewResolutionConfig()
            {
                Name = "2560x1440 Landscape",
                width = 2560,
                height = 1440,
                orientation = GameViewOrientation.Landscape
            });
            
            GameViewResolutionConfigs.Add(new GameViewResolutionConfig()
            {
                Name = "2560x1440 Portrait",
                width = 2560,
                height = 1440,
                orientation = GameViewOrientation.Portrait
            });
        }
    }

    internal void OnUnloaded()
    {
        DetachActions();
    }

    internal void OnLoaded()
    {
        AttachActions();
        InitDefaultResolutionConfig();
    }
    
    private void AttachActions()
    {
        GameViewResolutionComboBox.s_OnResolutionListAdded -= OnResolutionListAdded;
        GameViewResolutionComboBox.s_OnResolutionListAdded += OnResolutionListAdded;
        GameViewResolutionComboBox.s_OnResolutionListRemoved -= OnResolutionListRemoved;
        GameViewResolutionComboBox.s_OnResolutionListRemoved += OnResolutionListRemoved;
    }

    private void DetachActions()
    {
        GameViewResolutionComboBox.s_OnResolutionListAdded -= OnResolutionListAdded;
        GameViewResolutionComboBox.s_OnResolutionListRemoved -= OnResolutionListRemoved;
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
        GameViewResolutionComboBox.AddGameViewResolutionConfig(new GameViewResolutionConfig()
        {
            Name = "Free Aspect",
            width = 0,
            height = 0,
            orientation = GameViewOrientation.None
        });
        
        GameViewResolutionComboBox.AddGameViewResolutionConfig(new GameViewResolutionConfig()
        {
            Name = "2560x1440 Landscape",
            width = 2560,
            height = 1440,
            orientation = GameViewOrientation.Landscape
        });
        
        GameViewResolutionComboBox.AddGameViewResolutionConfig(new GameViewResolutionConfig()
        {
            Name = "2560x1440 Portrait",
            width = 2560,
            height = 1440,
            orientation = GameViewOrientation.Portrait
        });

        GameViewResolutionSelectedIndex = 0;
    }
}