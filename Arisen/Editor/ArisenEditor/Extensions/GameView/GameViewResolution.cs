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
public static class GameViewResolution
{
    private static Dictionary<string, GameViewResolutionConfig> s_ResolutionConfigs = new();
    
    public static Action<GameViewResolutionConfig> s_OnResolutionListAdded;
    public static Action<GameViewResolutionConfig> s_OnResolutionListRemoved;
    public static Action<GameViewResolutionConfig> s_OnResolutionChanged;

    internal static float s_GameViewScale = 1.0f;
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