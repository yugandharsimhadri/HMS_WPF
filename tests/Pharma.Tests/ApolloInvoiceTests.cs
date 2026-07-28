using Pharma.Core;

namespace Pharma.Tests;

/// <summary>
/// A real chemist's bill, priced through our arithmetic.
///
/// Apollo Pharmacy, bill 26475GC0219254 of 8 July 2026 — five lines, two of them
/// part-pack sales of the exact kind that used to be billed as whole strips
/// here. The figures below are transcribed from that bill, so anywhere our
/// totals differ from theirs, one of us is charging the customer wrongly.
///
/// It is also the shape of bill this system has to be able to produce: what a
/// chemist actually prints, as against what we happened to build.
/// </summary>
public class ApolloInvoiceTests
{
    private const decimal NoDiscount = 0m;

    /// <summary>
    /// One line of the bill as it was printed. Apollo quotes MRP <b>per unit</b>
    /// — ₹1.95 a tablet — where we hold the MRP printed on the pack, so the pack
    /// price is the unit price times what the pack holds.
    /// </summary>
    private static decimal PackMrp(decimal unitMrp, int unitsPerPack)
        => Math.Round(unitMrp * unitsPerPack, 2, MidpointRounding.AwayFromZero);

    // ── The five lines, exactly as printed ─────────────────────────────────

    /// <summary>
    /// 100 tablets of a strip of ten, at ₹1.95 each, is ₹195.00 — ten whole
    /// strips. This is the line that would have come to ₹1,950 under the fault
    /// that started all of this.
    /// </summary>
    [Fact]
    public void Glycomet_one_hundred_tablets_of_a_ten_strip()
    {
        var amounts = GstCalculator.Line(PackMrp(1.95m, 10), 10, 100, NoDiscount, 5m);

        Assert.Equal(195.00m, amounts.Gross);
        Assert.Equal("10 × 10'S", PackMath.Describe(100, 10, "10'S", "tablets"));
    }

    /// <summary>
    /// 45 of a strip of fifteen: three whole strips, ₹74.25. Not a round number
    /// of packs by accident — 45 is 3 × 15 exactly.
    /// </summary>
    [Fact]
    public void Limcee_forty_five_tablets_of_a_fifteen_strip()
    {
        var amounts = GstCalculator.Line(PackMrp(1.65m, 15), 15, 45, NoDiscount, 5m);

        Assert.Equal(74.25m, amounts.Gross);
        Assert.Equal("3 × 15'S", PackMath.Describe(45, 15, "15'S", "tablets"));
    }

    /// <summary>
    /// Eight lozenges out of a jar of 288 — the loose sale in its purest form,
    /// and the one where pricing per unit rather than per pack matters most.
    /// </summary>
    [Fact]
    public void Strepsils_eight_lozenges_from_a_jar_of_288()
    {
        var amounts = GstCalculator.Line(PackMrp(3.50m, 288), 288, 8, NoDiscount, 5m);

        Assert.Equal(28.00m, amounts.Gross);
        Assert.Equal("8 lozenges", PackMath.Describe(8, 288, "288'S", "lozenges"));
    }

    /// <summary>A tube of toothpaste is one thing, whatever it weighs.</summary>
    [Theory]
    [InlineData(162.00, "COLGATE SENSITIVE PLUS TOOTH PASTE 70G")]
    [InlineData(280.00, "PEPSODENT SENSITIVITY CARE TOOTHPASTE 150G")]
    public void A_single_pack_is_priced_at_its_own_mrp(decimal mrp, string name)
    {
        var amounts = GstCalculator.Line(mrp, 1, 1, NoDiscount, 5m);

        Assert.Equal(mrp, amounts.Gross);
        Assert.Equal("1", PackMath.Describe(1, 1, name));
    }

    // ── The bill as a whole ────────────────────────────────────────────────

    private static IEnumerable<LineAmounts> TheWholeBill() =>
    [
        GstCalculator.Line(162.00m, 1, 1, NoDiscount, 5m),                  // Colgate
        GstCalculator.Line(PackMrp(1.95m, 10), 10, 100, NoDiscount, 5m),    // Glycomet
        GstCalculator.Line(PackMrp(1.65m, 15), 15, 45, NoDiscount, 5m),     // Limcee
        GstCalculator.Line(280.00m, 1, 1, NoDiscount, 5m),                  // Pepsodent
        GstCalculator.Line(PackMrp(3.50m, 288), 288, 8, NoDiscount, 5m)     // Strepsils
    ];

