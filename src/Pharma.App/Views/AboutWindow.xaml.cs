using System.Windows;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Pharma.App.ViewModels;
using Pharma.Core.Licensing;

namespace Pharma.App.Views;

/// <summary>
/// What this copy is and how long it may be used for — the screen support asks
/// people to read out.
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>Creates the dialog, resolving the licence through the container.</summary>
    public AboutWindow()
    {
        InitializeComponent();

        DataContext = new AboutViewModel(App.Services.GetRequiredService<ILicenseService>());
    }

    /// <summary>Opens the developer's site from the name under the product title.</summary>
    private void Vendor_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Web.Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
