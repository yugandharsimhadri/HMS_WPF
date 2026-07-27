using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pharma.App;

/// <summary>
/// Keeps a minus sign, a letter or a stray second decimal point out of a box
/// that takes a number.
///
/// The services refuse negatives outright, but being told off after pressing
/// Save is a poor way to find out. This stops it being typed, and stops a
/// pasted "-50" arriving either.
///
///     &lt;TextBox local:NumericInput.Mode="Whole" /&gt;
///     &lt;TextBox local:NumericInput.Mode="Money" /&gt;
/// </summary>
public static partial class NumericInput
{
    public enum Kind
    {
        /// <summary>Not a numeric box.</summary>
        None,

        /// <summary>A count: 0, 1, 2 … Nothing after a decimal point.</summary>
        Whole,

        /// <summary>An amount: up to two decimal places.</summary>
        Money
    }

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode", typeof(Kind), typeof(NumericInput),
            new PropertyMetadata(Kind.None, OnModeChanged));

    public static void SetMode(DependencyObject element, Kind value) => element.SetValue(ModeProperty, value);
    public static Kind GetMode(DependencyObject element) => (Kind)element.GetValue(ModeProperty);

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;

        box.PreviewTextInput -= OnTyping;
        box.PreviewKeyDown -= OnKeyDown;
        DataObject.RemovePastingHandler(box, OnPaste);

        if ((Kind)e.NewValue == Kind.None) return;

        box.PreviewTextInput += OnTyping;
        box.PreviewKeyDown += OnKeyDown;
        DataObject.AddPastingHandler(box, OnPaste);
    }

    private static void OnTyping(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box) return;

        e.Handled = !Allows(box, Proposed(box, e.Text));
    }

    /// <summary>Space would slip past PreviewTextInput and break parsing.</summary>
    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) e.Handled = true;
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box) return;

        var pasted = e.DataObject.GetData(typeof(string)) as string ?? "";

        if (!Allows(box, Proposed(box, pasted))) e.CancelCommand();
    }

    /// <summary>What the box would hold if this text were accepted.</summary>
    private static string Proposed(TextBox box, string incoming)
    {
        var text = box.Text ?? "";
        var start = box.SelectionStart;
        var length = box.SelectionLength;

        return text.Remove(start, length).Insert(start, incoming);
    }

    private static bool Allows(TextBox box, string candidate)
    {
        if (candidate.Length == 0) return true;

        return GetMode(box) switch
        {
            Kind.Whole => Whole().IsMatch(candidate),
            Kind.Money => Money().IsMatch(candidate),
            _ => true
        };
    }

    // No sign, no exponent, no thousands separator — just digits.
    [GeneratedRegex(@"^\d*$")]
    private static partial Regex Whole();

    [GeneratedRegex(@"^\d*(\.\d{0,2})?$")]
    private static partial Regex Money();
}
