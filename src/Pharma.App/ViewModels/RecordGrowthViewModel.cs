using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Pharma.App.ViewModels;

/// <summary>What was entered on the form — growth is never billed, so
/// there is nothing else for the caller to do with it beyond recording it.</summary>
public record GrowthDraft(DateTime MeasuredOn, decimal? WeightKg, decimal? HeightCm, decimal? HeadCircumferenceCm);

/// <summary>Records one growth measurement — the popup form behind
/// Pediatrics' "+ Record growth" button.</summary>
public partial class RecordGrowthViewModel : ObservableObject
{
    public string Header => "Record growth measurement";

    public event Action? RequestClose;
    public GrowthDraft? Result { get; private set; }

    [ObservableProperty] private string _measuredOnText = DateTime.Today.ToString("yyyy-MM-dd");
    [ObservableProperty] private decimal? _weightKg;
    [ObservableProperty] private decimal? _heightCm;
    [ObservableProperty] private decimal? _headCircumferenceCm;
    [ObservableProperty] private string _status = "";

    [RelayCommand]
    private void Save()
    {
        if (WeightKg is null && HeightCm is null && HeadCircumferenceCm is null)
        {
            Status = "Enter at least one measurement.";
            return;
        }

        var measuredOn = DateTime.TryParse(MeasuredOnText, out var parsed) ? parsed : DateTime.Today;
        Result = new GrowthDraft(measuredOn, WeightKg, HeightCm, HeadCircumferenceCm);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
