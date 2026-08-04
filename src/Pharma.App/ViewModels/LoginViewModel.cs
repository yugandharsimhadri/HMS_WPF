using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App.ViewModels;

public enum LoginStage
{
    Credentials,
    ChangePassword,
    Recovery,
    RecoveryDone
}

/// <summary>
/// Drives the one login window through its four possible stages: enter
/// credentials, forced change on first sign-in, EnterpriseAdmin's recovery
/// picker, or the confirmation after a reset. Passwords come in as plain
/// method parameters rather than bound properties — <see cref="System.Windows.Controls.PasswordBox"/>
/// deliberately has no bindable Password dependency property, so the
/// window's code-behind reads it and passes it straight through.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SettingsService _settings;
    private User? _pendingUser;

    /// <summary>Raised once a sign-in is fully complete — credentials
    /// accepted and, if it was required, the password already changed.</summary>
    public event Action<User>? LoggedIn;

    /// <summary>Raised when Cancel is pressed. Only wired up when the window
    /// was opened to switch users rather than at startup — see
    /// <see cref="AllowCancel"/>.</summary>
    public event Action? CancelRequested;

    /// <summary>Whether a Cancel button is shown at all. False at startup,
    /// where closing the window with nothing signed in means the
    /// application itself does not open.</summary>
    public bool AllowCancel { get; }

    [ObservableProperty] private LoginStage stage = LoginStage.Credentials;
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string changePasswordHint = "";
    [ObservableProperty] private ObservableCollection<User> recoveryUserOptions = [];
    [ObservableProperty] private User? selectedRecoveryUser;
    [ObservableProperty] private string recoveryResultMessage = "";

    /// <summary>The clinic's own name, the same value the window title bar
    /// and the shell's sidebar show — not a name baked into the build, so a
    /// clinic that has renamed itself sees that reflected here too.</summary>
    [ObservableProperty] private string clinicName = "Sivaayaan HMS";

    [ObservableProperty] private BitmapImage? logoImage;
    public bool HasLogo => LogoImage is not null;

    public bool IsCredentialsStage => Stage == LoginStage.Credentials;
    public bool IsChangePasswordStage => Stage == LoginStage.ChangePassword;
    public bool IsRecoveryStage => Stage == LoginStage.Recovery;
    public bool IsRecoveryDoneStage => Stage == LoginStage.RecoveryDone;
    public bool IsNotBusy => !IsBusy;

    partial void OnStageChanged(LoginStage value)
    {
        OnPropertyChanged(nameof(IsCredentialsStage));
        OnPropertyChanged(nameof(IsChangePasswordStage));
        OnPropertyChanged(nameof(IsRecoveryStage));
        OnPropertyChanged(nameof(IsRecoveryDoneStage));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));
    partial void OnLogoImageChanged(BitmapImage? value) => OnPropertyChanged(nameof(HasLogo));

    public LoginViewModel(AuthService auth, SettingsService settings, bool allowCancel)
    {
        _auth = auth;
        _settings = settings;
        AllowCancel = allowCancel;

        LoadBrandingAsync().Forget("Loading the clinic branding for the login screen");
    }

    /// <summary>Same clinic identity the shell's own sidebar shows — read
    /// fresh every time this window opens rather than passed in, so a
    /// rename or a newly uploaded logo shows up the very next sign-in
    /// without anything else needing to know this window exists.</summary>
    private async Task LoadBrandingAsync()
    {
        var clinic = await _settings.GetClinicAsync();
        if (!string.IsNullOrWhiteSpace(clinic.Name)) ClinicName = clinic.Name;

        var theme = await _settings.GetDocumentThemeAsync();
        if (!theme.HasLogo) return;

        try
        {
            var bytes = Convert.FromBase64String(theme.LogoBase64!);
            using var stream = new MemoryStream(bytes);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            LogoImage = image;
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Login screen: saved logo could not be shown ({ex.Message}).");
        }
    }

    public async Task SubmitCredentialsAsync(string password)
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Enter a username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(Username, password);

            switch (result.Outcome)
            {
                case LoginOutcome.Failed:
                    ErrorMessage = result.Message ?? "Incorrect username or password.";
                    break;

                case LoginOutcome.Success when result.User!.MustChangePassword:
                    _pendingUser = result.User;
                    ChangePasswordHint = $"Set a new password for {result.User.Username} before continuing.";
                    Stage = LoginStage.ChangePassword;
                    break;

                case LoginOutcome.Success:
                    LoggedIn?.Invoke(result.User!);
                    break;

                case LoginOutcome.EnterpriseRecovery:
                    RecoveryUserOptions = new ObservableCollection<User>(await _auth.GetUsersAsync());
                    Stage = LoginStage.Recovery;
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Something went wrong. Please try again.";
            AppLog.Error("Login failed unexpectedly.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SubmitNewPasswordAsync(string newPassword, string confirmPassword)
    {
        ErrorMessage = "";
        if (_pendingUser is null) return;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            ErrorMessage = "The new password must be at least 8 characters.";
            return;
        }

        if (newPassword != confirmPassword)
        {
            ErrorMessage = "The passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            await _auth.ChangeOwnPasswordAsync(_pendingUser.Id, newPassword);
            var user = _pendingUser;
            _pendingUser = null;
            LoggedIn?.Invoke(user);
        }
        catch (Exception ex)
        {
            ErrorMessage = "The password could not be changed. Please try again.";
            AppLog.Error("Change password failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SubmitResetAsync(string temporaryPassword)
    {
        ErrorMessage = "";

        if (SelectedRecoveryUser is null)
        {
            ErrorMessage = "Choose a user.";
            return;
        }

        if (string.IsNullOrWhiteSpace(temporaryPassword) || temporaryPassword.Length < 8)
        {
            ErrorMessage = "The temporary password must be at least 8 characters.";
            return;
        }

        IsBusy = true;
        try
        {
            var name = await _auth.ResetPasswordAsync(SelectedRecoveryUser.Id, temporaryPassword);
            RecoveryResultMessage =
                $"Password for '{name}' has been reset. They can sign in with the temporary " +
                "password now, and will be asked to set their own right away.";
            Stage = LoginStage.RecoveryDone;
        }
        catch (Exception ex)
        {
            ErrorMessage = "The password could not be reset. Please try again.";
            AppLog.Error("Password reset via EnterpriseAdmin failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToLogin()
    {
        _pendingUser = null;
        SelectedRecoveryUser = null;
        RecoveryResultMessage = "";
        Username = "";
        ErrorMessage = "";
        Stage = LoginStage.Credentials;
    }

    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke();
}
