using System;
using System.Collections.Generic;

namespace ArisenEditor.Extensions.GameView;

/// <summary>
/// 
/// </summary>
public enum GameViewOrientation
{
    /// <summary>
    /// 
    /// </summary>
    None,
    
    /// <summary>
    /// 
    /// </summary>
    Portrait,
    /// <summary>
    /// 
    /// </summary>
    Landscape
}

/// <summary>
/// 
/// </summary>
public class GameViewResolutionConfig
{
    /// <summary>
    ///
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int width { get; set; }
    /// <summary>
    /// 
    /// </summary>
    public int height { get; set; }
    /// <summary>
    /// 
    /// </summary>
    private GameViewOrientation m_Orientation { get; set; }
    
    public GameViewOrientation Orientation => m_Orientation;
    
    /// <summary>
    /// 
    /// </summary>
    public GameViewResolutionConfig(string Name, int width, int height)
    {
        this.width = width;
        this.height = height;
        this.Name = Name;
        if (width <= 0 || height <= 0)
        {
            this.m_Orientation = GameViewOrientation.None;
        }
        else
        {
            this.m_Orientation = width > height ? GameViewOrientation.Landscape : GameViewOrientation.Portrait;
        }
    }
}

/// <summary>
/// 
/// </summary>
internal static class GameViewResolution
{
    private static Dictionary<string, GameViewResolutionConfig> s_ResolutionConfigs = new();
    
    internal static Action<GameViewResolutionConfig> s_OnResolutionListAdded;
    internal static Action<GameViewResolutionConfig> s_OnResolutionListRemoved;
    internal static Action<GameViewResolutionConfig> s_OnResolutionChanged;
    internal static Action<float> s_OnGameViewScaleChanged;
    

    private static float s_GameViewScale = 1.0f;

    public static float GameViewScale
    {
        get => s_GameViewScale;
        set
        {
            if (Math.Abs(s_GameViewScale - value) > float.Epsilon)
            {
                s_GameViewScale = value;
                s_OnGameViewScaleChanged?.Invoke(s_GameViewScale);
            }
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="config"></param>
    internal static void AddGameViewResolutionConfig(GameViewResolutionConfig config)
    {
        if (s_ResolutionConfigs.TryAdd(config.Name, config))
        {
            s_OnResolutionListAdded?.Invoke(config);
        }
        else
        {
            // TODO: log
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="config"></param>
    internal static void RemoveGameViewResolutionConfig(GameViewResolutionConfig config)
    {
        s_OnResolutionListRemoved?.Invoke(config);
        s_ResolutionConfigs.Remove(config.Name);
    }
}