using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// One delivery line, shown over the shell. The medicine was chosen on the page
/// behind, so this asks only what the delivery note says — and asks it in the
/// middle of the screen rather than in a column the operator has to scroll.
/// </summary>
public partial class ReceiveStockView : UserControl
{
    public ReceiveStockView() => InitializeComponent();
}
