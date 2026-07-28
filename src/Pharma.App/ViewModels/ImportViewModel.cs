using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Pharma.Core;
using Pharma.Data;
using Pharma.Data.Import;

namespace Pharma.App.ViewModels;

/// <summary>
/// Importing a supplier's bill: choose the profile and the file, look at what it
/// will do, then commit. Nothing is written until Import is clicked.
/// </summary>
public partial class ImportViewModel(
    PurchaseImportService import,
    IDbContextFactory<AppDbContext> factory) : ObservableObject
{
    public ObservableCollection<ImportProfile> Profiles { get; } = [];
    public ObservableCollection<ImportLine> Lines { get; } = [];
    public ObservableCollection<ImportIssue> Issues { get; } = [];

    [ObservableProperty] private ImportProfile? _profile;
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _status = "Choose the supplier's profile and their file.";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _canImport;
    [ObservableProperty] private bool _busy;

    private ImportPreview? _preview;

    public event Action? Imported;

    public async Task LoadAsync()
    {
        await using var db = await factory.CreateDbContextAsync();

        Profiles.Clear();
        foreach (var p in await db.ImportProfiles.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync())
            Profiles.Add(p);

        Profile ??= Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose the supplier's file",
            Filter = "Supplier files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        FilePath = dialog.FileName;
        PreviewCommand.Execute(null);
    }

    /// <summary>Reads the file and shows what importing it would do. Writes nothing.</summary>
    [RelayCommand]
    private async Task PreviewAsync()
    {
        Lines.Clear();
        Issues.Clear();
        CanImport = false;
        Summary = "";
        _preview = null;

        if (Profile is null)
        {
            Status = "Choose a profile first.";
            return;
        }

        if (!File.Exists(FilePath))
        {
            Status = "Choose the supplier's file.";
            return;
        }

        try
        {
            Busy = true;

            var bill = new VendorBillParser(Profile).Parse(FilePath);
            var preview = await import.PreviewAsync(bill, Profile, Path.GetFileName(FilePath));
            preview.SupplierName = string.IsNullOrWhiteSpace(SupplierName) ? null : SupplierName.Trim();

            _preview = preview;

            foreach (var line in preview.Lines) Lines.Add(line);
            foreach (var issue in preview.Issues.OrderByDescending(i => i.Severity)) Issues.Add(issue);

            CanImport = preview.CanImport;

            Summary = preview.Lines.Count == 0
                ? ""
                : $"Bill {bill.BillNo} dated {bill.BillDate:dd MMM yyyy} · {preview.Lines.Count} line(s) · " +
                  $"{preview.NewMedicines} new medicine(s) · {preview.TotalUnits} unit(s) · net ₹{bill.NetAmount:0.00}";

            Status = preview.BlockedReason
                     ?? (preview.NeedsChecking > 0
                         ? $"{preview.NeedsChecking} line(s) need checking before importing."
                         : "Ready to import. Stock will be added to what is already on the shelf.");
        }
        catch (Exception ex)
        {
            AppLog.Error($"Could not read {FilePath}.", ex);
            Status = $"Could not read that file: {ex.Message}";
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (_preview is null || !CanImport) return;

        try
        {
            Busy = true;

            _preview.SupplierName = string.IsNullOrWhiteSpace(SupplierName) ? null : SupplierName.Trim();
            var result = await import.CommitAsync(_preview);

            Status = $"Imported as {result.EntryNo}: {result.Lines} line(s), " +
                     $"{result.ProductsCreated} new medicine(s), {result.UnitsAdded} unit(s) added to stock.";

            CanImport = false;
            Imported?.Invoke();
        }
        catch (Exception ex)
        {
            AppLog.Error("Import failed.", ex);
            Dialog.Show(
                $"Nothing was imported.\n\n{ex.Message}",
                "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            Status = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
