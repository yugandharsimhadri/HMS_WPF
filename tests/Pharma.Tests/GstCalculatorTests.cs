using Pharma.Core;

namespace Pharma.Tests;

public class PaymentPolicyTests
{
    [Fact]
    public void Only_settled_in_full_payment_methods_are_offered()
    {
        // The clinic takes no credit and no part payments. Credit existed as an
        // option that recorded nothing, so a bill could look paid while the money
        // was never collected. Re-adding it needs this test changed first.
        Assert.Equal(
            ["Cash", "Upi", "Card"],
            Enum.GetNames<PaymentMode>());
    }

    [Fact]
    public void The_stored_numbers_do_not_move()
    {
        // Payment mode is persisted as a number, so existing bills would be
        // re-labelled if these ever shifted.
        Assert.Equal(1, (int)PaymentMode.Cash);
        Assert.Equal(2, (int)PaymentMode.Upi);
        Assert.Equal(3, (int)PaymentMode.Card);
    }
}

public class GstCalculatorTests
{
    [Fact]
    public void Tax_is_extracted_from_the_mrp_not_added_to_it()
    {
        // 10 strips at ₹112.00 MRP, 12% GST. The customer pays exactly ₹1120.
        var line = GstCalculator.Line(mrp: 112m, quantity: 10, discountPercent: 0m, gstRate: 12m);

        Assert.Equal(1120.00m, line.Net);
        Assert.Equal(1000.00m, line.Taxable);
        Assert.Equal(120.00m, line.Gst);
    }

    [Fact]
    public void Cgst_and_sgst_split_the_tax_exactly()
    {
        var line = GstCalculator.Line(mrp: 33.33m, quantity: 3, discountPercent: 0m, gstRate: 5m);

        Assert.Equal(line.Gst, line.Cgst + line.Sgst);
    }

    [Fact]
    public void Discount_reduces_the_taxable_value_as_well_as_the_tax()
    {
        var full = GstCalculator.Line(100m, 1, 0m, 12m);
        var discounted = GstCalculator.Line(100m, 1, 10m, 12m);

        Assert.Equal(90.00m, discounted.Net);
        Assert.True(discounted.Taxable < full.Taxable);
        Assert.True(discounted.Gst < full.Gst);
    }

    [Fact]
    public void Bill_rounds_to_the_nearest_rupee_and_records_the_difference()
    {
        var lines = new[] { GstCalculator.Line(10.40m, 1, 0m, 12m) };
        var bill = GstCalculator.Bill(lines);

        Assert.Equal(10m, bill.Net);
        Assert.Equal(-0.40m, bill.RoundOff);
    }

    [Fact]
    public void Round_off_always_reconciles_the_printed_total()
    {
        var lines = new[]
        {
            GstCalculator.Line(23.75m, 3, 0m, 5m),
            GstCalculator.Line(112.50m, 2, 7.5m, 12m),
            GstCalculator.Line(9.99m, 7, 0m, 18m)
        };

        var bill = GstCalculator.Bill(lines);
        var lineSum = lines.Sum(l => l.Net);

        Assert.Equal(lineSum + bill.RoundOff, bill.Net);
        Assert.Equal(bill.Net, Math.Round(bill.Net, 0));
    }

    [Fact]
    public void Taxable_plus_tax_equals_what_the_customer_pays()
    {
        var lines = new[]
        {
            GstCalculator.Line(45m, 4, 5m, 12m),
            GstCalculator.Line(120m, 1, 0m, 5m)
        };

        var bill = GstCalculator.Bill(lines);

        Assert.Equal(bill.Taxable + bill.Cgst + bill.Sgst, bill.Net - bill.RoundOff);
    }
}
