using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// The test picker: every active test, searchable, added straight to the
/// bill on the <see cref="DiagnosticsViewModel"/> screen behind it as each
/// one is picked. Deliberately stays open across several picks — a bill is
/// rarely just one test, so closing it is its own "Done" rather than
/// something that happens the moment the first test lands.
/// </summary>
public partial class DiagnosticTestPickerViewModel(DiagnosticsService diagnostics, DiagnosticsViewModel billing)
    : ObservableObject
{
    /// <summary>Who this popup is adding tests for, so it reads correctly
    /// even though the bill itself is off-screen behind it.</summary>
    public string PatientName => billing.SelectedPatient?.Name ?? "";

    public ObservableCollection<DiagnosticTest> Tests { get; } = [];
    [ObservableProperty] private string _search = "";

    public event Action? RequestClose;

    /// <summary>Populated before the popup is shown, so it opens already
    /// listing every active test rather than an empty box waiting to be
    /// typed into.</summary>
    public async Task LoadAsync() => await FindAsync();

    partial void OnSearchChanged(string value) => FindAsync().Forget("Searching tests");

    /// <summary>
    /// Already-added tests are left out of the results entirely rather than
    /// shown with a disabled button — a test that has been billed twice
    /// belongs to a quantity of 2 on that one line, adjusted on the bill
    /// grid, not to a second line at the same price from a second click here.
    /// </summary>
    [RelayCommand]
    private async Task FindAsync()
    {
        Tests.Clear();
        var billed = billing.Lines.Select(l => l.TestId).ToHashSet();

        foreach (var t in await diagnostics.SearchTestsAsync(Search, activeOnly: true))
            if (!billed.Contains(t.Id))
                Tests.Add(t);
    }

    /// <summary>Read straight off the bill behind this popup rather than
    /// kept as its own count, so it can never disagree with what is
    /// actually on the bill once this closes.</summary>
    public int AddedCount => billing.Lines.Count;
    public decimal AddedTotal => billing.FinalAmount;

    [RelayCommand]
    private void AddTest(DiagnosticTest? test)
    {
        if (test is null) return;
        if (!billing.AddTestLine(test)) return;

        // Leaves the list rather than staying with a spent "Add" button —
        // the row disappearing is the confirmation that it landed on the bill.
        Tests.Remove(test);
        OnPropertyChanged(nameof(AddedCount));
        OnPropertyChanged(nameof(AddedTotal));
    }

    [RelayCommand]
    private void Done() => RequestClose?.Invoke();
}
