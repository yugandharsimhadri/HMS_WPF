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
