using System.Windows;
using System.Windows.Controls;

namespace Pharma.App;

/// <summary>
/// A field label that carries the asterisk, so "this one is not optional" is
/// said once in a template rather than typed as a literal " *" on thirty
/// screens where the colour and spacing would drift apart.
///
/// Only for fields something actually refuses to save without — an asterisk on
/// a field that saves fine empty teaches people to ignore all of them.
/// </summary>
public class RequiredLabel : Control
{
    static RequiredLabel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RequiredLabel), new FrameworkPropertyMetadata(typeof(RequiredLabel)));

        // A TextBlock is not focusable; a Control is, and this is one. Left at
        // the default, every required-field label sat in the tab order, so
        // tabbing from Batch no to Expiry stopped twice on the way and the
        // second press appeared to do nothing.
        //
        // Both are needed: Focusable keeps it out of the tab order, IsTabStop
        // keeps it out even if something re-enables focus for accessibility.
        FocusableProperty.OverrideMetadata(
            typeof(RequiredLabel), new FrameworkPropertyMetadata(false));

        IsTabStopProperty.OverrideMetadata(
            typeof(RequiredLabel), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(RequiredLabel),
            new PropertyMetadata(""));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
