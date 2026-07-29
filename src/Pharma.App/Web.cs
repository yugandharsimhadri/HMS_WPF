using System.Diagnostics;

namespace Pharma.App;

/// <summary>
/// Opens a link in whatever browser the PC uses.
/// </summary>
/// <remarks>
/// One place, because a clinic PC is exactly where this fails: no default
/// browser set, or a locked-down profile that refuses to launch one. A dead
/// link must never take the application down with it, so a failure is logged
/// and otherwise ignored — the user sees nothing happen, which is the truth.
/// </remarks>
public static class Web
{
    /// <summary>Opens an http or https address.</summary>
    /// <param name="url">The address to open.</param>
    public static void Open(string url)
    {
        // Only ever hand the shell an http(s) address. Everything passed here
        // today is a constant, but "open whatever you are given with
        // UseShellExecute" is a habit worth not getting into: it will run an
        // executable as happily as it opens a page.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            AppLog.Warn($"Refused to open a link that is not http(s): {url}");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            AppLog.Info($"Opened {uri.AbsoluteUri} in the browser.");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"The link {uri.AbsoluteUri} could not be opened ({ex.Message}).");
        }
    }
}
