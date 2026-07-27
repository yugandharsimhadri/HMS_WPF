using Pharma.Core;

namespace Pharma.Tests;

/// <summary>
/// The pack arithmetic, held to its promises across every pack size rather than
/// the handful anyone thinks to write down.
///
/// The reported fault was arithmetic that is right for a strip of 10 and wrong
/// for a strip of 15. Example tests would never have caught it; these walk every
/// pack size from 1 to 30 and every quantity to 200.
/// </summary>
public class PackMathPropertyTests
{
    private static IEnumerable<int> PackSizes => Enumerable.Range(1, 30);
    private static IEnumerable<int> Quantities => Enumerable.Range(1, 200);

    private static readonly decimal[] Prices =
        [1m, 2.50m, 9.99m, 12m, 30m, 33.33m, 87.50m, 112m, 120m, 999.95m];

    /// <summary>
    /// The promise the whole design rests on. A customer buying a full strip must
    /// pay exactly what is printed on it — never MRP ÷ 15 × 15, which is 87.45
    /// for an 87.50 strip and an argument at the counter.
    /// </summary>
    [Fact]
    public void A_whole_pack_always_costs_exactly_the_printed_price()
    {
        foreach (var mrp in Prices)
        foreach (var perPack in PackSizes)
        foreach (var packs in new[] { 1, 2, 3, 7, 13 })
        {
            var expected = mrp * packs;
            var actual = PackMath.Gross(mrp, perPack, perPack * packs);

            Assert.True(expected == actual,
                $"{packs} pack(s) of {perPack} at {mrp}: expected {expected}, got {actual}");
        }
    }

    /// <summary>Selling more can never cost less.</summary>
    [Fact]
    public void Price_never_goes_down_as_the_quantity_goes_up()
    {
        foreach (var mrp in Prices)
        foreach (var perPack in PackSizes)
        {
            var previous = 0m;

            foreach (var quantity in Quantities)
            {
                var price = PackMath.Gross(mrp, perPack, quantity);

                Assert.True(price >= previous,
                    $"{quantity} of {perPack} at {mrp}: {price} is less than {previous} for one fewer");

                previous = price;
            }
        }
    }

    /// <summary>
    /// Part of a pack never costs more than the whole pack. Rounding the unit
    /// price up could otherwise make 14 of a 15-strip dearer than 15.
    /// </summary>
    [Fact]
    public void Part_of_a_pack_never_costs_more_than_the_pack()
    {
        foreach (var mrp in Prices)
        foreach (var perPack in PackSizes.Where(p => p > 1))
        foreach (var loose in Enumerable.Range(1, perPack))
        {
            var price = PackMath.Gross(mrp, perPack, loose);

            Assert.True(price <= mrp,
                $"{loose} of a {perPack} pack at {mrp} came to {price}");
        }
    }

    /// <summary>Money is money: never a fraction of a paisa.</summary>
    [Fact]
    public void Every_price_is_a_whole_number_of_paise()
    {
        foreach (var mrp in Prices)
        foreach (var perPack in PackSizes)
        foreach (var quantity in Quantities.Where(q => q % 7 == 0))
        {
            var price = PackMath.Gross(mrp, perPack, quantity);

            Assert.Equal(price, decimal.Round(price, 2));
        }
    }

    /// <summary>Nothing sold, nothing charged — at any pack size.</summary>
    [Fact]
    public void Nothing_costs_nothing()
    {
        foreach (var mrp in Prices)
        foreach (var perPack in PackSizes)
            Assert.Equal(0m, PackMath.Gross(mrp, perPack, 0));
    }

    /// <summary>
    /// GST comes out of the price rather than being added to it, so the customer
    /// pays the MRP whatever the rate — the entire point of MRP-inclusive pricing.
    /// </summary>
    [Fact]
    public void Tax_is_extracted_from_the_price_never_added_to_it()
    {
        foreach (var mrp in Prices)
        foreach (var perPack in new[] { 1, 10, 15 })
        foreach (var rate in new[] { 0m, 5m, 12m, 18m })
        foreach (var quantity in new[] { 1, 7, 15, 30 })
        {
            var line = GstCalculator.Line(mrp, perPack, quantity, 0m, rate);
            var gross = PackMath.Gross(mrp, perPack, quantity);

            Assert.Equal(gross, line.Net);
            Assert.Equal(line.Net, decimal.Round(line.Taxable + line.Gst, 2));
        }
    }

    /// <summary>
    /// Describing a quantity and pricing it must agree about how many whole packs
    /// there are, or the bill says one thing and charges another.
    /// </summary>
    [Fact]
    public void What_is_described_is_what_is_charged()
    {
        foreach (var perPack in PackSizes.Where(p => p > 1))
        foreach (var quantity in Quantities)
        {
            var packs = quantity / perPack;
            var loose = quantity % perPack;

            var described = PackMath.Describe(quantity, perPack, $"{perPack} TAB");

            if (loose == 0) Assert.Contains($"{packs} × {perPack} TAB", described);
            else if (packs == 0) Assert.StartsWith($"{loose} ", described);
            else Assert.Contains($"{packs} × {perPack} TAB + {loose} ", described);
        }
    }

    /// <summary>
    /// A pack size read off a supplier's file is either a count we are sure of or
    /// nothing at all. Guessing "60ML" is 60 sellable units is how a bottle gets
    /// priced at a sixtieth.
    /// </summary>
    [Fact]
    public void A_measured_pack_never_yields_a_count()
    {
        foreach (var measure in new[] { "ML", "GM", "GR", "MG", "KG", "MCG", "L" })
        foreach (var size in new[] { "1", "15", "60", "100", "200" })
        {
            Assert.Null(PackMath.UnitsFromPacking($"{size}{measure}"));
            Assert.Null(PackMath.UnitsFromPacking($"{size} {measure}"));
        }
    }
}
