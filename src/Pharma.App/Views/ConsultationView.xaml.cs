using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// The consultation, shown as a layer over the shell rather than as its own
/// window. A separate window can end up behind another application, leaving the
/// doctor clicking a main window that cannot answer because a dialog they
/// cannot see owns the input.
/// </summary>
public partial class ConsultationView : UserControl
{
    public ConsultationView() => InitializeComponent();
}
