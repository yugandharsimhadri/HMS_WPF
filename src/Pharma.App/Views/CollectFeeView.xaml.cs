using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// Taking the consultation fee, shown over the shell. It exists because a
/// receipt is numbered and dated the moment it is written, so the amount and
/// the payment mode have to be seen before the press, not after.
/// </summary>
public partial class CollectFeeView : UserControl
{
    public CollectFeeView() => InitializeComponent();
}
