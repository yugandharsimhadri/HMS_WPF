using System.Windows;
using System.Windows.Controls;
using Pharma.App.ViewModels;
using Pharma.Data;

namespace Pharma.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    // PasswordBox has no bindable Password property, so the user-management
    // form's Save button is a plain Click handler rather than a Command —
    // same reason LoginWindow reads its PasswordBoxes in code-behind.
    private void SaveUser_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var password = UserPasswordBox.Password;
        UserPasswordBox.Clear();

        vm.SaveUserAsync(string.IsNullOrEmpty(password) ? null : password)
          .Forget(nameof(SaveUser_Click));
    }
}
