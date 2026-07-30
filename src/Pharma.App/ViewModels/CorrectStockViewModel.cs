using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;
using System.Windows;

namespace Pharma.App.ViewModels;

/// <summary>
/// Putting a shelf count right, over the shell.
///
/// Stock otherwise only moves by receiving or selling, and both leave a
/// document. A correction has none, so it writes one — otherwise a shortfall is
/// indistinguishable from theft and nobody can answer what happened. That is
/// why the reason is asked for rather than assumed.
/// </summary>
public partial class CorrectStockViewModel : ObservableObject
{
    private readonly PharmacyService _pharmacy;
    private readonly Product _product;

    public CorrectStockViewModel(PharmacyService pharmacy, Product product)
    {
        _pharmacy = pharmacy;
        _product = product;
    }

    public string Header => $"Correct the count — {_product.Name}";

    public ObservableCollection<Batch> Batches { get; } = [];

    public Array Reasons => Enum.GetValues<AdjustmentReason>();

    public event Action? RequestClose;

    /// <summary>What the page behind should say. Null when nothing was corrected.</summary>
    public string? Outcome { get; private set; }

    [ObservableProperty] private Batch? _selectedBatch;
    [ObservableProperty] private int _correctedQuantity;
    [ObservableProperty] private AdjustmentReason _reason = AdjustmentReason.Recount;
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// Starts at what the system currently believes, so the operator changes a
    /// number rather than typing one from nothing — and so a correction of zero
    /// is a deliberate act rather than an empty box submitted by accident.
    /// </summary>
    partial void OnSelectedBatchChanged(Batch? value) => CorrectedQuantity = value?.QtyOnHand ?? 0;

    public async Task LoadAsync()
    {
        Batches.Clear();
        foreach (var b in await _pharmacy.GetSellableBatchesAsync(_product.Id)) Batches.Add(b);

        // One batch is the common case, and making them pick it from a list of
        // one is a click that teaches nothing.
        if (Batches.Count == 1) SelectedBatch = Batches[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        AppLog.Trace(
            $"Inventory.CorrectStock batch={SelectedBatch?.Id} to={CorrectedQuantity} " +
            $"reason={Reason} notes='{Notes}'");

        if (SelectedBatch is null)
        {
            Status = "Choose the batch whose count is wrong.";
            Dialog.Show(Status, "Inventory", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await Safely.RunAsync(async () =>
        {
            var adjustment = await _pharmacy.AdjustStockAsync(
                SelectedBatch.Id, CorrectedQuantity, Reason, Notes);

            Outcome = $"{adjustment.ProductName} batch {adjustment.BatchNo}: " +
                      $"{adjustment.QuantityBefore} → {adjustment.QuantityAfter} ({adjustment.Reason}).";

            RequestClose?.Invoke();
        }, "Correcting the stock count", m => Status = m);
    }

    /// <summary>Closes without correcting anything.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
