using System.Windows;

namespace Pharma.App.Views;

/// <summary>
/// The application's own message box.
///
/// Windows' MessageBox takes its colours from the operating system and knows
/// nothing about the palette, so in the dark theme it opened as a light grey
/// box in the middle of a near-black screen. This is the same conversation in
/// the application's own clothes.
/// </summary>
public partial class MessageWindow : Window
{
    public MessageWindow(string message, string title,
                         MessageBoxButton buttons, MessageBoxImage icon)
    {
        InitializeComponent();

        Title = title;
        MessageText.Text = message;

        ShowGlyph(icon);
        ShowButtons(buttons);
    }

    /// <summary>What the operator answered. Closing the window counts as backing out.</summary>
    public MessageBoxResult Answer { get; private set; } = MessageBoxResult.None;

    /// <summary>
    /// A single letter in a coloured disc rather than the Windows icons, which
    /// are bitmaps drawn for a grey dialog and look pasted on against either
    /// palette.
    /// </summary>
    private void ShowGlyph(MessageBoxImage icon)
    {
        var (mark, brush) = icon switch
        {
            MessageBoxImage.Error => ("!", "Danger"),
            MessageBoxImage.Warning => ("!", "Warn"),
            MessageBoxImage.Question => ("?", "Accent"),
            MessageBoxImage.Information => ("i", "Accent"),
            _ => ("", "")
        };

        if (brush.Length == 0)
        {
            Glyph.Visibility = Visibility.Collapsed;
            return;
        }

        GlyphMark.Text = mark;

        // Border.BackgroundProperty, not the Background this Window inherits from
        // Control — they are different dependency properties and setting the
        // wrong one silently does nothing.
        Glyph.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, brush);
    }

    /// <summary>
    /// Only the buttons the question needs. Enter takes the affirmative one and
    /// Esc backs out, so a dialog can always be dismissed from the keyboard —
    /// there is no close button on a message box.
    /// </summary>
    private void ShowButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                Reveal(OkButton, isDefault: true);
                Reveal(CancelButton, isCancel: true);
                break;

            case MessageBoxButton.YesNo:
                Reveal(YesButton, isDefault: true);
                // No stands in for Esc: there is nothing else to back out to.
                Reveal(NoButton, isCancel: true);
                break;

            case MessageBoxButton.YesNoCancel:
                Reveal(YesButton, isDefault: true);
                Reveal(NoButton);
                Reveal(CancelButton, isCancel: true);
                break;

            default:
                Reveal(OkButton, isDefault: true, isCancel: true);
                break;
        }
    }

    private static void Reveal(System.Windows.Controls.Button button,
                               bool isDefault = false, bool isCancel = false)
    {
        button.Visibility = Visibility.Visible;
        button.IsDefault = isDefault;
        button.IsCancel = isCancel;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Answered(MessageBoxResult.OK);
    private void Yes_Click(object sender, RoutedEventArgs e) => Answered(MessageBoxResult.Yes);
    private void No_Click(object sender, RoutedEventArgs e) => Answered(MessageBoxResult.No);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Answered(MessageBoxResult.Cancel);

    private void Answered(MessageBoxResult answer)
    {
        Answer = answer;
        Close();
    }
}
