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
