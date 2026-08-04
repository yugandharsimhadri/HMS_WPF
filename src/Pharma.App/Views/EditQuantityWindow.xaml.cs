using System.Windows;
using Pharma.App.ViewModels;

namespace Pharma.App.Views;

/// <summary>
/// Changing the quantity on a bill line at the counter — small and modal,
/// the same weight as <see cref="QuickStockWindow"/>, so it does not
/// interrupt the counter's own flow any more than adding stock does.
/// </summary>
public partial class EditQuantityWindow : Window
{
    /// <summary>True once a valid quantity was confirmed, so the caller
    /// knows whether to apply <see cref="NewQuantity"/> or leave the line alone.</summary>
    public bool Confirmed { get; private set; }

    public int NewQuantity { get; private set; }

    public EditQuantityWindow(SaleRow row)
    {
        InitializeComponent();

        var vm = new EditQuantityViewModel(row);
        vm.Confirmed += quantity =>
        {
            Confirmed = true;
            NewQuantity = quantity;
            Close();
        };

        DataContext = vm;
        Loaded += (_, _) =>
        {
            QuantityBox.Focus();
            QuantityBox.SelectAll();
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
