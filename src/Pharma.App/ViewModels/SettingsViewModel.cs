using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Core.Licensing;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// The clinic's own identity (printed on every bill) and the doctor list.
///
/// The type behind it is still <c>ShopProfile</c> and the columns are still
/// named for a shop. That is a schema already live at a customer, and renaming
/// it would earn a migration for no benefit anyone can see — the words on
/// screen are what people read, and those say clinic / pharmacy.
/// </summary>
public partial class SettingsViewModel(
    SettingsService settings, OpdService opd, ILicenseService licence) : ObservableObject, IPage
{
    public string Title => "Settings";
    public string Subtitle => "Clinic / Pharmacy details printed on bills and prescriptions";

    /// <summary>One line about the licence, so the state is visible without
    /// opening the dialog.</summary>
    public string LicenceSummary
    {
        get
        {
            var info = licence.GetLicenseInfo();

            if (info.IsClockTampered) return "The system clock needs attention.";
            if (info.IsExpired) return $"{info.Edition} — expired.";

            return $"{info.Edition}, licensed to {info.CustomerName} · " +
                   $"{info.DaysRemaining:N0} day(s) remaining.";
        }
    }

    /// <summary>Opens the About dialog.</summary>
    [RelayCommand]
    private void ShowAbout()
    {
        var about = new Views.AboutWindow { Owner = Application.Current?.MainWindow };
        about.ShowDialog();

        // The remaining days move while the application is open.
        OnPropertyChanged(nameof(LicenceSummary));
    }

    public ObservableCollection<Doctor> Doctors { get; } = [];

    [ObservableProperty] private string _shopName = "";
    [ObservableProperty] private string _addressLine = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private bool _gstRegistered;
    [ObservableProperty] private string _gstin = "";

    /// <summary>Spells out what the switch does to a printed bill.</summary>
    public string GstHint => GstRegistered
        ? "Bills print as TAX INVOICE with GSTIN, GST columns and a GST summary."
        : "No GST is charged. Bills print as INVOICE with no GSTIN and no GST.";

    partial void OnGstRegisteredChanged(bool value) => OnPropertyChanged(nameof(GstHint));
    [ObservableProperty] private string _drugLicenceNo = "";
    [ObservableProperty] private string _pharmacistName = "";
    [ObservableProperty] private string _billFooter = "";
    [ObservableProperty] private QueueLayout _queueLayout = QueueLayout.Tiles;

    /// <summary>Applied the moment it is picked, so the choice can be seen.</summary>
    [ObservableProperty] private Pharma.Core.AppThemeKind _theme = Pharma.Core.AppThemeKind.Light;

    public Array Themes => Enum.GetValues<Pharma.Core.AppThemeKind>();

    partial void OnThemeChanged(Pharma.Core.AppThemeKind value) => AppTheme.Apply(value);

    public Array QueueLayouts => Enum.GetValues<QueueLayout>();

    // Held as text, because they are typed. Anything unreadable falls back to
    // the current default rather than being refused — an unparseable window
    // would otherwise hide the whole OPD queue, which is a far worse outcome
    // than a session that quietly runs the usual hours.
    [ObservableProperty] private string _morningFrom = "10:00";
    [ObservableProperty] private string _morningTo = "13:00";
    [ObservableProperty] private string _eveningFrom = "16:00";
    [ObservableProperty] private string _eveningTo = "20:00";

    /// <summary>
    /// Reads the four boxes back as sentences, so a typo is visible before it
    /// is saved rather than the next time somebody wonders where the queue went.
    /// </summary>
    public string SessionHint
    {
        get
        {
            var bad = new[]
            {
                ("Morning from", MorningFrom), ("Morning to", MorningTo),
                ("Evening from", EveningFrom), ("Evening to", EveningTo)
            }.Where(f => !TimeSpan.TryParse(f.Item2, out _)).Select(f => f.Item1).ToList();

            if (bad.Count > 0)
                return $"{string.Join(", ", bad)} — not a time. Use the 24-hour clock, like 16:30.";

            var morning = TimeSpan.Parse(MorningFrom);
            var morningEnd = TimeSpan.Parse(MorningTo);
            var evening = TimeSpan.Parse(EveningFrom);
            var eveningEnd = TimeSpan.Parse(EveningTo);

            if (morningEnd <= morning || eveningEnd <= evening)
                return "A sitting has to end after it starts.";

            return $"The OPD screen can show the morning sitting ({MorningFrom}–{MorningTo}), " +
                   $"the evening one ({EveningFrom}–{EveningTo}), or the full day. " +
                   $"A visit booked outside both is only on the full day.";
        }
    }

    partial void OnMorningFromChanged(string value) => OnPropertyChanged(nameof(SessionHint));
    partial void OnMorningToChanged(string value) => OnPropertyChanged(nameof(SessionHint));
    partial void OnEveningFromChanged(string value) => OnPropertyChanged(nameof(SessionHint));
    partial void OnEveningToChanged(string value) => OnPropertyChanged(nameof(SessionHint));
    [ObservableProperty] private string _databasePath = DbBootstrapper.DatabasePath;
    [ObservableProperty] private string _logPath = AppLog.CurrentFile;
    [ObservableProperty] private string _backupPath = DbBootstrapper.BackupDirectory;
    [ObservableProperty] private string _lastBackup = "";

    /// <summary>
    /// When the database was last copied, in words. A clinic that cannot see
    /// this has no idea whether it is one day or one year since anything was
    /// safe, which is the same as having no backups.
    /// </summary>
    private void RefreshLastBackup()
    {
        var last = DbBootstrapper.LastBackup;

        if (last is null)
        {
            LastBackup = "Never backed up. Take one now.";
            return;
        }

        var age = (int)(DateTime.Now - last.LastWriteTime).TotalDays;

        var when = age switch
        {
            <= 0 => "today",
            1 => "yesterday",
            _ => $"{age} days ago"
        };

        LastBackup = $"Last backup {when} — {last.LastWriteTime:dd MMM yyyy HH:mm} " +
                     $"({last.Length / 1024} KB)" +
                     (age >= 7 ? "  ⚠ that is a while ago." : "");
    }

    /// <summary>Copies the database now, whatever was taken automatically today.</summary>
    [RelayCommand]
    private async Task BackUpNowAsync()
    {
        await Safely.RunAsync(async () =>
        {
            var file = await Task.Run(DbBootstrapper.BackupNow);

            RefreshLastBackup();
            Status = $"Backed up to {file.FullName}";
        }, "Backing up the database", m => Status = m);
    }

    /// <summary>Opens the backup folder, so a copy can be put on a pen drive.</summary>
    [RelayCommand]
    private void OpenBackupFolder()
    {
        Safely.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DbBootstrapper.BackupDirectory,
            UseShellExecute = true
        }), "Opening the backup folder", m => Status = m);
    }

    // Doctor form
    [ObservableProperty] private Doctor? _selectedDoctor;
    [ObservableProperty] private string _doctorName = "";

    /// <summary>Set when a doctor was saved without a name.</summary>
    [ObservableProperty] private bool _doctorNameMissing;

    partial void OnDoctorNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) DoctorNameMissing = false;
    }
    [ObservableProperty] private string _registrationNo = "";
    [ObservableProperty] private string _speciality = "";
    [ObservableProperty] private decimal _consultationFee;

    [ObservableProperty] private string _status = "";

    public async Task LoadAsync()
    {
        var profile = await settings.GetAsync();
        ShopName = profile.Name;
        AddressLine = profile.AddressLine;
        Phone = profile.Phone;
        GstRegistered = profile.GstRegistered;
        Gstin = profile.Gstin;
        DrugLicenceNo = profile.DrugLicenceNo;
        PharmacistName = profile.PharmacistName;
        BillFooter = profile.BillFooter;
        QueueLayout = profile.QueueLayout;
        Theme = profile.Theme;

        MorningFrom = profile.MorningFrom.ToString(@"hh\:mm");
        MorningTo = profile.MorningTo.ToString(@"hh\:mm");
        EveningFrom = profile.EveningFrom.ToString(@"hh\:mm");
        EveningTo = profile.EveningTo.ToString(@"hh\:mm");

        RefreshLastBackup();

        await LoadDoctorsAsync();
    }

    private async Task LoadDoctorsAsync()
    {
        Doctors.Clear();
        foreach (var d in await opd.GetDoctorsAsync()) Doctors.Add(d);
    }

    partial void OnSelectedDoctorChanged(Doctor? value)
    {
        if (value is null) return;
        DoctorName = value.Name;
        RegistrationNo = value.RegistrationNo ?? "";
        Speciality = value.Speciality ?? "";
        ConsultationFee = value.ConsultationFee;
    }

    /// <summary>
    /// Opens the data health check — the one place that finds medicines whose
    /// pack size and units-per-pack disagree, and repairs them together.
    /// </summary>
    [RelayCommand]
    private void CheckDataHealth()
    {
        var window = new Views.DataHealthWindow { Owner = System.Windows.Application.Current.MainWindow };
        window.ShowDialog();
    }

    /// <summary>
    /// Empties the shop form. What is already saved stays saved — this is for
    /// starting the entry again, not for wiping the clinic's details.
    /// </summary>
    [RelayCommand]
    private void ClearShop()
    {
        ShopName = AddressLine = Phone = Gstin = "";
        DrugLicenceNo = PharmacistName = BillFooter = "";
        GstRegistered = false;

        Status = "Form cleared. Nothing was changed until you save.";
    }

    [RelayCommand]
    private async Task SaveShopAsync()
    {
        // Read back first, so a session time that will not parse can fall back
        // to what is already stored rather than to a hard-coded default.
        var saved = await settings.GetAsync();

        await settings.SaveAsync(new ShopProfile
        {
            Name = ShopName.Trim(),
            AddressLine = AddressLine.Trim(),
            Phone = Phone.Trim(),
            GstRegistered = GstRegistered,
            Gstin = Gstin.Trim(),
            DrugLicenceNo = DrugLicenceNo.Trim(),
            PharmacistName = PharmacistName.Trim(),
            BillFooter = BillFooter.Trim(),
            QueueLayout = QueueLayout,
            Theme = Theme,

            // A time that will not parse keeps the one already saved. The OPD
            // screen filters on these, and a bad window there hides the queue.
            MorningFrom = Session(MorningFrom, saved.MorningFrom),
            MorningTo = Session(MorningTo, saved.MorningTo),
            EveningFrom = Session(EveningFrom, saved.EveningFrom),
            EveningTo = Session(EveningTo, saved.EveningTo)
        });

        Status = $"Saved. The OPD queue will use {QueueLayout.ToString().ToLowerInvariant()}, " +
                 $"in the {Theme.ToString().ToLowerInvariant()} theme.";
    }

    /// <summary>A typed session time, or what was already saved if it is not one.</summary>
    private static TimeSpan Session(string typed, TimeSpan fallback)
        => TimeSpan.TryParse(typed, out var parsed) ? parsed : fallback;

    /// <summary>Opens the log folder so a problem can be reported with the file attached.</summary>
    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppLog.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not open the log folder.", ex);
            Dialog.Show(AppLog.LogDirectory, "Log folder", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void NewDoctor()
    {
        SelectedDoctor = null;
        DoctorName = RegistrationNo = Speciality = "";
        ConsultationFee = 0;
    }

    [RelayCommand]
    private async Task SaveDoctorAsync()
    {
        if (string.IsNullOrWhiteSpace(DoctorName))
        {
            DoctorNameMissing = true;
            Dialog.Show("Doctor name is required.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DoctorNameMissing = false;

        var doctor = SelectedDoctor ?? new Doctor();
        doctor.Name = DoctorName.Trim();
        doctor.RegistrationNo = string.IsNullOrWhiteSpace(RegistrationNo) ? null : RegistrationNo.Trim();
        doctor.Speciality = string.IsNullOrWhiteSpace(Speciality) ? null : Speciality.Trim();
        doctor.ConsultationFee = ConsultationFee;
        doctor.IsActive = true;

        await opd.SaveDoctorAsync(doctor);

        var saved = doctor.Name;
        await LoadDoctorsAsync();

        // Cleared like every other form on the way out. Left selected, the next
        // name typed over it saves as an edit to this doctor rather than adding
        // the new one.
        NewDoctor();

        Status = $"{saved} saved. The form is clear for the next doctor.";
    }
}
