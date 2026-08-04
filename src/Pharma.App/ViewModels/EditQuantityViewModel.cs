using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;

namespace Pharma.App.ViewModels;

/// <summary>
/// Changing the quantity on a bill line at the counter — a small popup
/// instead of typing straight into the grid cell, so the operator sees the
/// new amount before it takes effect rather than after. What actually
/// happens to stock once confirmed (re-taking it, possibly spanning a
/// second batch) is unchanged — see <see cref="SaleViewModel.EditQuantityAsync"/>,
/// which sets <see cref="SaleRow.Quantity"/> from the confirmed value and
/// lets the same reallocation path <c>OnLineChanged</c> already drives run
/// exactly as it did for the old inline edit.
/// </summary>
public partial class EditQuantityViewModel : ObservableObject
{
    private readonly SaleRow _row;

    public EditQuantityViewModel(SaleRow row)
    {
        _row = row;

        ProductName = row.ProductName;
        BatchNo = row.BatchNo;
        Available = row.Available;

        _quantity = row.Quantity;
        UpdatePreview();
    }

    public string ProductName { get; }
    public string BatchNo { get; }
    public int Available { get; }

    /// <summary>Raised once the operator confirms a valid quantity.</summary>
    public event Action<int>? Confirmed;

    [ObservableProperty] private int _quantity;
    [ObservableProperty] private bool _quantityMissing;
    [ObservableProperty] private string _packsPreview = "";
    [ObservableProperty] private string _amountPreview = "";

    partial void OnQuantityChanged(int value)
    {
        if (value > 0) QuantityMissing = false;
        UpdatePreview();
    }

    /// <summary>The same math <see cref="SaleRow.Amount"/> uses, recomputed
    /// live as the operator types — so the price shown here is exactly what
    /// the bill will carry, not an estimate.</summary>
    private void UpdatePreview()
    {
        if (Quantity <= 0)
        {
            PacksPreview = "";
            AmountPreview = "";
            return;
        }

        PacksPreview = PackMath.Describe(Quantity, _row.UnitsPerPack, _row.PackLabel, _row.UnitName);

        var amount = GstCalculator.Line(_row.Mrp, _row.UnitsPerPack, Quantity, _row.DiscountPercent, _row.GstRate).Net;
        AmountPreview = $"₹{amount:0.00}";
    }

    [RelayCommand]
    private void Confirm()
    {
        if (Quantity <= 0)
        {
            QuantityMissing = true;
            return;
        }

        Confirmed?.Invoke(Quantity);
    }
}
