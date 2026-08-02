using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// One patient, added or edited over the shell.
///
/// A new instance per open, holding one patient. That is what retired the worst
/// bug this screen had: the register and the form used to share a selection, so
/// typing the next child's details over a patient still loaded saved them on top
/// of that patient instead of adding anybody, and the first child left the
/// register without a trace.
/// </summary>
public partial class PatientEditorViewModel : ObservableObject
{
    private readonly OpdService _opd;

    /// <summary>The record being edited, or null when this is a new patient.</summary>
    private readonly Patient? _existing;

    public PatientEditorViewModel(OpdService opd, Patient? existing = null)
    {
        _opd = opd;
        _existing = existing;

        if (existing is null)
        {
            PatientNo = "(assigned on save)";
            return;
        }

        PatientNo = existing.PatientNo;
        Name = existing.Name;
        Phone = existing.Phone;
        Age = existing.Age > 0 ? existing.Age.ToString() : "";
        Gender = existing.Gender;
        Address = existing.Address ?? "";
        Allergies = existing.Allergies ?? "";
    }

    public string Header => _existing is null ? "New patient" : _existing.Name;

    /// <summary>Removing is only offered for somebody already on the register.</summary>
    public bool CanRemove => _existing is not null;

    public Array Genders => Enum.GetValues<Gender>();

    public event Action? RequestClose;

    /// <summary>What the register should say. Null when nothing was written.</summary>
    public string? Outcome { get; private set; }

    /// <summary>The patient that was actually saved, so a caller that opened
    /// this to add someone new — the Diagnostics billing screen, for one —
    /// can select them the moment this closes instead of asking the desk to
    /// search for the person they just typed in.</summary>
    public Patient? Saved { get; private set; }

    [ObservableProperty] private string _patientNo = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _age = "";
    [ObservableProperty] private Gender _gender = Gender.Male;
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _allergies = "";
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
            Warn("Patient name is required.");
            return;
        }

        NameMissing = false;

        int.TryParse(Age, out var age);

        var patient = _existing ?? new Patient();
        patient.Name = Name.Trim();
        patient.Phone = Phone.Trim();
        patient.Age = age;
        patient.Gender = Gender;
        patient.Address = Empty(Address);
        patient.Allergies = Empty(Allergies);

        try
        {
            var saved = await _opd.SavePatientAsync(patient);

            Saved = saved;
            Outcome = $"{saved.Name} saved as {saved.PatientNo}.";
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (_existing is null) return;

        var confirm = Dialog.Show(
            $"Remove {_existing.Name} from the register?",
            "Patients", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var error = await _opd.DeletePatientAsync(_existing.Id);
        if (error is not null)
        {
            Warn(error);
            return;
        }

        Outcome = $"{_existing.Name} removed from the register.";
        RequestClose?.Invoke();
    }

    /// <summary>Closes without writing anything. Nothing typed has been saved.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Patients", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
