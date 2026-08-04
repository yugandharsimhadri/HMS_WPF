using System.Windows;
using System.Windows.Input;
using Pharma.App.ViewModels;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.Views;

/// <summary>
/// The one login window, shown modally either at startup (no Cancel, closing
/// it without signing in ends the application) or from "Log out" inside the
/// shell to switch users (Cancel leaves the current session as it was).
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public User? LoggedInUser { get; private set; }

    public LoginWindow(AuthService auth, SettingsService settings, bool allowCancel = false)
    {
        InitializeComponent();

        _vm = new LoginViewModel(auth, settings, allowCancel);
        _vm.LoggedIn += user =>
        {
            LoggedInUser = user;
            DialogResult = true;
            Close();
        };
        _vm.CancelRequested += () =>
        {
            DialogResult = false;
            Close();
        };

        // Stage changes retire a PasswordBox from view (e.g. credentials to
        // change-password) but WPF does not clear it for us — nothing bound
        // reads it, so a leftover value would just sit there unmasked in
        // memory for no reason.
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(LoginViewModel.Stage)) return;
            PasswordBox.Clear();
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
            ResetPasswordBox.Clear();
        };

        DataContext = _vm;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void SignIn_Click(object sender, RoutedEventArgs e)
        => _vm.SubmitCredentialsAsync(PasswordBox.Password).Forget(nameof(SignIn_Click));

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SignIn_Click(sender, e);
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
        => _vm.SubmitNewPasswordAsync(NewPasswordBox.Password, ConfirmPasswordBox.Password)
              .Forget(nameof(ChangePassword_Click));

    private void ConfirmPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ChangePassword_Click(sender, e);
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
        => _vm.SubmitResetAsync(ResetPasswordBox.Password).Forget(nameof(ResetPassword_Click));
}
