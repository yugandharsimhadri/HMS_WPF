using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Data;

namespace Pharma.App.Views;

public partial class ConsultationWindow : Window
{
    private readonly ConsultationViewModel _vm;

    public ConsultationWindow(Guid visitId)
    {
        InitializeComponent();

        _vm = new ConsultationViewModel(
            visitId,
            App.Services.GetRequiredService<OpdService>(),
            App.Services.GetRequiredService<PharmacyService>(),
            App.Services.GetRequiredService<SettingsService>());

        _vm.RequestClose += Close;
        DataContext = _vm;

        // An `async void` handler that throws would take the process down.
        Loaded += (_, _) => _vm.LoadAsync().Forget("Loading the consultation");
    }
}
