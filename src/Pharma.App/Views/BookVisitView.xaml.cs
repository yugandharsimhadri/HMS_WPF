using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// Booking a visit, shown over the shell. It was a 330px panel beside the queue
/// that had to be scrolled to reach Book visit — which is the one step in the
/// form that must never be hard to find.
/// </summary>
public partial class BookVisitView : UserControl
{
    public BookVisitView() => InitializeComponent();
}
