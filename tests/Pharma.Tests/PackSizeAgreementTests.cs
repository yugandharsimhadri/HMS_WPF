using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.Tests;

/// <summary>
/// A medicine whose pack size says "15 TAB" while its units-per-pack says 1 is
/// the worst kind of wrong: nothing errors, the shop just sells whole strips to
/// anyone asking for tablets and charges fifteen times the price.
///
/// Reported from the counter: 59 strips of Paracetamol on the shelf, a child
/// needs 9 tablets, and adding 9 took 9 strips.
/// </summary>
public class PackSizeAgreementTests
{
    [Theory]
    [InlineData("15 TAB", 15)]
    [InlineData("10 CAP", 10)]
    [InlineData("10 TAB", 10)]
    [InlineData("1x10", 10)]
    [InlineData("30s", 30)]
    public void A_pack_size_that_states_a_count_gives_units_per_pack(string packing, int expected)
        => Assert.Equal(expected, PackMath.UnitsFromPacking(packing));

    [Theory]
    [InlineData("100 ML")]
    [InlineData("21.8 G")]
    [InlineData("60ML")]
    public void A_pack_size_that_states_a_volume_or_weight_states_no_count(string packing)
        => Assert.Null(PackMath.UnitsFromPacking(packing));

    /// <summary>
    /// The catalogue the app ships with is the one every new clinic starts from,
    /// so a disagreement there reaches every user before they type anything.
    /// </summary>
    [Fact]
    public void Every_seeded_medicine_agrees_with_its_own_pack_size()
    {
        foreach (var product in DbBootstrapper.StarterCatalogue())
        {
            var stated = PackMath.UnitsFromPacking(product.PackSize);

            Assert.Equal(stated ?? 1, product.UnitsPerPack);
        }
    }

    /// <summary>
    /// The starter catalogue goes in under a unique index. Two medicines sharing
    /// a key would stop a fresh installation opening its own database.
    /// </summary>
    [Fact]
    public void No_two_seeded_medicines_share_a_key()
    {
        var keys = DbBootstrapper.StarterCatalogue().Select(p => p.BuildKey()).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(keys, k => k.StartsWith('|'));
    }

    /// <summary>
    /// Editing an existing medicine used to keep only some of the fields. The
    /// four it dropped included units-per-pack, so a shop that spotted the
    /// problem and corrected it saw the form accept the change and the counter
    /// go on selling strips.
    /// </summary>
    [Fact]
    public async Task Editing_a_medicine_keeps_every_field()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"pharma-edit-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = factory.CreateDbContext()) db.Database.Migrate();

        var pharmacy = new PharmacyService(factory);

        var product = new Product { Name = "Edit Me", PackSize = "10 TAB", UnitsPerPack = 1 };
        await pharmacy.SaveProductAsync(product);

        product.GenericName = "Paracetamol";
        product.PackSize = "15 TAB";
        product.UnitsPerPack = 15;
        product.AllowLooseSale = false;
        product.DispensingUnit = DispensingUnit.Capsule;
        await pharmacy.SaveProductAsync(product);

        var saved = (await pharmacy.SearchProductsAsync("Edit Me")).Single();

        Assert.Equal("Paracetamol", saved.GenericName);
        Assert.Equal("15 TAB", saved.PackSize);
        Assert.Equal(15, saved.UnitsPerPack);
        Assert.False(saved.AllowLooseSale);
        Assert.Equal(DispensingUnit.Capsule, saved.DispensingUnit);

        try { File.Delete(dbPath); } catch (IOException) { }
    }

    [Fact]
    public void Nine_tablets_out_of_a_strip_of_fifteen_is_not_nine_strips()
    {
        var paracetamol = DbBootstrapper.StarterCatalogue()
                                        .Single(p => p.Name.StartsWith("Paracetamol"));

        // 59 strips on the shelf, received as packs.
        var onHand = 59 * paracetamol.UnitsPerPack;
        Assert.Equal(885, onHand);

        // Two a day for four and a half days.
        const int prescribed = 9;

        // What leaves the shelf is nine tablets, not nine strips.
        Assert.Equal(876, onHand - prescribed);

        // And it is priced as nine tablets: 15 for 30.00 is 2.00 each.
        Assert.Equal(18.00m, PackMath.Gross(30.00m, paracetamol.UnitsPerPack, prescribed));
    }
}
