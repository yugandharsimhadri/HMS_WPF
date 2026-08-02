using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Pharma.Core;
using Pharma.Core.Licensing;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Settings, in six destinations rather than one scrolling screen: General
/// (how the software behaves), Clinic (who the doctor's documents speak as),
/// Pharmacy (who the bill speaks as), Doctors, Reports (how every document is
/// branded — logo and footer), and Features (which optional modules, like
/// Diagnostics, are switched on).
///
/// Still one view model and one screen, with an internal tab strip rather
/// than six sidebar entries — the sidebar is at seven items already, and
/// every one of these screens is a handful of fields, not a destination
/// someone comes back to over and over the way OPD or the pharmacy counter
/// are. See docs/SETTINGS_REORG.md for the reasoning.
/// </summary>
public partial class SettingsViewModel(
    SettingsService settings, OpdService opd, ILicenseService licence) : ObservableObject, IPage
{
    public string Title => "Settings";
    public string Subtitle => "General, clinic, pharmacy, doctors, document branding and features";

    [ObservableProperty] private string _status = "";

    public async Task LoadAsync()
    {
        var general = await settings.GetGeneralAsync();
        QueueLayout = general.QueueLayout;
        Theme = general.Theme;
        DiagnosticsEnabled = general.DiagnosticsEnabled;

        var clinic = await settings.GetClinicAsync();
        ClinicName = clinic.Name;
        ClinicAddress = clinic.AddressLine;
        ClinicAddress2 = clinic.AddressLine2;
        ClinicPhone = clinic.Phone;
        ClinicGstRegistered = clinic.GstRegistered;
        ClinicGstin = clinic.Gstin;
        ClinicFooter = clinic.FooterText;
        MorningFrom = clinic.MorningFrom.ToString(@"hh\:mm");
        MorningTo = clinic.MorningTo.ToString(@"hh\:mm");
        EveningFrom = clinic.EveningFrom.ToString(@"hh\:mm");
        EveningTo = clinic.EveningTo.ToString(@"hh\:mm");

        var pharmacy = await settings.GetPharmacyAsync();
        PharmacyName = pharmacy.Name;
        PharmacyAddress = pharmacy.AddressLine;
        PharmacyAddress2 = pharmacy.AddressLine2;
        PharmacyPhone = pharmacy.Phone;
        PharmacyGstRegistered = pharmacy.GstRegistered;
        PharmacyGstin = pharmacy.Gstin;
        DrugLicenceNo = pharmacy.DrugLicenceNo;
        PharmacistName = pharmacy.PharmacistName;
        PharmacyFooter = pharmacy.FooterText;

        var theme = await settings.GetDocumentThemeAsync();
        DocumentFooter = theme.Footer;
        _logoBase64 = theme.LogoBase64;
        _logoContentType = theme.LogoContentType;
        RefreshLogoPreview();

        RefreshLastBackup();

        await LoadDoctorsAsync();
    }

    // ── General ────────────────────────────────────────────────────────────

    [ObservableProperty] private QueueLayout _queueLayout = QueueLayout.Tiles;

    /// <summary>Applied the moment it is picked, so the choice can be seen.</summary>
    [ObservableProperty] private Pharma.Core.AppThemeKind _theme = Pharma.Core.AppThemeKind.Light;

    public Array Themes => Enum.GetValues<Pharma.Core.AppThemeKind>();
    public Array QueueLayouts => Enum.GetValues<QueueLayout>();

    partial void OnThemeChanged(Pharma.Core.AppThemeKind value) => AppTheme.Apply(value);

    [RelayCommand]
    private async Task SaveGeneralAsync()
    {
        await settings.SaveGeneralAsync(new GeneralSettings
        {
            QueueLayout = QueueLayout, Theme = Theme, DiagnosticsEnabled = DiagnosticsEnabled
        });
        Status = $"Saved. The OPD queue will use {QueueLayout.ToString().ToLowerInvariant()}, " +
                 $"in the {Theme.ToString().ToLowerInvariant()} theme.";
    }

    // ── Features ───────────────────────────────────────────────────────────

    /// <summary>
    /// Off by default. Every optional module — Diagnostics today, more later —
    /// gets one row here rather than its own settings screen, so turning one on
    /// is never more than a checkbox and a save.
    /// </summary>
    [ObservableProperty] private bool _diagnosticsEnabled;

    [RelayCommand]
    private async Task SaveFeaturesAsync()
    {
        await settings.SaveGeneralAsync(new GeneralSettings
        {
            QueueLayout = QueueLayout, Theme = Theme, DiagnosticsEnabled = DiagnosticsEnabled
        });

        // The shell reads this once at startup; poke it directly so the nav
        // button appears or disappears immediately rather than after a restart.
        App.Services.GetRequiredService<MainViewModel>().DiagnosticsEnabled = DiagnosticsEnabled;

        Status = DiagnosticsEnabled
            ? "Saved. The Diagnostics module is now available from the sidebar."
            : "Saved. The Diagnostics module is hidden from the sidebar.";
    }

    /// <summary>One line about the licence, so the state is visible without opening the dialog.</summary>
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

    [RelayCommand]
    private void ShowAbout()
    {
        var about = new Views.AboutWindow { Owner = Application.Current?.MainWindow };
        about.ShowDialog();

        // The remaining days move while the application is open.
        OnPropertyChanged(nameof(LicenceSummary));
    }

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

    [RelayCommand]
    private void OpenBackupFolder()
    {
        Safely.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DbBootstrapper.BackupDirectory,
            UseShellExecute = true
        }), "Opening the backup folder", m => Status = m);
    }

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

    /// <summary>
    /// Opens the data health check — the one place that finds medicines whose
    /// pack size and units-per-pack disagree, and repairs them together.
    /// </summary>
    [RelayCommand]
    private void CheckDataHealth()
    {
        var window = new Views.DataHealthWindow { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    // ── Clinic ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _clinicName = "";
    [ObservableProperty] private string _clinicAddress = "";
    [ObservableProperty] private string _clinicAddress2 = "";
    [ObservableProperty] private string _clinicPhone = "";
    [ObservableProperty] private bool _clinicGstRegistered;
    [ObservableProperty] private string _clinicGstin = "";

    /// <summary>
    /// Printed at the foot of the prescription and the fee receipt. Left
    /// blank, the shared Reports footer keeps printing instead — see
    /// ClinicProfile.FooterText.
    /// </summary>
    [ObservableProperty] private string _clinicFooter = "";

    public string ClinicGstHint => ClinicGstRegistered
        ? "The prescription and the fee receipt print your GSTIN."
        : "No GSTIN is printed on the prescription or the fee receipt.";

    partial void OnClinicGstRegisteredChanged(bool value) => OnPropertyChanged(nameof(ClinicGstHint));

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

    [RelayCommand]
    private async Task SaveClinicAsync()
    {
        // Read back first, so a session time that will not parse can fall back
        // to what is already stored rather than to a hard-coded default.
        var saved = await settings.GetClinicAsync();

        await settings.SaveClinicAsync(new ClinicProfile
        {
            Name = ClinicName.Trim(),
            AddressLine = ClinicAddress.Trim(),
            AddressLine2 = ClinicAddress2.Trim(),
            Phone = ClinicPhone.Trim(),
            GstRegistered = ClinicGstRegistered,
            Gstin = ClinicGstin.Trim(),
            FooterText = ClinicFooter.Trim(),

            // A time that will not parse keeps the one already saved. The OPD
            // screen filters on these, and a bad window there hides the queue.
            MorningFrom = Session(MorningFrom, saved.MorningFrom),
            MorningTo = Session(MorningTo, saved.MorningTo),
            EveningFrom = Session(EveningFrom, saved.EveningFrom),
            EveningTo = Session(EveningTo, saved.EveningTo)
        });

        // The window title bar and the sidebar tile read this live — no
        // restart to see a renamed clinic reflected there.
        App.Services.GetRequiredService<MainViewModel>().ClinicDisplayName = ClinicName.Trim();

        Status = "Clinic details saved.";
    }

    /// <summary>A typed session time, or what was already saved if it is not one.</summary>
    private static TimeSpan Session(string typed, TimeSpan fallback)
        => TimeSpan.TryParse(typed, out var parsed) ? parsed : fallback;

    // ── Pharmacy ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _pharmacyName = "";
    [ObservableProperty] private string _pharmacyAddress = "";
    [ObservableProperty] private string _pharmacyAddress2 = "";
    [ObservableProperty] private string _pharmacyPhone = "";
    [ObservableProperty] private bool _pharmacyGstRegistered;
    [ObservableProperty] private string _pharmacyGstin = "";
    [ObservableProperty] private string _drugLicenceNo = "";
    [ObservableProperty] private string _pharmacistName = "";

    /// <summary>
    /// Printed at the foot of the medicine bill. Left blank, the shared
    /// Reports footer keeps printing instead — see PharmacyProfile.FooterText.
    /// </summary>
    [ObservableProperty] private string _pharmacyFooter = "";

    /// <summary>Spells out what the switch does to a printed bill.</summary>
    public string PharmacyGstHint => PharmacyGstRegistered
        ? "Bills print as TAX INVOICE with GSTIN, GST columns and a GST summary."
        : "No GST is charged. Bills print as INVOICE with no GSTIN and no GST.";

    partial void OnPharmacyGstRegisteredChanged(bool value) => OnPropertyChanged(nameof(PharmacyGstHint));

    [RelayCommand]
    private async Task SavePharmacyAsync()
    {
        await settings.SavePharmacyAsync(new PharmacyProfile
        {
            Name = PharmacyName.Trim(),
            AddressLine = PharmacyAddress.Trim(),
            AddressLine2 = PharmacyAddress2.Trim(),
            Phone = PharmacyPhone.Trim(),
            GstRegistered = PharmacyGstRegistered,
            Gstin = PharmacyGstin.Trim(),
            DrugLicenceNo = DrugLicenceNo.Trim(),
            PharmacistName = PharmacistName.Trim(),
            FooterText = PharmacyFooter.Trim()
        });

        Status = "Pharmacy details saved.";
    }

    // ── Doctors ────────────────────────────────────────────────────────────

    public ObservableCollection<Doctor> Doctors { get; } = [];

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
    [ObservableProperty] private string _doctorPhone = "";
    [ObservableProperty] private decimal _consultationFee;

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
        DoctorPhone = value.Phone ?? "";
        ConsultationFee = value.ConsultationFee;
    }

    [RelayCommand]
    private void NewDoctor()
    {
        SelectedDoctor = null;
        DoctorName = RegistrationNo = Speciality = DoctorPhone = "";
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
        doctor.Phone = string.IsNullOrWhiteSpace(DoctorPhone) ? null : DoctorPhone.Trim();
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

    // ── Reports: document branding ────────────────────────────────────────

    [ObservableProperty] private string _documentFooter = "";
    [ObservableProperty] private BitmapImage? _logoPreview;

    private string? _logoBase64;
    private string? _logoContentType;

    public bool HasLogo => _logoBase64 is not null;

    [RelayCommand]
    private void UploadLogo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a logo",
            Filter = "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
        };
        if (dialog.ShowDialog() != true) return;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(dialog.FileName);
        }
        catch (Exception ex)
        {
            Warn($"Could not read that file. {ex.Message}");
            return;
        }

        // Capped well under what a Settings row can actually hold, so the
        // database and every day's backup stay a reasonable size. A logo
        // sized correctly for the header column below never comes close to
        // this — it is a safety net against a phone photo, not the real
        // guidance, which is the pixel check that follows.
        const int maxBytes = 1 * 1024 * 1024;
        if (bytes.Length > maxBytes)
        {
            Warn($"That image is {bytes.Length / 1024.0 / 1024.0:0.#} MB. Choose one under 1 MB.");
            return;
        }

        var contentType = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null
        };

        if (contentType is null)
        {
            Warn("Choose a PNG or JPEG image.");
            return;
        }

        if (!TryDecodeDimensions(bytes, out var width, out var height))
        {
            Warn("That file could not be read as an image.");
            return;
        }

        // The header column is a landscape box, DocumentBuilder.LogoColumnMaxWidth
        // (190 DIU / 1.98in) by LogoColumnMaxHeight (50 DIU / 0.52in) — wide
        // and short, not square, so width and height are checked against
        // separate floors and ceilings rather than one shared number.
        if (width < MinLogoWidth || height < MinLogoHeight)
        {
            Warn($"That image is {width}x{height}px, too small to print clearly. " +
                 $"Use one at least {MinLogoWidth}x{MinLogoHeight}px.");
            return;
        }

        if (width > MaxLogoWidth || height > MaxLogoHeight)
        {
            Warn($"That image is {width}x{height}px — larger than the header needs. " +
                 $"Use one no bigger than {MaxLogoWidth}x{MaxLogoHeight}px.");
            return;
        }

        _logoBase64 = Convert.ToBase64String(bytes);
        _logoContentType = contentType;
        RefreshLogoPreview();

        // Not a hard rule — a tall or square mark still prints, just with
        // empty space left and right inside the wide column — so this is a
        // nudge, not a second gate the upload has already passed.
        var aspect = (double)width / height;
        Status = aspect < 2.0
            ? $"Logo loaded ({width}x{height}px). It prints best as a wide mark — " +
              $"this will letterbox left and right in the header column. Click Save to keep it."
            : "Logo loaded. Click Save to keep it.";
    }

    /// <summary>
    /// The smallest width a logo may be, in source pixels.
    ///
    /// <see cref="Printing.DocumentBuilder.LogoColumnMaxWidth"/> is 190
    /// device-independent units — 1.98 inch. A clinic laser printer commonly
    /// runs 300-600 DPI, which asks for 594-1188 physical pixels across that
    /// span to print crisp rather than upscaled and soft. 700 sits inside
    /// that range at roughly the same relative position the old square
    /// slot's 150px floor sat inside its own 112-225px range: clean at the
    /// low end, a barely-there upscale toward the high end.
    /// </summary>
    private const int MinLogoWidth = 700;

    /// <summary>
    /// The smallest height a logo may be, in source pixels — the same trade
    /// as <see cref="MinLogoWidth"/>, against
    /// <see cref="Printing.DocumentBuilder.LogoColumnMaxHeight"/>'s 50 DIU
    /// (0.52 inch, 156-313px across the 300-600 DPI range).
    /// </summary>
    private const int MinLogoHeight = 200;

    /// <summary>The largest width a logo may be — the header column gets
    /// nothing from more than this, and the Settings row only grows for no
    /// reason. Some headroom past the 600 DPI ceiling (1188px) rather than a
    /// hard cutoff right at it.</summary>
    private const int MaxLogoWidth = 1300;

    /// <summary>The largest height a logo may be — same reasoning as
    /// <see cref="MaxLogoWidth"/>, against the 600 DPI ceiling for
    /// <see cref="Printing.DocumentBuilder.LogoColumnMaxHeight"/> (313px).</summary>
    private const int MaxLogoHeight = 350;

    private static bool TryDecodeDimensions(byte[] bytes, out int width, out int height)
    {
        width = height = 0;

        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.None);
            var frame = decoder.Frames[0];

            width = frame.PixelWidth;
            height = frame.PixelHeight;
            return width > 0 && height > 0;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Uploaded logo could not be decoded to check its size ({ex.Message}).");
            return false;
        }
    }

    /// <summary>
    /// Clears the logo. The header goes back to text only — there is no
    /// compiled default to fall back to any more, since the compact header
    /// this feeds has no room for the old full-width banner. See
    /// DocumentBuilder.LogoElement.
    /// </summary>
    [RelayCommand]
    private void RemoveLogo()
    {
        _logoBase64 = null;
        _logoContentType = null;
        LogoPreview = null;
        OnPropertyChanged(nameof(HasLogo));

        Status = "Logo removed. Click Save to keep it off. Documents print with text only.";
    }

    private void RefreshLogoPreview()
    {
        OnPropertyChanged(nameof(HasLogo));

        if (_logoBase64 is null)
        {
            LogoPreview = null;
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(_logoBase64);
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            LogoPreview = image;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Saved logo could not be previewed ({ex.Message}).");
            LogoPreview = null;
        }
    }

    [RelayCommand]
    private async Task SaveDocumentThemeAsync()
    {
        await settings.SaveDocumentThemeAsync(new DocumentTheme
        {
            Footer = DocumentFooter.Trim(),
            LogoBase64 = _logoBase64,
            LogoContentType = _logoContentType
        });

        Status = "Document branding saved. It applies to every prescription, receipt and bill from now on.";
    }

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Reports", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
