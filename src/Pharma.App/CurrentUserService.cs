using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.App;

/// <summary>
/// Who is signed in right now, for the shell to show and for
/// <see cref="AppDbContext"/> to stamp records with (through
/// <see cref="ICurrentUserContext"/>). Null when login is switched off, or
/// before anyone has signed in — every screen already works with no user
/// attributed to what it saves, so nothing here forces one to exist.
/// </summary>
public class CurrentUserService : ICurrentUserContext, INotifyPropertyChanged
{
    private User? _current;

    public User? Current
    {
        get => _current;
        private set
        {
            if (ReferenceEquals(_current, value)) return;
            _current = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UserId));
            OnPropertyChanged(nameof(IsSignedIn));
            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    public Guid? UserId => Current?.Id;
    public bool IsSignedIn => Current is not null;
    public bool IsAdmin => Current?.Role == UserRole.Admin;

    public string DisplayLabel => Current is null ? "" : $"{Current.DisplayName} ({Current.Role})";

    public void SignIn(User user) => Current = user;
    public void SignOut() => Current = null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
