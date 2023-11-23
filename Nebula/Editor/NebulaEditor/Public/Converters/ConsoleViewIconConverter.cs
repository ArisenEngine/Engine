using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NebulaEditor.Models;
using NebulaEditor.ViewModels;
using NebulaEngine.Debugger;

namespace NebulaEditor.Converters;

public class ConsoleViewIconConverter : IValueConverter
{
    private static Bitmap m_Info;
    private static Bitmap InfoIcon
    {
        get
        {
            if (m_Info == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/info.png")))
                {
                    m_Info = new Bitmap(fileStream);
                }
            }

            return m_Info;
        }
    }
    
    private static Bitmap m_Log;
    private static Bitmap LogIcon
    {
        get
        {
            if (m_Log == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/log.png")))
                {
                    m_Log = new Bitmap(fileStream);
                }
            }

            return m_Log;
        }
    }
    
    private static Bitmap m_Warning;
    private static Bitmap WarningIcon
    {
        get
        {
            if (m_Warning == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/warning.png")))
                {
                    m_Warning = new Bitmap(fileStream);
                }
            }

            return m_Warning;
        }
    }
    
    private static Bitmap m_Error;
    private static Bitmap ErrorIcon
    {
        get
        {
            if (m_Error == null)
            {
                using (var fileStream = AssetLoader.Open(new Uri("avares://NebulaEditor/Assets/Icons/error.png")))
                {
                    m_Error = new Bitmap(fileStream);
                }
            }

            return m_Error;
        }
    }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value != null)
        {
            MessageItemNode messageItemNode = value as MessageItemNode;

            switch (messageItemNode.LogLevel)
            {
                case Logger.LogLevel.Error:
                    return ErrorIcon;
                case Logger.LogLevel.Log:
                    return LogIcon;
                case Logger.LogLevel.Info:
                    return InfoIcon;
                case Logger.LogLevel.Warning:
                    return WarningIcon;
            }
        }
        
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}