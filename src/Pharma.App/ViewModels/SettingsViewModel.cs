using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>Shop identity (printed on every bill) and the doctor list.</summary>
public partial class SettingsViewModel(SettingsService settings, OpdService opd) : ObservableObject, IPage
{
    public string Title => "Settings";
    public string Subtitle => "Shop details printed on bills and prescriptions";

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

    public Array QueueLayouts => Enum.GetValues<QueueLayout>();
    [ObservableProperty] private string _databasePath = DbBootstrapper.DatabasePath;
    [ObservableProperty] private string _logPath = AppLog.CurrentFile;

    // Doctor form
    [ObservableProperty] private Doctor? _selectedDoctor;
    [ObservableProperty] private string _doctorName = "";
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

    [RelayCommand]
    private async Task SaveShopAsync()
    {
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
            QueueLayout = QueueLayout
        });

        Status = $"Saved. The OPD queue will use {QueueLayout.ToString().ToLowerInvariant()}.";
    }

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
            MessageBox.Show(AppLog.LogDirectory, "Log folder", MessageBoxButton.OK, MessageBoxImage.Information);
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
            MessageBox.Show("Doctor name is required.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var doctor = SelectedDoctor ?? new Doctor();
        doctor.Name = DoctorName.Trim();
        doctor.RegistrationNo = string.IsNullOrWhiteSpace(RegistrationNo) ? null : RegistrationNo.Trim();
        doctor.Speciality = string.IsNullOrWhiteSpace(Speciality) ? null : Speciality.Trim();
        doctor.ConsultationFee = ConsultationFee;
        doctor.IsActive = true;

        await opd.SaveDoctorAsync(doctor);
        Status = $"{doctor.Name} saved.";

        await LoadDoctorsAsync();
        SelectedDoctor = Doctors.FirstOrDefault(d => d.Id == doctor.Id);
    }
}
