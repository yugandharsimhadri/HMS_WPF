using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;
using System.Windows;

namespace Pharma.App.ViewModels;

/// <summary>
/// One delivery line going onto the shelf, over the shell.
///
/// The medicine is chosen on the page behind and handed in here, so this asks
/// only what a delivery note actually says: which batch, when it expires, how
/// many packs, what was paid and what is printed on them.
///
/// A new instance per line. Nothing carries over to the next one — not the
/// supplier, not the rate, and above all not the medicine, because stock keyed
/// against whichever one happened to still be selected is stock counted onto
/// the wrong shelf.
/// </summary>
public partial class ReceiveStockViewModel : ObservableObject
{
    private readonly PharmacyService _pharmacy;
    private readonly Product _product;

    public ReceiveStockViewModel(PharmacyService pharmacy, Product product)
    {
        _pharmacy = pharmacy;
        _product = product;

        UpdateIntakePreview();
    }

    public string Header => $"Receive stock — {_product.Name}";

    /// <summary>What is there now, so the number about to be added has context.</summary>
    public string OnHand =>
        $"{_product.StockOnHand} {_product.DispensingUnit.Name(_product.StockOnHand)} on hand · " +
        (_product.UnitsPerPack > 1
            ? $"one pack is {_product.UnitsPerPack} {_product.DispensingUnit.Name(2)}"
            : $"sold as a single {_product.DispensingUnit.Name(1)}");

    public event Action? RequestClose;

    /// <summary>What the page behind should say. Null when nothing was received.</summary>
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _batchNo = "";
    [ObservableProperty] private DateTime _expiryDate = DateTime.Today.AddYears(2);
    [ObservableProperty] private int _packs;
    [ObservableProperty] private int _freePacks;
    [ObservableProperty] private decimal _purchaseRate;
    [ObservableProperty] private decimal _mrp;
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _supplierInvoiceNo = "";
    [ObservableProperty] private string _intakePreview = "";
    [ObservableProperty] private string _status = "";

    // Set when receiving was turned away for want of one of these, cleared the
    // moment the value is put right. Expiry is here too: a date in the past is
    // as much a stopper as a blank batch number, and the box is the only place
    // to say which of the fields the message was about.
    [ObservableProperty] private bool _batchNoMissing;
    [ObservableProperty] private bool _packsMissing;
    [ObservableProperty] private bool _mrpMissing;
    [ObservableProperty] private bool _expiryMissing;

    partial void OnBatchNoChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) BatchNoMissing = false;
    }

    partial void OnMrpChanged(decimal value)
    {
        if (value > 0) MrpMissing = false;
    }

    partial void OnExpiryDateChanged(DateTime value)
    {
        if (value.Date > DateTime.Today) ExpiryMissing = false;
    }

    // Either box satisfies "how many packs arrived", so both clear the mark.
    partial void OnPacksChanged(int value)
    {
        if (value > 0 || FreePacks > 0) PacksMissing = false;
        UpdateIntakePreview();
    }

    partial void OnFreePacksChanged(int value)
    {
        if (value > 0 || Packs > 0) PacksMissing = false;
        UpdateIntakePreview();
    }

    /// <summary>
    /// Spells out packs in, units out. "Qty" alone is the single most misread
    /// field in a pharmacy: the shop counts strips, the counter sells tablets.
    /// </summary>
    private void UpdateIntakePreview()
    {
        var total = Packs + FreePacks;

        if (total <= 0)
        {
            IntakePreview = "";
            return;
        }

        var perPack = Math.Max(1, _product.UnitsPerPack);
        var units = total * perPack;
        var unitName = _product.DispensingUnit.Name(units);

        IntakePreview = perPack > 1
            ? $"{total} pack(s) × {perPack} = {units} {unitName} onto the shelf"
            : $"{units} {unitName} onto the shelf";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        AppLog.Trace(
            $"Inventory.ReceiveStock product='{_product.Name}' id={_product.Id} " +
            $"batch='{BatchNo}' packs={Packs}+{FreePacks} rate={PurchaseRate} mrp={Mrp} exp={ExpiryDate:yyyy-MM-dd}");

        if (string.IsNullOrWhiteSpace(BatchNo))
        {
            BatchNoMissing = true;
            Warn("Batch number is printed on the pack and has to appear on the bill.");
            return;
        }

        if (Packs <= 0 && FreePacks <= 0)
        {
            PacksMissing = true;
            Warn("Enter how many packs arrived.");
            return;
        }

        if (Mrp <= 0)
        {
            MrpMissing = true;
            Warn("Enter the MRP printed on the pack — the counter prices from it.");
            return;
        }

        if (ExpiryDate.Date <= DateTime.Today)
        {
            ExpiryMissing = true;
            Warn("Expiry must be in the future.");
            return;
        }

        BatchNoMissing = PacksMissing = MrpMissing = ExpiryMissing = false;

        await Safely.RunAsync(async () =>
        {
            var entry = new StockEntry
            {
                EntryDate = DateTime.Today,
                SupplierName = Empty(SupplierName),
                SupplierInvoiceNo = Empty(SupplierInvoiceNo)
            };

            var item = new StockEntryItem
            {
                ProductId = _product.Id,
                BatchNo = BatchNo.Trim(),
                ExpiryDate = ExpiryDate,
                Quantity = Packs,
                FreeQuantity = FreePacks,
                UnitsPerPack = _product.UnitsPerPack,
                PurchaseRate = PurchaseRate,
                Mrp = Mrp
            };

            var saved = await _pharmacy.ReceiveStockAsync(entry, [item]);

            Outcome = $"{saved.EntryNo}: {item.UnitsReceived} " +
                      $"{_product.DispensingUnit.Name(item.UnitsReceived)} of {_product.Name} " +
                      $"added to batch {item.BatchNo}.";

            RequestClose?.Invoke();
        }, "Receiving stock", m => Status = m);
    }

    /// <summary>Closes without receiving anything. Nothing typed has been written.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
