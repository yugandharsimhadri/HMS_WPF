using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>One gender choice offered on a reference-range row — "Any" is a
/// real, selectable option here, not the absence of one, so it needs its own
/// entry (null) alongside the three <see cref="Gender"/> values.</summary>
public record GenderOption(string Label, Gender? Value);

/// <summary>One reference-range row, buffered locally until the analyte
/// itself is saved — same "edit freely, commit wholesale" shape as
/// <see cref="DentalPackageEditorViewModel"/>'s item list, except each row
/// here is a real persisted row rather than an embedded snapshot.</summary>
public partial class LabRangeRow : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty] private string _label = "";
    [ObservableProperty] private Gender? _gender;
    [ObservableProperty] private decimal? _minAgeYears;
    [ObservableProperty] private decimal? _maxAgeYears;
    [ObservableProperty] private decimal? _lowValue;
    [ObservableProperty] private decimal? _highValue;
    [ObservableProperty] private string _textRange = "";

    public string GenderLabel => Gender is null ? "Any" : Gender.ToString()!;
    partial void OnGenderChanged(Gender? value) => OnPropertyChanged(nameof(GenderLabel));
}

/// <summary>
/// An analyte, added or edited over the shell, with its reference ranges
/// edited alongside it. Ranges are buffered in <see cref="Ranges"/> and only
/// reach <see cref="PathologyLabService"/> when Save is pressed — deleted
/// rows are tracked in <see cref="_removedRangeIds"/> so their DB rows are
/// removed at the same time.
/// </summary>
public partial class LabAnalyteEditorViewModel : ObservableObject
{
    private readonly PathologyLabService _lab;
    private readonly LabAnalyte _analyte;
    private readonly bool _existingInDb;
    private readonly List<Guid> _removedRangeIds = [];

    public LabAnalyteEditorViewModel(PathologyLabService lab, IEnumerable<string> categories, LabAnalyte? existing = null)
    {
        _lab = lab;
        _analyte = existing ?? new LabAnalyte();
        _existingInDb = existing is not null;

        foreach (var c in categories) Categories.Add(c);

        if (existing is null) return;

        Name = existing.Name;
        Category = existing.Category;
        Units = existing.Units;
        ResultType = existing.ResultType;
        DecimalPlaces = existing.DecimalPlaces;
        SequenceOrder = existing.SequenceOrder;
        Active = existing.Active;
    }

    public string Header => _existingInDb ? _analyte.Name : "New analyte";
    public bool CanDelete => _existingInDb;

    public ObservableCollection<string> Categories { get; } = [];
    public static Array ResultTypes => Enum.GetValues<LabResultType>();
    public static GenderOption[] GenderChoices { get; } =
        [new("Any", null), new(nameof(Gender.Male), Gender.Male), new(nameof(Gender.Female), Gender.Female), new(nameof(Gender.Other), Gender.Other)];

    public event Action? RequestClose;
    public string? Outcome { get; private set; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _units = "";
    [ObservableProperty] private LabResultType _resultType = LabResultType.Numeric;
    [ObservableProperty] private int _decimalPlaces = 1;
    [ObservableProperty] private int _sequenceOrder;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _nameMissing;

    public ObservableCollection<LabRangeRow> Ranges { get; } = [];

    // New-range input row
    [ObservableProperty] private string _rangeLabel = "";
    [ObservableProperty] private Gender? _rangeGender;
    [ObservableProperty] private decimal? _rangeMinAge;
    [ObservableProperty] private decimal? _rangeMaxAge;
    [ObservableProperty] private decimal? _rangeLow;
    [ObservableProperty] private decimal? _rangeHigh;
    [ObservableProperty] private string _rangeTextRange = "";

    public async Task LoadAsync()
    {
        Ranges.Clear();
        if (!_existingInDb) return;

        foreach (var r in await _lab.GetReferenceRangesAsync(_analyte.Id))
        {
            Ranges.Add(new LabRangeRow
            {
                Id = r.Id, Label = r.Label, Gender = r.Gender,
                MinAgeYears = r.MinAgeYears, MaxAgeYears = r.MaxAgeYears,
                LowValue = r.LowValue, HighValue = r.HighValue, TextRange = r.TextRange ?? ""
            });
        }
    }

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) NameMissing = false;
    }

    [RelayCommand]
    private void AddRange()
    {
        if (string.IsNullOrWhiteSpace(RangeLabel))
        {
            Warn("Give the range a label, e.g. \"Adult\" or \"Child 1-5y\".");
            return;
        }

        Ranges.Add(new LabRangeRow
        {
            Label = RangeLabel.Trim(), Gender = RangeGender, MinAgeYears = RangeMinAge, MaxAgeYears = RangeMaxAge,
            LowValue = RangeLow, HighValue = RangeHigh, TextRange = RangeTextRange.Trim()
        });

        RangeLabel = "";
        RangeGender = null;
        RangeMinAge = null;
        RangeMaxAge = null;
        RangeLow = null;
        RangeHigh = null;
        RangeTextRange = "";
    }

    [RelayCommand]
    private void RemoveRange(LabRangeRow? row)
    {
        if (row is null) return;

        Ranges.Remove(row);
        _removedRangeIds.Add(row.Id);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            NameMissing = true;
            Warn("Analyte name is required.");
            return;
        }

        NameMissing = false;

        _analyte.Name = Name.Trim();
        _analyte.Category = Category.Trim();
        _analyte.Units = Units.Trim();
        _analyte.ResultType = ResultType;
        _analyte.DecimalPlaces = DecimalPlaces;
        _analyte.SequenceOrder = SequenceOrder;
        _analyte.Active = Active;

        try
        {
            await _lab.SaveAnalyteAsync(_analyte);

            foreach (var id in _removedRangeIds) await _lab.DeleteReferenceRangeAsync(id);

            foreach (var row in Ranges)
            {
                await _lab.SaveReferenceRangeAsync(new LabAnalyteReferenceRange
                {
                    Id = row.Id, AnalyteId = _analyte.Id, Label = row.Label, Gender = row.Gender,
                    MinAgeYears = row.MinAgeYears, MaxAgeYears = row.MaxAgeYears,
                    LowValue = row.LowValue, HighValue = row.HighValue,
                    TextRange = string.IsNullOrWhiteSpace(row.TextRange) ? null : row.TextRange
                });
            }

            Outcome = $"{_analyte.Name} saved.";
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
        if (!_existingInDb) return;

        var confirm = Dialog.Show(
            $"Delete {_analyte.Name}?", "Analyte Master", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _lab.DeleteAnalyteAsync(_analyte.Id);
            Outcome = $"{_analyte.Name} deleted.";
            RequestClose?.Invoke();
        }
        catch (InvalidOperationException ex)
        {
            Warn(ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Analyte Master", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
