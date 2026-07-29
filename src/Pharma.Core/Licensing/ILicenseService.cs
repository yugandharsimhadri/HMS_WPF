namespace Pharma.Core.Licensing;

/// <summary>
/// The one thing the application asks about its licence.
/// </summary>
/// <remarks>
/// Deliberately the only licensing type the UI knows. Where the licence came
/// from, how the expiry is stored, and how rollback is caught are all behind
/// this, so adding a signed licence file or an activation server later changes
/// a container registration rather than a screen.
/// </remarks>
public interface ILicenseService
{
    /// <summary>
    /// Decides whether the application may run, and records the run when it may.
    /// </summary>
    /// <remarks>
    /// Synchronous on purpose. It gates the first window, so there is nothing
    /// useful to do while it is in flight, and the work is one small file read.
    /// </remarks>
    LicenseValidationResult Validate();

    /// <summary>Whether the licence is past its expiry.</summary>
    bool IsExpired();

    /// <summary>Whole days left, never negative.</summary>
    int GetRemainingDays();

    /// <summary>Everything worth showing the user about their licence.</summary>
    LicenseInfo GetLicenseInfo();
}
