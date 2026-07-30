using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Stock: what is on the shelf, and the two things that can be done to it.
///
/// The page itself only finds a medicine and shows what is there. Receiving and
/// correcting each open over the shell, because both are jobs with a beginning
/// and an end — a delivery line, a recount — and neither belongs in a column
/// that is always half-filled with whatever was done last.
///
/// Split from the medicine catalogue because they are different jobs done by
/// different people at different times: the catalogue is set up once, stock
/// moves every delivery.
/// </summary>
public partial class InventoryViewModel(PharmacyService pharmacy) : ObservableObject, IPage
{
    public string Title => "Inventory";
    public string Subtitle => SelectedProduct is null
        ? $"{Products.Count} medicine(s) · pick one to receive or correct stock"
        : $"{SelectedProduct.Name} · {SelectedProduct.StockOnHand} " +
          $"{SelectedProduct.DispensingUnit.Name(SelectedProduct.StockOnHand)} on hand";

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<Batch> Batches { get; } = [];
    public ObservableCollection<StockAdjustment> Adjustments { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _status = "";

    [NotifyPropertyChangedFor(nameof(HasProduct))]
    [ObservableProperty] private Product? _selectedProduct;

    /// <summary>Drives both buttons: neither job means anything without a medicine.</summary>
    public bool HasProduct => SelectedProduct is not null;

    /// <summary>
    /// Says so when a medicine's pack size and its units-per-pack disagree.
    /// That combination sells whole strips to anyone asking for tablets and
    /// reports no error, so it has to be visible where stock is handled — on the
    /// page, before either popup is opened.
    /// </summary>
    [ObservableProperty] private string _packWarning = "";

    private void UpdatePackWarning()
    {
        PackWarning = "";
        if (SelectedProduct is not { } product) return;

        var stated = PackMath.UnitsFromPacking(product.PackSize);
        var perPack = Math.Max(1, product.UnitsPerPack);

        if (stated is { } n && n != perPack)
        {
            PackWarning =
                $"⚠ The pack size says {n} per pack but this medicine is set to {perPack}. " +
                $"The counter will sell whole packs to anyone asking for " +
                $"{product.DispensingUnit.Name(2)}. Fix it on the Medicines screen — " +
                $"set Units in one pack to {n} and save, and the stock already on the " +
                $"shelf is re-counted with it.";
            return;
        }

        // The medicine may be right while stock received earlier is not.
        var stale = Batches.Where(b => b.UnitsPerPack != perPack).ToList();

        if (stale.Count > 0)
            PackWarning =
                $"⚠ {stale.Count} batch(es) here were received at a different pack size. " +
                $"Open this medicine on the Medicines screen and save it to re-count them.";
    }

    public async Task LoadAsync()
    {
        await FindAsync();
        await LoadAdjustmentsAsync();
    }

    [RelayCommand]
    private async Task FindAsync()
    {
        var previous = SelectedProduct?.Id;

        Products.Clear();
        foreach (var p in await pharmacy.SearchProductsAsync(Search, 200)) Products.Add(p);

        SelectedProduct = Products.FirstOrDefault(p => p.Id == previous);
        OnPropertyChanged(nameof(Subtitle));
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(Subtitle));

        Batches.Clear();
        UpdatePackWarning();

        if (value is null) return;

        LoadBatchesAsync(value.Id).Forget("Loading batches");
    }

    private async Task LoadBatchesAsync(Guid productId)
    {
        Batches.Clear();
        foreach (var b in await pharmacy.GetSellableBatchesAsync(productId)) Batches.Add(b);

        UpdatePackWarning();
    }

    private async Task LoadAdjustmentsAsync()
    {
        Adjustments.Clear();
        foreach (var a in await pharmacy.GetAdjustmentsAsync(100)) Adjustments.Add(a);
    }

    /// <summary>
    /// Empties the search and the selection. There is no form left on this page
    /// to clear — receiving and correcting take their fields away with them.
    /// </summary>
    [RelayCommand]
    private async Task ClearAsync()
    {
        SelectedProduct = null;
        Search = "";
        await FindAsync();

        Status = "";
    }

    // ── The two jobs ───────────────────────────────────────────────────────

    /// <summary>
    /// One delivery line, over the shell. A fresh view model each time, so the
    /// supplier, rate and batch of the last delivery cannot follow this one —
    /// stock keyed against a stale medicine is stock counted onto the wrong
    /// shelf, and it looks exactly like stock counted right.
    /// </summary>
    [RelayCommand]
    private async Task ReceiveStockAsync()
    {
        if (SelectedProduct is not { } product)
        {
            Warn("Choose the medicine you are receiving.");
            return;
        }

        var receiving = new ReceiveStockViewModel(pharmacy, product);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        await shell.ShowOverlayAsync(receiving, close => receiving.RequestClose += () => close());

        // Backing out says nothing, so whatever the page said last stays.
        if (receiving.Outcome is not { } outcome) return;

        // Everything clears, the selection included. Receiving twice against a
        // medicine still sitting selected is how one delivery becomes two.
        SelectedProduct = null;
        Search = "";
        await FindAsync();

        Status = $"{outcome} The screen is clear for the next line.";
    }

    /// <summary>
    /// A recount, over the shell. Refuses to open with nothing on the shelf:
    /// there is no count to put right, and an empty batch list only invites a
    /// correction against whatever else was selected.
    /// </summary>
    [RelayCommand]
    private async Task CorrectStockAsync()
    {
        if (SelectedProduct is not { } product)
        {
            Warn("Choose the medicine whose count is wrong.");
            return;
        }

        if (Batches.Count == 0)
        {
            Warn($"{product.Name} has no stock on the shelf to correct. Receive some first.");
            return;
        }

        var correcting = new CorrectStockViewModel(pharmacy, product);
        var shell = App.Services.GetRequiredService<MainViewModel>();

        var showing = shell.ShowOverlayAsync(correcting, close => correcting.RequestClose += () => close());

        await correcting.LoadAsync();
        await showing;

        if (correcting.Outcome is not { } outcome) return;

        await LoadBatchesAsync(product.Id);
        await LoadAdjustmentsAsync();
        await FindAsync();

        Status = outcome;
    }

    [RelayCommand]
    private async Task ImportBillAsync()
    {
        await Safely.RunAsync(async () =>
        {
            var window = new Views.ImportWindow { Owner = Application.Current.MainWindow };
            window.ShowDialog();

            if (!window.Imported) return;

            await FindAsync();
            if (SelectedProduct is not null) await LoadBatchesAsync(SelectedProduct.Id);

            Status = "Stock imported. It was added to what was already on the shelf.";
        }, "Importing a supplier bill", m => Status = m);
    }

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
