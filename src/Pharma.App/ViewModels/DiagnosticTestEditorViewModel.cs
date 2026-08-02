using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// One diagnostic test, added or edited over the shell — the same "fresh view
/// model per open" shape as <see cref="PatientEditorViewModel"/> and
/// <see cref="MedicineEditorViewModel"/>, so Test Master gets a popup instead
/// of a permanent side panel, matching how the Medicines screen already adds
/// and edits a product.
/// </summary>
public partial class DiagnosticTestEditorViewModel : ObservableObject
{
    private readonly DiagnosticsService _diagnostics;

    /// <summary>The record being edited, or null when this is a new test.</summary>
    private readonly DiagnosticTest? _existing;

    public DiagnosticTestEditorViewModel(
        DiagnosticsService diagnostics, IEnumerable<string> categories, DiagnosticTest? existing = null)
    {
        _diagnostics = diagnostics;
        _existing = existing;

        foreach (var c in categories) Categories.Add(c);

        if (existing is null) return;

        Name = existing.Name;
        Category = existing.Category;
        Price = existing.Price;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New test" : _existing.Name;

    /// <summary>Deleting is only offered for a test already saved — and only
    /// succeeds if it has never been billed; see DiagnosticsService.DeleteTestAsync.</summary>
    public bool CanDelete => _existing is not null;

    public ObservableCollection<string> Categories { get; } = [];

    public event Action? RequestClose;

    /// <summary>What Test Master should say. Null when nothing was written.</summary>
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";

    /// <summary>Set when a save was turned away for want of a name; cleared the
    /// moment one is typed, so the field is never left red once it is right.</summary>
    [ObservableProperty] private bool _nameMissing;

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NameMissing = true;
            Warn("Test name is required.");
            return;
        }

        NameMissing = false;

        var test = _existing ?? new DiagnosticTest();
        test.Name = Name.Trim();
        test.Category = string.IsNullOrWhiteSpace(Category) ? "Others" : Category.Trim();
        test.Price = Price;
        test.Active = Active;

        try
        {
            await _diagnostics.SaveTestAsync(test);

            Outcome = $"{test.Name} saved.";
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_existing is null) return;

        var confirm = Dialog.Show(
            $"Delete {_existing.Name}?", "Test Master", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _diagnostics.DeleteTestAsync(_existing.Id);
            Outcome = $"{_existing.Name} deleted.";
            RequestClose?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            Warn(ex.Message);
        }
    }

    /// <summary>Closes without writing anything. Nothing typed has been saved.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Test Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
