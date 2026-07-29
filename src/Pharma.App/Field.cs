using System.Windows;

namespace Pharma.App;

/// <summary>
/// Marks an input the screen has just refused to save without.
///
/// The rule itself lives in the service that enforces it — this only carries the
/// answer back to the box the operator has to look at. Set it when a save is
/// turned away, clear it as soon as something is typed, so the red never
/// outlives the problem.
/// </summary>
public static class Field
{
    public static readonly DependencyProperty IsMissingProperty =
        DependencyProperty.RegisterAttached(
            "IsMissing", typeof(bool), typeof(Field),
            new FrameworkPropertyMetadata(false));

    public static void SetIsMissing(DependencyObject element, bool value)
        => element.SetValue(IsMissingProperty, value);

    public static bool GetIsMissing(DependencyObject element)
        => (bool)element.GetValue(IsMissingProperty);
}
