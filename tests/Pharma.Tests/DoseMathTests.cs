using Pharma.Core;

namespace Pharma.Tests;

/// <summary>
/// A prescription is written in individual units and the pharmacy stocks strips.
/// These pin the arithmetic that joins the two, so nobody has to convert by hand.
/// </summary>
public class DoseMathTests
{
    [Theory]
    [InlineData("1-0-1", 2)]
    [InlineData("1-1-1", 3)]
    [InlineData("0-0-1", 1)]
    [InlineData("1-1-1-1", 4)]
    [InlineData("OD", 1)]
    [InlineData("BD", 2)]
    [InlineData("TDS", 3)]
    [InlineData("QID", 4)]
    [InlineData("HS", 1)]
    public void A_written_frequency_gives_doses_a_day(string frequency, decimal expected)
        => Assert.Equal(expected, DoseMath.DosesPerDay(frequency));

    [Fact]
    public void Half_doses_are_understood()
        => Assert.Equal(1m, DoseMath.DosesPerDay("1/2-0-1/2"));

    [Theory]
    [InlineData("SOS")]
    [InlineData("PRN")]
    [InlineData("as needed")]
    [InlineData("")]
    [InlineData(null)]
    public void An_as_needed_frequency_has_no_fixed_daily_dose(string? frequency)
        => Assert.Null(DoseMath.DosesPerDay(frequency));

    [Fact]
    public void A_three_day_course_twice_a_day_is_six_tablets()
        => Assert.Equal(6, DoseMath.UnitsForCourse("1-0-1", 3));

    [Fact]
    public void A_half_dose_course_rounds_up_to_whole_tablets()
    {
        // One a day for 5 days at half morning and half night = 5 tablets exactly.
        Assert.Equal(5, DoseMath.UnitsForCourse("1/2-0-1/2", 5));

        // Half a tablet cannot be handed over, and running short mid-course is
        // worse than one spare, so 1.5 a day for 3 days rounds 4.5 up to 5.
        Assert.Equal(5, DoseMath.UnitsForCourse("1/2-1/2-1/2", 3));
    }

    [Fact]
    public void No_quantity_is_suggested_when_it_cannot_be_worked_out()
    {
        Assert.Null(DoseMath.UnitsForCourse("SOS", 5));
        Assert.Null(DoseMath.UnitsForCourse("1-0-1", 0));
    }

    [Fact]
    public void A_course_reads_back_in_strips_for_the_pharmacy()
    {
        // 1-1-1 for 5 days is 15 tablets: one full strip of ten plus five loose.
        var units = DoseMath.UnitsForCourse("1-1-1", 5);

        Assert.Equal(15, units);
        Assert.Equal("1 × 10 TAB + 5 tablets", PackMath.Describe(units!.Value, 10, "10 TAB"));

        // The same course from a strip of fifteen is exactly one strip.
        Assert.Equal("1 × 15 TAB", PackMath.Describe(units.Value, 15, "15 TAB"));
    }

    [Fact]
    public void What_the_doctor_writes_is_what_the_counter_charges_for()
    {
        // 1-0-1 for 3 days = 6 tablets. From a 10-tablet strip at MRP 112 that is
        // six eleven-twenty units, and the customer is not charged for a strip.
        var units = DoseMath.UnitsForCourse("1-0-1", 3)!.Value;

        Assert.Equal(6, units);
        Assert.Equal(67.20m, PackMath.Gross(112m, 10, units));
    }
}
