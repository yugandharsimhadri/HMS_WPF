using Pharma.Core;
using Pharma.Data.Import;

namespace Pharma.Tests;

/// <summary>
/// Parses the vendors' real exported files. These are the actual bills received,
/// so a regression here is a regression against production data.
/// </summary>
public class VendorBillParserTests
{
    private static string Fixture(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static ImportProfile ProfileA() => new()
    {
        Name = "A",
        ColumnMap = StandardMap,
        DateFormats = "dd/MM/yyyy|d/M/yyyy",
        ExpiryFormats = "M/yyyy|MM/yyyy",
        DefaultGstRate = 5m
    };

    private static ImportProfile ProfileB() => new()
    {
        Name = "B",
        ColumnMap = StandardMap,
        DateFormats = "dd-MM-yyyy|d-M-yyyy",
        ExpiryFormats = "MMM-yy|MMM-yyyy",
        DefaultGstRate = 5m
    };

    private const string StandardMap = """
        BillNo=FeedNo
        BillDate=FeedDate
        CustomerName=CustName
        SubTotal=SubTotal
        DiscountPercent=DisPer
        TotalDiscount=SumDis
        TaxableValue=GstVal1
        TaxAmount=Gst1
        RoundOff=Rounding
        NetAmount=NetAmt
        ProductCode=ProdCode
        ProductName=ProdName
        PackSize=Packing
        BatchNo=BatchNo
        Quantity=Qty
        FreeQuantity=Free
        Rate=Rate
        Mrp=Mrp
        LineValue=ProValue
        GstPercent=IGstPer
        Expiry=Expiry
        Manufacturer=ComName
        HsnCode=HsnCode
        PreviousMrp=Mrp_Old
        """;

    // ── Profile A ──────────────────────────────────────────────────────────

    [Fact]
    public void Profile_A_parses_its_single_line_bill()
    {
        var bill = new VendorBillParser(ProfileA()).Parse(Fixture("Profile_A.csv"));

        Assert.False(bill.HasErrors);
        Assert.Equal("ER01441", bill.BillNo);
        Assert.Equal(new DateTime(2026, 7, 14), bill.BillDate);
        Assert.Single(bill.Lines);

        var line = bill.Lines[0];
        Assert.Equal("RELENT PLUS SYRUP 60ML", line.ProductName);
        Assert.Equal("D260374", line.BatchNo);
        Assert.Equal(20, line.Quantity);
        Assert.Equal(2, line.FreeQuantity);
        Assert.Equal(102.50m, line.Rate);      // what the hospital pays
        Assert.Equal(134.53m, line.Mrp);       // what the customer pays
        Assert.Equal(5m, line.GstPercent);
        Assert.Equal("30049099", line.HsnCode);
    }

    [Fact]
    public void Numeric_expiry_becomes_the_end_of_that_month()
    {
        var bill = new VendorBillParser(ProfileA()).Parse(Fixture("Profile_A.csv"));

        // "4/2028" — good until the last day of April 2028.
        Assert.Equal(new DateTime(2028, 4, 30), bill.Lines[0].Expiry);
    }

    [Fact]
    public void Free_goods_lower_the_real_cost_per_unit()
    {
        var line = new VendorBillParser(ProfileA()).Parse(Fixture("Profile_A.csv")).Lines[0];

        Assert.Equal(22, line.TotalUnits);            // 20 paid + 2 free
        Assert.Equal(93.18m, line.EffectiveUnitCost); // 2050 / 22, not the stated 102.50
    }

    [Fact]
    public void A_reduced_mrp_is_reported_but_does_not_block_the_import()
    {
        var bill = new VendorBillParser(ProfileA()).Parse(Fixture("Profile_A.csv"));

        // Mrp_Old 143.50 → Mrp 134.53.
        var notice = Assert.Single(bill.Issues, i => i.Field == ImportField.Mrp);
        Assert.Equal(ImportSeverity.Info, notice.Severity);
        Assert.Contains("down from", notice.Message);
        Assert.False(bill.HasErrors);
    }

    // ── Profile B ──────────────────────────────────────────────────────────

    [Fact]
    public void Profile_B_parses_every_line_of_the_nine_line_bill()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        Assert.False(bill.HasErrors);
        Assert.Equal("SW02236", bill.BillNo);
        Assert.Equal(new DateTime(2026, 7, 4), bill.BillDate);
        Assert.Equal(9, bill.Lines.Count);
        Assert.Equal(5m, bill.DiscountPercent);
        Assert.Equal(15334m, bill.NetAmount);
    }

    [Fact]
    public void Month_name_expiry_becomes_the_end_of_that_month()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        var calcimax = bill.Lines.Single(l => l.ProductName.StartsWith("CALCIMAX"));
        Assert.Equal(new DateTime(2027, 9, 30), calcimax.Expiry);   // "Sep-27"

