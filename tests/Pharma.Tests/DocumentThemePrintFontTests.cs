using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// The print font family and size adjustment (Settings → Reports → Document
/// branding) round-trip through the same key/value Settings table every
/// other document-branding field already uses.
/// </summary>
public class DocumentThemePrintFontTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"printfont-{Guid.NewGuid():N}.db");
    private readonly SettingsService _settings;

    public DocumentThemePrintFontTests()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        var factory = services.BuildServiceProvider().GetRequiredService<IDbContextFactory<AppDbContext>>();

        _settings = new SettingsService(factory);

        using var db = factory.CreateDbContext();
        db.Database.Migrate();
    }

    [Fact]
    public async Task With_nothing_saved_yet_the_font_family_is_null_and_the_size_delta_is_zero()
    {
        var theme = await _settings.GetDocumentThemeAsync();

        Assert.Null(theme.PrintFontFamily);
        Assert.Equal(0, theme.PrintFontSizeDelta);
    }

    [Fact]
    public async Task A_saved_font_family_and_size_delta_are_read_back()
    {
        await _settings.SaveDocumentThemeAsync(new DocumentTheme
        {
            Footer = "x",
            PrintFontFamily = "Georgia",
            PrintFontSizeDelta = 2.5
        });

        var theme = await _settings.GetDocumentThemeAsync();

        Assert.Equal("Georgia", theme.PrintFontFamily);
        Assert.Equal(2.5, theme.PrintFontSizeDelta);
    }

    [Fact]
    public async Task A_negative_size_delta_round_trips_too()
    {
        await _settings.SaveDocumentThemeAsync(new DocumentTheme { Footer = "x", PrintFontSizeDelta = -1.5 });

        var theme = await _settings.GetDocumentThemeAsync();

        Assert.Equal(-1.5, theme.PrintFontSizeDelta);
    }

    [Fact]
    public async Task Clearing_the_font_family_back_to_blank_is_read_back_as_null()
    {
        await _settings.SaveDocumentThemeAsync(new DocumentTheme { Footer = "x", PrintFontFamily = "Georgia" });
        await _settings.SaveDocumentThemeAsync(new DocumentTheme { Footer = "x", PrintFontFamily = "" });

        var theme = await _settings.GetDocumentThemeAsync();

        Assert.Null(theme.PrintFontFamily);
    }

    // ── Title font — separate from the general print font ──────────────────

    [Fact]
    public async Task With_nothing_saved_yet_the_title_font_family_is_null_and_its_size_delta_is_zero()
    {
        var theme = await _settings.GetDocumentThemeAsync();

        Assert.Null(theme.TitleFontFamily);
        Assert.Equal(0, theme.TitleFontSizeDelta);
    }

    [Fact]
    public async Task A_saved_title_font_and_size_delta_are_read_back_independently_of_the_print_font()
    {
        await _settings.SaveDocumentThemeAsync(new DocumentTheme
        {
            Footer = "x",
            PrintFontFamily = "Georgia",
            PrintFontSizeDelta = 1,
            TitleFontFamily = "Playfair Display",
            TitleFontSizeDelta = 4
        });

        var theme = await _settings.GetDocumentThemeAsync();

        Assert.Equal("Georgia", theme.PrintFontFamily);
        Assert.Equal("Playfair Display", theme.TitleFontFamily);
        Assert.Equal(1, theme.PrintFontSizeDelta);
        Assert.Equal(4, theme.TitleFontSizeDelta);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch (IOException) { }
    }
}
