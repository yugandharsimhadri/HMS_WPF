using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// Putting a shelf count right, shown over the shell. Separate from receiving
/// because they are opposite acts: one records stock arriving, the other admits
/// the records were wrong.
/// </summary>
public partial class CorrectStockView : UserControl
{
    public CorrectStockView() => InitializeComponent();
}
