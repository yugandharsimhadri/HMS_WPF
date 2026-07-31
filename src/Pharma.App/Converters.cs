using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Pharma.App;

/// <summary>Highlights the nav button whose key matches the active page.</summary>
public class NavBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value?.ToString(), parameter?.ToString())
            ? new SolidColorBrush(Color.FromArgb(0x33, 0x2A, 0xA7, 0x9B))
            : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// Whether the nav key matches the active page — true where NavBrushConverter
/// would tint the button. The tint alone was the only sign of "you are here";
/// this drives a left bar and bold text on the same button so the current
/// page is never colour alone.
/// </summary>
public class NavActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter?.ToString() == "invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Shows a band only when there is something to say in it.</summary>
public class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Fee badge fill: paid reads calm, due has to catch the eye.</summary>
public class FeeBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(0xE4, 0xF3, 0xEA))
            : new SolidColorBrush(Color.FromRgb(0xFB, 0xEF, 0xDC));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Text on the fee badge, dark enough to read on its own fill.</summary>
public class FeeInkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(0x0B, 0x5A, 0x54))
            : new SolidColorBrush(Color.FromRgb(0x8A, 0x53, 0x0B));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Shows a "no records" placeholder when a report's row count is zero.</summary>
public class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Blank instead of "0" in empty numeric boxes, so forms do not look pre-filled.</summary>
/// <summary>
/// An enum name as English: "FullDay" reads "Full day".
///
/// Enum members cannot have spaces, and a combo box showing "FullDay" beside
/// "Morning" and "Evening" looks like a bug to the person reading it.
/// </summary>
public class EnumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var name = value?.ToString() ?? "";
        if (name.Length == 0) return "";

        var words = System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", " $1");
        return string.Concat(words[0], words[1..].ToLowerInvariant());
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public class BlankIfZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            decimal d when d == 0 => "",
            int i when i == 0 => "",
            _ => value?.ToString() ?? ""
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return targetType == typeof(int) ? 0 : 0m;

        if (targetType == typeof(int))
            return int.TryParse(text, out var i) ? i : 0;

        return decimal.TryParse(text, out var d) ? d : 0m;
    }
}
