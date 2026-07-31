using System.Reflection;

namespace Pharma.App;

/// <summary>
/// Who built this and which build it is.
///
/// One place, so the line in the sidebar, the About dialog and the line at the
/// top of every log file cannot end up disagreeing about the version — which is
/// exactly the sort of thing that wastes an hour on a support call.
/// </summary>
public static class AppInfo
{
    public const string Developer = "Sivayaan Technologies";

    /// <summary>Where the developer's name links to, on the sidebar and the
    /// About dialog.</summary>
    public const string DeveloperUrl = "https://sivayaantechnologies.com";

    /// <summary>
    /// What the build actually stamped on the assembly: "1.0.0.4" from the
    /// &lt;Version&gt; in the project file, where the fourth part counts the
    /// builds that have gone out to a clinic.
    ///
    /// Read from the informational version rather than the assembly version,
    /// because they are not the same thing and only one of them is the build.
    /// The assembly version discards any suffix — 1.2.3-beta is filed under
    /// 1.2.3.0 — so a screen reading it back cannot be trusted to agree with
    /// what was shipped.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    /// <summary>
    /// The commit the build came from, seven characters, or empty when the
    /// build was not made from a git working tree.
    ///
    /// Deliberately not on the sidebar. The publish number is what a support
    /// call turns on, and a line of hex beside it only invites the clinic to
    /// read out the wrong half. It stays in About and at the top of every log
    /// file, where whoever is diagnosing can find it — and it is the only thing
    /// that separates two builds of 1.0.0.4 where one was rebuilt after a fix
    /// that never got its own publish number.
    /// </summary>
    public static string Build { get; } = ReadBuild();

    public static string Credit => $"Developed by {Developer}";

    /// <summary>"Version 1.0.0.4", as it reads in the sidebar.</summary>
    public static string VersionLabel => $"Version {Version}";

    /// <summary>
    /// The whole thing as the build wrote it, commit and all: what About shows
    /// and what every log file opens with.
    /// </summary>
    public static string FullVersion =>
        Build.Length > 0 ? $"{Version}+{Build}" : Version;

    /// <summary>
    /// The raw attribute: "1.0.0.4+5b135f47b7a0adbfec32accb705b41712caec6c2",
    /// or "1.0.0.4" where the SDK did not append a commit.
    /// </summary>
    private static string? Informational =>
        Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

    private static string ReadVersion()
    {
        var informational = Informational;

        // Falls back to the assembly version rather than "unknown": inside a
        // test host, or a single-file bundle that strips attributes, a slightly
        // less precise number still beats no number at all. All four parts,
        // because the fourth is the publish number and dropping it would lose
        // the one thing that identifies the release.
        if (string.IsNullOrWhiteSpace(informational))
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }

    private static string ReadBuild()
    {
        var informational = Informational;
        if (string.IsNullOrWhiteSpace(informational)) return "";

        var plus = informational.IndexOf('+');
        if (plus < 0 || plus == informational.Length - 1) return "";

        var commit = informational[(plus + 1)..];
        return commit.Length <= 7 ? commit : commit[..7];
    }
}