        var calpol = bill.Lines.First(l => l.ProductName.StartsWith("CALPOL"));
        Assert.Equal(new DateTime(2028, 1, 31), calpol.Expiry);     // "Jan-28"
    }

    [Fact]
    public void The_same_medicine_on_two_batches_stays_two_lines()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        var calpol = bill.Lines.Where(l => l.ProductName == "CALPOL PED DROPS").ToList();

        Assert.Equal(2, calpol.Count);
        Assert.Equal(["NA497", "NA504"], calpol.Select(l => l.BatchNo).Order());
    }

    [Fact]
    public void Runs_of_spaces_in_a_vendor_name_are_collapsed()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        // The file contains "CIPLOX EYE DROPS       CIPLA".
        Assert.Contains(bill.Lines, l => l.ProductName == "CIPLOX EYE DROPS CIPLA");
        Assert.DoesNotContain(bill.Lines, l => l.ProductName.Contains("  "));
    }

    [Fact]
    public void The_bill_reconciles_against_its_own_totals()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        // 15372.40 − 768.62 discount = 14603.79 taxable, +730.21 GST = 15334.00.
        Assert.Contains(bill.Issues, i => i.Severity == ImportSeverity.Info && i.Message.Contains("reconcile"));
        Assert.DoesNotContain(bill.Issues, i => i.Field is "SubTotal" or "NetAmount" && i.Severity == ImportSeverity.Warning);
    }

    [Fact]
    public void Units_include_the_free_goods_across_the_whole_bill()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        // Paid 9+5+5+25+50+20+9+10+10 = 143, free 1+0+0+0+10+10+1+2+5 = 29.
        Assert.Equal(172, bill.TotalUnits);
    }

    // ── Profile safety ─────────────────────────────────────────────────────

    [Fact]
    public void An_ambiguous_date_is_flagged_so_it_can_be_checked()
    {
        var bill = new VendorBillParser(ProfileB()).Parse(Fixture("Profile_B.csv"));

        // "04-07-2026" is 4 July here, but 7 April under the other reading.
        var notice = Assert.Single(bill.Issues, i => i.Field == ImportField.BillDate);
        Assert.Equal(ImportSeverity.Info, notice.Severity);
        Assert.Contains("04 Jul 2026", notice.Message);
    }

    [Fact]
    public void The_wrong_profile_for_a_file_fails_with_a_clear_reason()
    {
        var wrong = new ImportProfile
        {
            Name = "Other vendor",
            ColumnMap = "BillNo=InvoiceNumber\nBillDate=InvoiceDate\nProductName=Item\n" +
                        "BatchNo=Batch\nQuantity=Qty\nRate=Cost\nMrp=Retail\nExpiry=Exp"
        };

        var bill = new VendorBillParser(wrong).Parse(Fixture("Profile_A.csv"));

        Assert.True(bill.HasErrors);
        var error = Assert.Single(bill.Issues, i => i.Severity == ImportSeverity.Error);
        Assert.Contains("expects columns not present", error.Message);
        Assert.Contains("InvoiceNumber", error.Message);
    }

    [Fact]
    public void Reading_a_file_with_the_wrong_expiry_format_is_an_error_not_a_guess()
    {
        // Profile A's numeric expiry rules applied to Profile B's "Sep-27".
        var bill = new VendorBillParser(ProfileA()).Parse(Fixture("Profile_B.csv"));

        Assert.True(bill.HasErrors);
        Assert.All(bill.Issues.Where(i => i.Severity == ImportSeverity.Error),
                   i => Assert.Equal(ImportField.Expiry, i.Field));
    }

    [Fact]
    public void A_missing_batch_number_blocks_the_line()
    {
        var csv = CsvFile.Parse(
            "FeedNo,FeedDate,ProdName,Packing,BatchNo,Qty,Free,Rate,Mrp,ProValue,IGstPer,Expiry,ComName,HsnCode,Mrp_Old\n" +
            "X1,01/07/2026,SOME SYRUP,60ml,,10,0,50,80,500,5,4/2028,ACME,3004,0\n");

        var bill = new VendorBillParser(ProfileA()).Parse(csv, "inline.csv");

        Assert.True(bill.HasErrors);
        Assert.Contains(bill.Issues, i => i.Field == ImportField.BatchNo && i.Severity == ImportSeverity.Error);
    }

    [Fact]
    public void Cost_above_mrp_is_flagged_because_every_sale_would_lose_money()
    {
        var csv = CsvFile.Parse(
            "FeedNo,FeedDate,ProdName,Packing,BatchNo,Qty,Free,Rate,Mrp,ProValue,IGstPer,Expiry,ComName,HsnCode,Mrp_Old\n" +
            "X1,01/07/2026,COSTLY SYRUP,60ml,B1,10,0,90,80,900,5,4/2028,ACME,3004,0\n");

        var bill = new VendorBillParser(ProfileA()).Parse(csv, "inline.csv");

        Assert.False(bill.HasErrors);
        Assert.Contains(bill.Issues,
            i => i.Field == ImportField.Rate && i.Severity == ImportSeverity.Warning);
    }

    [Fact]
    public void Two_different_bills_in_one_file_are_refused()
    {
        var csv = CsvFile.Parse(
            "FeedNo,FeedDate,ProdName,Packing,BatchNo,Qty,Free,Rate,Mrp,ProValue,IGstPer,Expiry,ComName,HsnCode,Mrp_Old\n" +
            "X1,01/07/2026,SYRUP ONE,60ml,B1,10,0,50,80,500,5,4/2028,ACME,3004,0\n" +
            "X2,01/07/2026,SYRUP TWO,60ml,B2,10,0,50,80,500,5,4/2028,ACME,3004,0\n");

        var bill = new VendorBillParser(ProfileA()).Parse(csv, "inline.csv");

        Assert.True(bill.HasErrors);
        Assert.Contains(bill.Issues, i => i.Message.Contains("mixes bills"));
    }
}
