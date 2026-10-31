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
        AgeMonths = existing.AgeMonths > 0 ? existing.AgeMonths.ToString() : "";
        DateOfBirth = existing.DateOfBirth;
        BloodGroup = existing.BloodGroup ?? "";
        GuardianName = existing.GuardianName ?? "";
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

    /// <summary>The months part, 0-11. Blank means none, not unknown.</summary>
    [ObservableProperty] private string _ageMonths = "";
    [ObservableProperty] private DateTime? _dateOfBirth;
    [ObservableProperty] private string _bloodGroup = "";
    [ObservableProperty] private string _guardianName = "";
    [ObservableProperty] private Gender _gender = Gender.Male;
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _allergies = "";
    [ObservableProperty] private string _status = "";

    public string[] BloodGroups { get; } = ["", "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];

    /// <summary>A DOB on file drives Age rather than the other way round, so the
    /// box is locked while one is set — nothing to reconcile between the two.</summary>
    public bool AgeIsEditable => DateOfBirth is null;

    /// <summary>
    /// Years and months as typed, with a date of birth overriding both — it is
    /// the more precise of the two, and re-typing an age every birthday is the
    /// reason ages on a register drift.
    ///
    /// Months are clamped to 0-11: anything above that is a miskey, and twelve
    /// months silently meaning a year would make the same child two different
    /// ages depending on how the desk chose to type it.
    /// </summary>
    private (int Years, int Months) AgeEntered()
    {
        if (DateOfBirth is { } dob) return Patient.AgeAndMonthsFromDob(dob);

        var years = int.TryParse(Age, out var y) ? Math.Max(0, y) : 0;
        var months = int.TryParse(AgeMonths, out var m) ? Math.Clamp(m, 0, 11) : 0;
        return (years, months);
    }

    /// <summary>
    /// Guardian's name only means anything for a minor — and most of all for a
    /// baby. This used to require years above zero, which excluded every infant
    /// under one: exactly the patient whose guardian the clinic most needs on
    /// file. Asked on months, so a seven-month-old qualifies.
    /// </summary>
    public bool ShowGuardian
    {
        get
        {
            var (years, months) = AgeEntered();
            return years * 12 + months is > 0 and < 18 * 12;
        }
    }

    /// <summary>Set when a save was turned away for want of a name; cleared the
    /// moment one is typed, so the field is never left red once it is right.</summary>
    [ObservableProperty] private bool _nameMissing;

    /// <summary>Set when a save was turned away for want of either a date of
    /// birth or an age — one of the two is required.</summary>
    [ObservableProperty] private bool _dobOrAgeMissing;

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    partial void OnAgeChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) DobOrAgeMissing = false;
        OnPropertyChanged(nameof(ShowGuardian));
    }

    partial void OnAgeMonthsChanged(string value)
    {
        // Months never satisfy the age requirement on their own - they only
        // refine the years. The requirement is a date of birth, or years.
        OnPropertyChanged(nameof(ShowGuardian));
    }

    /// <summary>A date of birth always wins over a typed age — it is the more
    /// precise of the two, and re-typing an age every birthday is the reason
    /// ages on a register drift.</summary>
    partial void OnDateOfBirthChanged(DateTime? value)
    {
        if (value is { } dob)
        {
            var (years, months) = Patient.AgeAndMonthsFromDob(dob);
            Age = years.ToString();
            AgeMonths = months.ToString();
        }

        OnPropertyChanged(nameof(AgeIsEditable));
        OnPropertyChanged(nameof(ShowGuardian));
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

        // Either a date of birth, or the age in years. Months are optional on
        // top of the years - blank or 0 both mean "no extra months" - and are
        // never enough alone, so a baby is entered as 0 years and some months.
        if (DateOfBirth is null && string.IsNullOrWhiteSpace(Age))
        {
            DobOrAgeMissing = true;
            Warn(string.IsNullOrWhiteSpace(AgeMonths)
                ? "Provide a date of birth, or the age in years."
                : "Enter the years as well - 0 for a baby under one. Months are optional.");
            return;
        }

        DobOrAgeMissing = false;

        var (age, months) = AgeEntered();

        var patient = _existing ?? new Patient();
        patient.Name = Name.Trim();
        patient.Phone = Phone.Trim();
        patient.Age = age;
        patient.AgeMonths = months;
        patient.DateOfBirth = DateOfBirth;
        patient.BloodGroup = Empty(BloodGroup);
        patient.GuardianName = age < 18 ? Empty(GuardianName) : null;
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
