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
    public GameViewOrientation orientation { get; set; }
    
    
    /// <summary>
    /// 
    /// </summary>
    public GameViewResolutionConfig()
    {
        
    }
}

/// <summary>
/// 
/// </summary>
public static class GameViewResolutionComboBox
{
    private static Dictionary<string, GameViewResolutionConfig> s_ResolutionConfigs = new();
    
    public static Action<GameViewResolutionConfig> s_OnResolutionListAdded;
    public static Action<GameViewResolutionConfig> s_OnResolutionListRemoved;
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