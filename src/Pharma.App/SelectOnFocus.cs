using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pharma.App;

/// <summary>
/// Tabbing into a box selects what is already in it, so typing replaces the
/// value instead of adding to it.
///
/// Without this, a quantity box holding "0" and tabbed into puts the caret at
/// one end, and typing 5 gives "05" or "50" depending on which end. Both parse
/// as a number, so nothing complains — the shop just receives fifty packs when
/// it meant five. Every numeric field on every screen had the same trap.
///
/// Registered once as a class handler rather than as a behaviour attached to
/// each box: there are around eighty text boxes in this application and the one
/// somebody forgets is the one that takes the wrong number.
/// </summary>
public static class SelectOnFocus
{
    public static void Register()
    {
        // Keyboard focus covers Tab, Shift+Tab and anything that calls Focus().
        EventManager.RegisterClassHandler(
            typeof(TextBox), UIElement.GotKeyboardFocusEvent,
            new RoutedEventHandler(SelectAll), handledEventsToo: true);

        // A click into an unfocused box would otherwise place the caret and
        // undo the selection, so the first click selects and later clicks
        // behave normally — which is what people expect of an address bar.
        EventManager.RegisterClassHandler(
            typeof(TextBox), UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(FirstClickSelects), handledEventsToo: true);
    }

    private static void SelectAll(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { IsReadOnly: false } box) box.SelectAll();
    }

    private static void FirstClickSelects(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox { IsReadOnly: false } box) return;
        if (box.IsKeyboardFocusWithin) return;   // already in it: let the click place the caret

        box.Focus();

        // Handled, or the click that follows the focus change would collapse
        // the selection the focus handler just made.
        e.Handled = true;
    }
}
