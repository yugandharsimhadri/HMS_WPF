using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Pharma.Core;
using Pharma.Data;

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

/// <summary>The disclosure arrow on a collapsible nav group header — down
/// while expanded, right while collapsed, the same convention as a
/// standard tree/accordion control.</summary>
public class ExpandArrowConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▾" : "▸";

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

/// <summary>
/// The closed box of a ComboBox's custom template (Theme.xaml) has no
/// built-in awareness of <c>DisplayMemberPath</c> — only the auto-generated
/// dropdown ComboBoxItems get that for free from the framework. A combo
/// bound with <c>DisplayMemberPath="Name"</c> (rather than an
/// <c>ItemTemplate</c>) used to show the selected item's own
/// <c>ToString()</c> in the closed box instead — "Pharma.Core.Doctor" rather
/// than the doctor's name — even though the dropdown list itself showed
/// every name correctly. This resolves <c>DisplayMemberPath</c> by hand for
/// that case. A combo that uses <c>ItemTemplate</c> instead is untouched —
/// with no <c>DisplayMemberPath</c> set, this passes the raw item straight
/// through for <c>ContentTemplate</c> to render, exactly as before.
/// </summary>
public class SelectionBoxDisplayConverter : IMultiValueConverter
{
    public object? Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var selectedItem = values.Length > 0 ? values[0] : null;
        var displayMemberPath = values.Length > 1 ? values[1] as string : null;

        if (selectedItem is null || string.IsNullOrEmpty(displayMemberPath)) return selectedItem;

        return selectedItem.GetType().GetProperty(displayMemberPath)?.GetValue(selectedItem) ?? selectedItem;
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
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

/// <summary>The dental case status pill's fill — Planned/InProgress reads
/// like an open/active badge, Completed like a settled one, Cancelled like
/// a danger band.</summary>
public class DentalCaseStatusFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DentalCaseStatus.Cancelled => "DangerFill",
            DentalCaseStatus.Completed => "BadgeFill",
            _ => "AccentSoft"
        };
        return Application.Current.TryFindResource(key) ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>The dental case status pill's text/ink colour, paired with
/// <see cref="DentalCaseStatusFillConverter"/>.</summary>
public class DentalCaseStatusInkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            DentalCaseStatus.Cancelled => "DangerInk",
            DentalCaseStatus.Completed => "InkMuted",
            _ => "AccentDark"
        };
        return Application.Current.TryFindResource(key) ?? Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>The pill wording on one Immunization tab row.</summary>
public class ImmunizationStatusLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ImmunizationStatus.Given => "Given",
            ImmunizationStatus.Overdue => "Overdue",
            ImmunizationStatus.DueSoon => "Due soon",
            ImmunizationStatus.Upcoming => "Upcoming",
            _ => ""
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>The pill's fill — reuses the same semantic brushes a status
/// badge already carries elsewhere in this app (Given reads like a paid
/// fee badge, Overdue like a danger band, Due soon like a warning band).</summary>
public class ImmunizationStatusFillConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ImmunizationStatus.Given => "AccentSoft",
            ImmunizationStatus.Overdue => "DangerFill",
            ImmunizationStatus.DueSoon => "WarnFill",
            _ => "BadgeFill"
        };
        return Application.Current.TryFindResource(key) ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>The pill's text/ink colour, paired with <see cref="ImmunizationStatusFillConverter"/>.</summary>
public class ImmunizationStatusInkConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ImmunizationStatus.Given => "AccentDark",
            ImmunizationStatus.Overdue => "DangerInk",
            ImmunizationStatus.DueSoon => "WarnInk",
            _ => "InkMuted"
        };
        return Application.Current.TryFindResource(key) ?? Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>"6 weeks", "9 months", "5 years" — a human age from a raw day
/// count, the same way every Vaccine Master row already stores its
/// recommended age. Not exact calendar math (a month is treated as 30.4368
/// days, a year as 365.25) — close enough for a checklist label.</summary>
public class ImmunizationAgeLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int days) return "";

        if (days == 0) return "At birth";
        if (days < 14) return $"{days} day{(days == 1 ? "" : "s")}";
        if (days < 84)
        {
            var weeks = Math.Max(1, (int)Math.Round(days / 7.0));
            return $"{weeks} week{(weeks == 1 ? "" : "s")}";
        }
        if (days < 730)
        {
            var months = Math.Max(1, (int)Math.Round(days / 30.4368));
            return $"{months} month{(months == 1 ? "" : "s")}";
        }
        var years = Math.Max(1, (int)Math.Round(days / 365.25));
        return $"{years} year{(years == 1 ? "" : "s")}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Whether a row's Record button should show at all — only once
/// there is actually something left to give.</summary>
public class ImmunizationNeedsActionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ImmunizationStatus.Overdue or ImmunizationStatus.DueSoon or ImmunizationStatus.Upcoming
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// The line under a vaccine's name — what was actually given (date, brand,
/// site) once given, or a plain due/overdue/upcoming line otherwise. Reads
/// the whole row rather than exposing this formatting on the data-layer
/// type, which has no business knowing how a UI wants doses phrased.
/// </summary>
public class ImmunizationRowDetailConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ImmunizationCardRow row) return "";

        if (row.Given is { } given)
        {
            var brand = given.ProductName is { } product
                ? (given.Manufacturer is { } maker ? $" · {product} ({maker})" : $" · {product}")
                : "";
            var site = string.IsNullOrWhiteSpace(given.SiteOfInjection) ? "" : $" · {given.SiteOfInjection}";
            return $"{given.GivenOn:dd MMM yyyy}{brand}{site}";
        }

        return row.Status switch
        {
            ImmunizationStatus.Overdue when row.RecommendedOn is { } d =>
                $"Was due {d:dd MMM yyyy} · overdue {(DateTime.Today - d).Days} day{((DateTime.Today - d).Days == 1 ? "" : "s")}",
            ImmunizationStatus.DueSoon when row.RecommendedOn is { } d =>
                $"Due {d:dd MMM yyyy} · in {(d - DateTime.Today).Days} day{((d - DateTime.Today).Days == 1 ? "" : "s")}",
            ImmunizationStatus.Upcoming when row.RecommendedOn is { } d => $"Due {d:dd MMM yyyy}",
            _ => "Not yet recorded"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
