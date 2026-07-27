using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One finding, with a tick so the operator chooses what to put right.</summary>
public partial class HealthRow(HealthFinding finding) : ObservableObject
{
    public HealthFinding Finding { get; } = finding;

    [ObservableProperty] private bool _selected = finding.CanRepairAutomatically;

    public string Medicine => Finding.ProductName;
    public string Problem => Describe(Finding.Problem);
    public string Now => Finding.Current;
    public string After => Finding.Proposed;
    public string Explanation => Finding.Explanation;

    /// <summary>"59 → 885 tablets", or blank when nothing on the shelf moves.</summary>
    public string StockChange => Finding.ChangesStock
        ? $"{Finding.QuantityBefore} → {Finding.QuantityAfter}"
        : "";

    public bool CanFix => Finding.CanRepairAutomatically;
    public bool IsDuplicate => Finding.Problem == HealthProblem.Duplicate;

    private static string Describe(HealthProblem problem) => problem switch
    {
        HealthProblem.PackSizeDisagrees => "Pack size disagrees",
        HealthProblem.BatchPackDisagrees => "Old stock at a different pack size",
        HealthProblem.UnitNotSet => "Sold-as not set",
        HealthProblem.Duplicate => "Duplicate — fix by hand",
        _ => problem.ToString()
    };
}

/// <summary>
/// The data health check. Finds every medicine whose record cannot be right and
/// repairs them together, because fixing them one at a time is not a fix when a
/// shop has two hundred.
/// </summary>
public partial class DataHealthViewModel(DataHealthService health) : ObservableObject
{
    public ObservableCollection<HealthRow> Findings { get; } = [];

    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;

    public async Task LoadAsync() => await ScanAsync();

    [RelayCommand]
    private async Task ScanAsync()
    {
        Busy = true;

        await Safely.RunAsync(async () =>
        {
            Findings.Clear();
            foreach (var f in await health.ScanAsync()) Findings.Add(new HealthRow(f));

            var fixable = Findings.Count(f => f.CanFix);
            var byHand = Findings.Count - fixable;

            Summary = Findings.Count == 0
                ? "Nothing to put right. Every medicine agrees with its own pack size."
                : $"{Findings.Count} thing(s) to look at — {fixable} can be put right here" +
                  (byHand > 0 ? $", {byHand} need doing by hand." : ".");

            Status = "";
        }, "Checking the data", m => Status = m);

        Busy = false;
    }

    /// <summary>
    /// Folds a duplicate into the record that holds the most stock. Asked first,
    /// because it moves batches and history and cannot be undone from here.
    /// </summary>
    [RelayCommand]
    private async Task MergeAsync(HealthRow? row)
    {
        if (row is null || row.Finding.Problem != HealthProblem.Duplicate) return;

        var answer = System.Windows.MessageBox.Show(
            $"Fold this copy of {row.Medicine} into the one it duplicates?\n\n" +
            $"Its batches, purchases, sales and prescriptions all move across, and " +
            $"the empty record is retired.\n\nThis cannot be undone from here.",
            "Merge duplicate", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (answer != System.Windows.MessageBoxResult.Yes) return;

        Busy = true;

        await Safely.RunAsync(async () =>
        {
            var survivorId = await health.SurvivorForAsync(row.Finding.ProductId)
                             ?? throw new InvalidOperationException(
                                 "The medicine this duplicates could not be found.");

            Status = await health.MergeAsync(survivorId, row.Finding.ProductId, Environment.UserName);
            await ScanAsync();
        }, "Merging the duplicate", m => Status = m);

        Busy = false;
    }

    [RelayCommand]
    private async Task RepairAsync()
    {
        var chosen = Findings.Where(f => f.Selected && f.CanFix).Select(f => f.Finding).ToList();

        if (chosen.Count == 0)
        {
            Status = "Nothing is ticked.";
            return;
        }

        Busy = true;

        await Safely.RunAsync(async () =>
        {
            var repaired = await health.RepairAsync(chosen, Environment.UserName);

            // Re-scan first: it clears the status, and the operator needs to be
            // told what happened, not what was happening before.
            await ScanAsync();

            Status = $"{repaired} put right. Stock that was re-counted is recorded " +
                     $"under Inventory → Recent corrections.";
        }, "Repairing the data", m => Status = m);

        Busy = false;
    }
}
