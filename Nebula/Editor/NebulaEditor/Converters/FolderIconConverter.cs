using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Data.Converters;

namespace NebulaEditor.Converters;

public class FolderIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Debug.WriteLine($"Convert:{value}");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}