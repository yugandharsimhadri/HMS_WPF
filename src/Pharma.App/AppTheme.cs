using System.Windows;

namespace Pharma.App;

/// <summary>
/// Swaps the palette between light and dark.
///
/// The two dictionaries hold the same keys, and everything that shows a colour
/// asks for them by DynamicResource, so replacing the dictionary repaints the
/// running application — no restart, and no screen left half in the old theme.
/// </summary>
public static class AppTheme
{
    private const string Light = "Theme.Light.xaml";
    private const string Dark = "Theme.Dark.xaml";

    /// <summary>What is showing now.</summary>
    public static Pharma.Core.AppThemeKind Current { get; private set; } = Pharma.Core.AppThemeKind.Light;

    public static void Apply(Pharma.Core.AppThemeKind theme)
    {
        var dictionaries = Application.Current?.Resources.MergedDictionaries;
        if (dictionaries is null) return;

        var wanted = new Uri(theme == Pharma.Core.AppThemeKind.Dark ? Dark : Light, UriKind.Relative);

        // The palette is always first, so styles merged after it can rely on it.
        var existing = dictionaries.FirstOrDefault(
            d => d.Source is { } s && (s.OriginalString.EndsWith(Light, StringComparison.OrdinalIgnoreCase)
                                    || s.OriginalString.EndsWith(Dark, StringComparison.OrdinalIgnoreCase)));

        var replacement = new ResourceDictionary { Source = wanted };

        if (existing is null) dictionaries.Insert(0, replacement);
        else dictionaries[dictionaries.IndexOf(existing)] = replacement;

        Current = theme;
        AppLog.Info($"Theme applied: {theme}.");
    }
}
