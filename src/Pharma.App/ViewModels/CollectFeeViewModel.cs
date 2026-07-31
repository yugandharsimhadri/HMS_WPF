using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

/// <summary>
/// Taking the consultation fee, over the shell.
///
/// Pressing Fee on a tile used to take the money there and then, at whatever
/// payment mode a combo box at the top of the screen happened to be left on,
/// and go straight to a print preview. Nothing was shown first and nothing
/// could be corrected after — a receipt is numbered and dated the moment it is
/// written, so a fee taken wrongly is a fee reversed on paper.
///
/// So it asks. The amount and the mode are both on screen, both editable, and
/// the last press is a plain yes or no naming the figure and the child.
/// </summary>
public partial class CollectFeeViewModel : ObservableObject
{
    private readonly OpdService _opd;
    private readonly SettingsService _settings;
    private readonly Visit _visit;

    public CollectFeeViewModel(OpdService opd, SettingsService settings, Visit visit)
    {
        _opd = opd;
        _settings = settings;
        _visit = visit;

        Fee = visit.Fee;
        _booked = visit.Fee;
    }

    /// <summary>What was quoted at booking, so a change can be pointed out.</summary>
    private readonly decimal _booked;

    public string Header => $"Token {_visit.TokenNo} — {_visit.Patient?.Name}";

    /// <summary>
    /// Who is being seen and by whom: the check before money changes hands.
    /// <c>PatientLine</c> is age, sex and the booked time.
    /// </summary>
    public string Summary => $"{_visit.PatientLine} · {_visit.Doctor?.Name}";

    public Array PaymentModes => Enum.GetValues<PaymentMode>();

    public event Action? RequestClose;

    /// <summary>What the queue should say. Null when nothing was taken.</summary>
    public string? Outcome { get; private set; }

    [ObservableProperty] private decimal _fee;
    [ObservableProperty] private PaymentMode _mode = PaymentMode.Cash;
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// On by default. Most clinics hand over a printed receipt, and the ones
    /// that do not can turn it off per collection — the receipt is still
    /// numbered and still reprintable from the patient's record either way.
    /// </summary>
    [ObservableProperty] private bool _printReceipt = true;

    /// <summary>
    /// Says so when the figure on screen is not the one quoted at booking.
    /// A concession is a decision; a mistyped digit is not, and they look the
    /// same until somebody says which this is.
    /// </summary>
    public string FeeNote => Fee == _booked
        ? ""
        : $"Booked at ₹{_booked:0.00}. This receipt will say ₹{Fee:0.00}.";

    partial void OnFeeChanged(decimal value) => OnPropertyChanged(nameof(FeeNote));

    [RelayCommand]
    private async Task CollectAsync()
    {
        if (Fee < 0)
        {
            Warn("A fee cannot be less than nothing.");
            return;
        }

        // The last gate before a number is burnt. It names the figure, the mode
        // and the child, because those are the three things that get mixed up
        // when two people are at the desk at once.
        var confirm = Dialog.Show(
            $"Take ₹{Fee:0.00} from {_visit.Patient?.Name} by {Mode}?",
            "Consultation fee", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        await Safely.RunAsync(async () =>
        {
            var paid = await _opd.CollectFeeAsync(_visit.Id, Mode, Fee);
            if (paid is null) return;

            Outcome = $"Receipt {paid.FeeReceiptNo} — ₹{paid.Fee:0.00} received from " +
                      $"{paid.Patient.Name} by {Mode}.";

            RequestClose?.Invoke();

            // After closing, so the preview opens over the queue rather than
            // over a form that is about to disappear from under it.
            if (!PrintReceipt) return;

            var shop = await _settings.GetAsync();
            PrintService.Preview(() => FeeReceiptDocument.Build(paid, shop), $"Receipt {paid.FeeReceiptNo}");
        }, "Taking the fee", m => Status = m);
    }

    /// <summary>Closes without taking anything. No receipt number is used up.</summary>
    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();

    private void Warn(string message)
    {
        Status = message;
        Dialog.Show(message, "Consultation fee", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
