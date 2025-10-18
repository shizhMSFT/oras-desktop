using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace OrasProject.OrasDesktop.Converters;

/// <summary>
/// Converts a boolean value to an opacity value.
/// True (leaf node/actual repository) -> 1.0 (fully opaque)
/// False (parent/grouping node) -> 0.6 (lighter/dimmed)
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isLeaf)
        {
            return isLeaf ? 1.0 : 0.6;
        }
        return 1.0; // Default to fully opaque
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
