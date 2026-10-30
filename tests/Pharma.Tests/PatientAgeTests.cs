using Pharma.Core;

namespace Pharma.Tests;

/// <summary>
/// Age in years and months.
///
/// A paediatric register cannot work in whole years: half its list is under
/// two, and "0 years" is the same label for a newborn and an eleven-month-old
/// who are nothing alike clinically. These pin how the pair is written down,
/// how it reads back, and how it is worked out from a date of birth.
/// </summary>
public class PatientAgeTests
{
    private static Patient At(int years, int months) => new() { Age = years, AgeMonths = months };

    // ── How it reads ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, "0m")]
    [InlineData(0, 1, "1m")]
    [InlineData(0, 7, "7m")]
    [InlineData(0, 11, "11m")]
    [InlineData(1, 0, "1y")]
    [InlineData(4, 3, "4y 3m")]
    [InlineData(41, 0, "41y")]
    public void The_label_is_what_a_clinic_would_say(int years, int months, string expected)
        => Assert.Equal(expected, At(years, months).AgeLabel);

    /// <summary>
    /// The two halves that would read badly if the label were a plain template:
    /// an adult does not want "41y 0m", and a baby does not want "0y 7m".
    /// </summary>
    [Fact]
    public void A_zero_half_is_dropped_rather_than_printed()
    {
        Assert.DoesNotContain("0m", At(41, 0).AgeLabel);
        Assert.DoesNotContain("0y", At(0, 7).AgeLabel);
    }

    [Fact]
    public void The_display_line_carries_the_months()
    {
        var baby = new Patient { Name = "Baby Anika", Age = 0, AgeMonths = 7, Gender = Gender.Female, Phone = "9000000000" };
        Assert.Contains("7m", baby.Display);
    }

    // ── How it computes ───────────────────────────────────────────────────

    [Fact]
    public void Whole_months_lived_adds_the_two_together()
    {
        Assert.Equal(0, At(0, 0).AgeInMonths);
        Assert.Equal(7, At(0, 7).AgeInMonths);
        Assert.Equal(51, At(4, 3).AgeInMonths);
    }

    // ── How it comes off a date of birth ──────────────────────────────────

    [Fact]
    public void A_birthday_today_is_a_whole_number_of_years()
    {
        var dob = DateTime.Today.AddYears(-4);
        var (years, months) = Patient.AgeAndMonthsFromDob(dob);

        Assert.Equal(4, years);
        Assert.Equal(0, months);
    }

    [Fact]
    public void A_baby_born_seven_months_ago_is_seven_months_old()
    {
        var dob = DateTime.Today.AddMonths(-7);
        var (years, months) = Patient.AgeAndMonthsFromDob(dob);

        Assert.Equal(0, years);
        Assert.Equal(7, months);
    }

    [Fact]
    public void Four_years_and_three_months_reads_as_both()
    {
        var dob = DateTime.Today.AddYears(-4).AddMonths(-3);
        var (years, months) = Patient.AgeAndMonthsFromDob(dob);

        Assert.Equal(4, years);
        Assert.Equal(3, months);
    }

    /// <summary>
    /// The months never reach twelve — that is another year, and letting it
    /// through would make "1y 12m" and "2y" two spellings of one age.
    /// </summary>
    [Fact]
    public void The_months_never_reach_twelve()
    {
        for (var back = 0; back < 36; back++)
        {
            var dob = DateTime.Today.AddMonths(-back);
            var (_, months) = Patient.AgeAndMonthsFromDob(dob);

            Assert.InRange(months, 0, 11);
        }
    }

    /// <summary>
    /// Counted off the anniversary, not by dividing days by an average month.
    /// A child born on the 31st is five months old on the 30th by every
    /// calendar a parent owns, and day-division drifts a day per month and
    /// eventually reports the wrong figure.
    /// </summary>
    [Fact]
    public void Months_are_counted_by_the_calendar_not_by_dividing_days()
    {
        // Eleven months back, whatever the lengths of the months in between.
        var dob = DateTime.Today.AddMonths(-11);
        var (years, months) = Patient.AgeAndMonthsFromDob(dob);

        Assert.Equal(0, years);
        Assert.Equal(11, months);

        // And the very next month rolls the year over cleanly.
        var ayearAgo = DateTime.Today.AddMonths(-12);
        var (y2, m2) = Patient.AgeAndMonthsFromDob(ayearAgo);

        Assert.Equal(1, y2);
        Assert.Equal(0, m2);
    }

    [Fact]
    public void The_years_still_agree_with_the_old_whole_year_calculation()
    {
        // AgeFromDob is what every existing reader was built on; the pair must
        // not disagree with it, or the same patient would be two ages depending
        // on which call site asked.
        foreach (var back in new[] { 0, 1, 5, 18, 41, 80 })
        {
            var dob = DateTime.Today.AddYears(-back).AddMonths(-5);
            var (years, _) = Patient.AgeAndMonthsFromDob(dob);

            Assert.Equal(Patient.AgeFromDob(dob), years);
        }
    }

    /// <summary>Existing records carry no months, and must keep reading exactly
    /// as they did — this is every patient already on file when the column
    /// arrives.</summary>
    [Fact]
    public void A_patient_with_no_months_reads_as_before()
    {
        var existing = new Patient { Name = "Ramesh Kumar", Age = 41, Phone = "9000000001" };

        Assert.Equal(0, existing.AgeMonths);
        Assert.Equal("41y", existing.AgeLabel);
        Assert.Equal(41 * 12, existing.AgeInMonths);
    }
}
