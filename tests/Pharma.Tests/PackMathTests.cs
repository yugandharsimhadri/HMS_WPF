using Pharma.Core;

namespace Pharma.Tests;

public class PackMathTests
{
    // ── Reading a pack size the vendor wrote as free text ───────────────────

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("10'S", 10)]
    [InlineData("10 TAB", 10)]
    [InlineData("15 CAP", 15)]
    [InlineData("6 PCS", 6)]
    [InlineData("1x10", 10)]
    [InlineData("2 X 15", 15)]
    public void A_countable_pack_gives_its_unit_count(string packing, int expected)
        => Assert.Equal(expected, PackMath.UnitsFromPacking(packing));

    [Theory]
    [InlineData("60ml")]      // a syrup bottle
    [InlineData("200ML")]
    [InlineData("15ML")]
    [InlineData("1GR")]       // a sachet, by weight
    [InlineData("100 GM")]
    [InlineData("")]
    [InlineData(null)]
    public void A_measured_pack_is_never_guessed_at(string? packing)
    {
        // "60ML" is one bottle, not sixty sellable units. Guessing here would
        // wreck both the stock count and the price.
        Assert.Null(PackMath.UnitsFromPacking(packing));
    }

    [Fact]
    public void Every_packing_in_the_real_vendor_files_is_read_or_left_alone()
    {
        // Exactly one of the ten lines across both supplier bills is countable.
        string[] packings = ["60ml", "200ML", "15ML", "15ML", "5ML", "1GR", "30s", "60ML", "30ml", "10ml"];

        var counted = packings.Select(PackMath.UnitsFromPacking).Where(u => u is not null).ToList();

        Assert.Single(counted);
        Assert.Equal(30, counted[0]);   // FERRO POPS IRON GUMMIES, "30s"
    }

    // ── Pricing ────────────────────────────────────────────────────────────

    [Fact]
    public void A_whole_strip_costs_exactly_the_printed_mrp()
    {
        // 87.50 over 15 does not divide evenly. Pricing whole packs from the pack
        // MRP rather than a rounded unit price is what keeps this exact.
        Assert.Equal(87.50m, PackMath.Gross(87.50m, 15, 15));
        Assert.Equal(175.00m, PackMath.Gross(87.50m, 15, 30));
    }

    [Fact]
    public void Five_tablets_out_of_a_ten_tablet_strip_cost_half()
    {
        // MRP 112 for 10 tablets, so 11.20 each.
        Assert.Equal(11.20m, PackMath.UnitPrice(112m, 10));
        Assert.Equal(56.00m, PackMath.Gross(112m, 10, 5));
    }

    [Fact]
    public void Packs_and_loose_units_on_one_line_add_up()
    {
        // Two full strips plus three loose tablets.
        Assert.Equal(224m + 33.60m, PackMath.Gross(112m, 10, 23));
    }

    [Fact]
    public void A_product_that_is_not_divisible_prices_per_pack()
    {
        // A 60ml syrup at MRP 134.53; three bottles.
        Assert.Equal(403.59m, PackMath.Gross(134.53m, 1, 3));
    }

    [Theory]
    [InlineData(0, 10, "0 tablets")]
    [InlineData(5, 10, "5 tablets")]
    [InlineData(10, 10, "1 × 10 TAB")]
    [InlineData(23, 10, "2 × 10 TAB + 3 tablets")]
    public void Stock_reads_the_way_the_counter_says_it(int qty, int units, string expected)
        => Assert.Equal(expected, PackMath.Describe(qty, units, "10 TAB"));

    [Fact]
    public void A_single_unit_pack_just_shows_the_number()
        => Assert.Equal("7", PackMath.Describe(7, 1, "60ML"));

    // ── GST still comes out of the MRP ──────────────────────────────────────

    [Fact]
    public void Tax_on_a_loose_sale_is_still_extracted_not_added()
    {
        // 5 tablets of a 112.00 strip at 12% GST = 56.00 paid by the customer.
        var line = GstCalculator.Line(112m, unitsPerPack: 10, quantity: 5,
                                      discountPercent: 0m, gstRate: 12m);

        Assert.Equal(56.00m, line.Net);
        Assert.Equal(50.00m, line.Taxable);
        Assert.Equal(6.00m, line.Gst);
        Assert.Equal(line.Gst, line.Cgst + line.Sgst);
    }

    [Fact]
    public void Selling_a_strip_loose_never_costs_more_than_the_strip()
    {
        // The rounding trap: 87.50 / 15 rounds up to 5.84, and 15 x 5.84 is 87.60.
        // Pricing whole packs from the MRP keeps a full strip at 87.50.
        var perUnit = PackMath.UnitPrice(87.50m, 15);
        Assert.Equal(5.83m, perUnit);

        Assert.Equal(87.50m, PackMath.Gross(87.50m, 15, 15));
        Assert.True(PackMath.Gross(87.50m, 15, 14) < 87.50m);
    }
}
