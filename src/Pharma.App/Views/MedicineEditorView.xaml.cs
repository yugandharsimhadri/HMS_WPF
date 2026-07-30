using System.Windows.Controls;

namespace Pharma.App.Views;

/// <summary>
/// Adding or editing a medicine, shown over the shell like the consultation
/// rather than squeezed into a column beside the catalogue. Fifteen fields need
/// the width, and on the screens this runs on there is only one screenful to
/// give them.
/// </summary>
public partial class MedicineEditorView : UserControl
{
    public MedicineEditorView() => InitializeComponent();
}
