using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Putting stock on the shelf from the counter, for a medicine that is
/// physically there but not in the system.
///
/// The supplier's file is not always usable and chasing a proper goods-inward
/// entry mid-queue is not realistic, so this asks for the least it can: how
/// many packs, and the MRP. Everything it fills in for itself is flagged, and
/// the entry is marked provisional so purchases and sales can be reconciled
/// later rather than never.
/// </summary>
public partial class QuickStockViewModel : ObservableObject
{
    private readonly PharmacyService _pharmacy;
    private readonly Product _product;

    public QuickStockViewModel(PharmacyService pharmacy, Product product)
    {
        _pharmacy = pharmacy;
        _product = product;

        Medicine = product.Name;
        OnHand = $"{product.StockOnHand} {product.DispensingUnit.Name(product.StockOnHand)} on hand";

        // The last price this medicine was received at is nearly always right.
        var latest = product.Batches
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.ReceivedOn)
            .FirstOrDefault();

        if (latest is not null)
        {
            Mrp = latest.Mrp;
            PurchaseRate = latest.PurchaseRate;
        }

        UpdatePreview();
    }

    public string Medicine { get; }
    public string OnHand { get; }

    /// <summary>Raised once stock has actually gone on the shelf.</summary>
    public event Action? Added;

    [ObservableProperty] private int _packs;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private decimal _purchaseRate;
    [ObservableProperty] private string _batchNo = "";
    [ObservableProperty] private DateTime _expiryDate = DateTime.Today.AddYears(2);
    [ObservableProperty] private string _preview = "";
    [ObservableProperty] private string _status = "";

    public string PackNote => _product.UnitsPerPack > 1
        ? $"One pack is {_product.UnitsPerPack} {_product.DispensingUnit.Name(2)}."
        : $"One pack is one {_product.DispensingUnit.Name(1)}.";

    partial void OnPacksChanged(int value) => UpdatePreview();

    private void UpdatePreview()
    {
        if (Packs <= 0)
        {
            Preview = "";
            return;
        }

        var perPack = Math.Max(1, _product.UnitsPerPack);
        var units = Packs * perPack;
        var unit = _product.DispensingUnit.Name(units);

        Preview = perPack > 1
            ? $"{Packs} pack(s) × {perPack} = {units} {unit} onto the shelf"
            : $"{units} {unit} onto the shelf";
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        await Safely.RunAsync(async () =>
        {
            var batch = await _pharmacy.QuickAddStockAsync(
                _product.Id, Packs, Mrp,
                BatchNo, ExpiryDate, PurchaseRate,
                Environment.UserName);

            Status = $"{batch.QtyOnHand} on the shelf as batch {batch.BatchNo}.";
            Added?.Invoke();
        }, "Adding stock at the counter", m => Status = m);
    }
}
