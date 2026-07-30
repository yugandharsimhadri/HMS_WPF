using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// One patient, shown over the shell rather than in a column beside the
/// register. The column was always full of whoever was last selected, which is
/// how the next child got saved on top of them.
/// </summary>
public partial class PatientEditorView : UserControl
{
    public PatientEditorView() => InitializeComponent();
}
