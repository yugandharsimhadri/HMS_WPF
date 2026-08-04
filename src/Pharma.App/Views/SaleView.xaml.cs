using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Pharma.App.ViewModels;

namespace Pharma.App.Views;

public partial class SaleView : UserControl
{
    public SaleView() => InitializeComponent();

    /// <summary>
    /// Opens the edit-quantity popup for whichever bill line was clicked —
    /// anywhere on the row, not one specific cell. A click that lands on the
    /// row's own remove button is left alone: that button already has its
    /// own action, and firing both from the one click would be confusing.
    /// </summary>
    private void SaleLinesGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not SaleViewModel vm) return;
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not SaleRow line) return;

        vm.EditQuantityCommand.Execute(line);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null && current is not T)
            current = VisualTreeHelper.GetParent(current);

        return current as T;
    }
}
