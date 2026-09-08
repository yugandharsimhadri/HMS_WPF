using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One anesthesia type, added or edited over the shell — the
/// smallest of the master editors, two fields plus Active.</summary>
public partial class AnesthesiaTypeEditorViewModel : ObservableObject
{
    private readonly DentistService _dentist;
    private readonly AnesthesiaTypeMaster? _existing;

    public AnesthesiaTypeEditorViewModel(DentistService dentist, AnesthesiaTypeMaster? existing = null)
    {
        _dentist = dentist;
        _existing = existing;

        if (existing is null) return;

        Name = existing.Name;
        DefaultCost = existing.DefaultCost;
        Active = existing.Active;
    }

    public string Header => _existing is null ? "New anesthesia type" : _existing.Name;

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private decimal _defaultCost;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";
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
            Warn("Anesthesia type name is required.");
            return;
        }

        NameMissing = false;

        var type = _existing ?? new AnesthesiaTypeMaster();
        type.Name = Name.Trim();
        type.DefaultCost = DefaultCost;
        type.Active = Active;

        try
        {
            await _dentist.SaveAnesthesiaTypeAsync(type);
            Outcome = $"{type.Name} saved.";
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Anesthesia Types", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
