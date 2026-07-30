using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pharma.Data;

/// <summary>
/// Used only by `dotnet ef`.
///
/// Adding a migration needs no real database — the tools read the model, not a
/// file — so it defaults to a throwaway one. But asking <i>which migrations has
/// this database had</i> needs the database in question, and that is the first
/// thing worth knowing when a clinic reports something odd. So the same
/// environment variable the application honours points the tools at a file:
///
///     $env:CLINICDESK_DB = "C:\HMS\DB\twinkle.db"
///     dotnet ef migrations list --project src\Pharma.Data --startup-project src\Pharma.Data
///
/// Point it at a copy of their database, never the original. `migrations list`
/// only reads, but nothing enforces that for every other command.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var path = Environment.GetEnvironmentVariable(DbBootstrapper.PathOverrideVariable);
        if (string.IsNullOrWhiteSpace(path)) path = "design.db";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        return new AppDbContext(options);
    }
}