    /// <summary>
    /// Apollo's Gross is ₹753.41, which is these five lines (₹739.25) plus a
    /// ₹14.16 packing and handling charge we have no concept of. The medicines
    /// themselves have to come to ₹739.25.
    /// </summary>
    [Fact]
    public void The_five_medicine_lines_come_to_what_the_bill_says()
    {
        var bill = GstCalculator.Bill(TheWholeBill());

        Assert.Equal(739.25m, bill.Gross);
        Assert.Equal(753.41m, bill.Gross + 14.16m);
    }

    /// <summary>
    /// Tax comes out of the MRP, never on top of it. Everything on this bill is
    /// at 5%, so the taxable value and the GST have to add back to the gross to
    /// the paisa — if they added on top, the customer would be charged ₹776 for
    /// a bill printed at ₹739.
    /// </summary>
    [Fact]
    public void Gst_is_taken_out_of_the_mrp_not_added_to_it()
    {
        var bill = GstCalculator.Bill(TheWholeBill());

        Assert.Equal(bill.Gross, bill.Taxable + bill.Cgst + bill.Sgst);

        // 739.25 at 5% inclusive: 704.05 taxable, 35.20 tax.
        Assert.Equal(704.05m, bill.Taxable);
        Assert.Equal(35.20m, bill.Cgst + bill.Sgst);
    }

    /// <summary>
    /// CGST and SGST are not equal on this bill, and on a real one they always
    /// are — Apollo's reads "CGST: 6.39  SGST: 6.39".
    ///
    /// The halving happens per line, and an odd number of paise cannot be split
    /// evenly, so the spare paise goes to SGST every time. Five lines, four of
    /// them odd, and the bill ends up four paise heavier on SGST. The total tax
    /// is exactly right and the customer pays the right amount, so nothing on
    /// the counter is wrong — but CGST and SGST are each meant to be half the
    /// GST, and a return filed from these figures would not balance.
    ///
    /// Recorded rather than fixed: halving at the bill instead of the line is a
    /// change to how every bill is taxed and wants its own pass.
    /// </summary>
    [Fact]
    public void The_two_halves_of_gst_do_not_match_on_a_bill_of_odd_paise()
    {
        var bill = GstCalculator.Bill(TheWholeBill());

        Assert.NotEqual(bill.Cgst, bill.Sgst);
        Assert.Equal(0.04m, bill.Sgst - bill.Cgst);

        // What it should be, and would be if the split happened once per bill.
        Assert.Equal(17.60m, (bill.Cgst + bill.Sgst) / 2);
    }

    /// <summary>
    /// The reason Apollo prints a per-unit MRP and we print the pack's.
    ///
    /// On their bill every line multiplies out: 100 × 1.95 = 195.00, and a
    /// customer can check it. Ours prints the MRP from the pack — ₹19.50 for the
    /// strip — against a quantity in tablets, so 100 × 19.50 = 1,950 against a
    /// line reading 195.00. The arithmetic is right and the printed line cannot
    /// be checked, which on a bill is its own kind of wrong.
    /// </summary>
    [Fact]
    public void A_per_unit_price_is_what_makes_a_line_check_out()
    {
        var packMrp = PackMrp(1.95m, 10);
        var amounts = GstCalculator.Line(packMrp, 10, 100, NoDiscount, 5m);

        // What we would print today, and what it multiplies out to.
        Assert.Equal(19.50m, packMrp);
        Assert.NotEqual(amounts.Gross, packMrp * 100);

        // What Apollo prints, and what it multiplies out to.
        Assert.Equal(1.95m, PackMath.UnitPrice(packMrp, 10));
        Assert.Equal(amounts.Gross, PackMath.UnitPrice(packMrp, 10) * 100);
    }

    /// <summary>
    /// Their loose line, checked the same way: 8 × ₹3.50 = ₹28.00, where the
    /// jar's own MRP is ₹1,008.
    /// </summary>
    [Fact]
    public void The_loose_line_checks_out_against_the_unit_price_too()
    {
        var jar = PackMrp(3.50m, 288);
        var amounts = GstCalculator.Line(jar, 288, 8, NoDiscount, 5m);

        Assert.Equal(1008.00m, jar);
        Assert.Equal(amounts.Gross, PackMath.UnitPrice(jar, 288) * 8);
    }
}
