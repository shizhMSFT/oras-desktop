using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OrasProject.OrasDesktop.Converters;

/// <summary>
/// Converts IsStatusError boolean to appropriate margin for status message.
/// When there's an error icon, margin is 0. When there's no error icon, adds left padding.
/// </summary>
public class StatusMessageMarginConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isError)
        {
            // If there's an error icon visible, no left margin needed (icon provides spacing)
            // If no error icon, add left margin to match the spacing
            return isError ? new Thickness(0) : new Thickness(10, 0, 0, 0);
        }

        return new Thickness(10, 0, 0, 0); // Default to left padding
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
