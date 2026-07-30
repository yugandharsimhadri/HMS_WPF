using Pharma.Core;

namespace Pharma.Tests;

/// <summary>
/// What a medicine is sold as.
/// </summary>
/// <remarks>
/// The numbers matter more than they look. They are what is stored against every
/// product and every batch, so renumbering an existing member would silently
/// turn one kind of stock into another on a shelf nobody has touched.
/// </remarks>
public class DispensingUnitTests
{
    [Theory]
    [InlineData(DispensingUnit.Tablet, 1)]
    [InlineData(DispensingUnit.Capsule, 2)]
    [InlineData(DispensingUnit.Bottle, 3)]
    [InlineData(DispensingUnit.Sachet, 4)]
    [InlineData(DispensingUnit.Tube, 5)]
    [InlineData(DispensingUnit.Vial, 6)]
    [InlineData(DispensingUnit.Piece, 7)]
    [InlineData(DispensingUnit.Syrup, 8)]
    [InlineData(DispensingUnit.Moisturizer, 9)]
    [InlineData(DispensingUnit.Soap, 10)]
    [InlineData(DispensingUnit.Others, 11)]
    public void Each_unit_keeps_the_number_it_is_stored_as(DispensingUnit unit, int stored)
        => Assert.Equal(stored, (int)unit);

    [Fact]
    public void The_new_kinds_are_offered_on_the_medicine_screen()
    {
        // The screen binds straight to the enum, so being a member is being
        // offered. Named individually because these four were asked for.
        var offered = Enum.GetValues<DispensingUnit>();

        Assert.Contains(DispensingUnit.Syrup, offered);
        Assert.Contains(DispensingUnit.Moisturizer, offered);
        Assert.Contains(DispensingUnit.Soap, offered);
        Assert.Contains(DispensingUnit.Others, offered);
    }

    [Theory]
    [InlineData(DispensingUnit.Syrup, 1, "syrup")]
    [InlineData(DispensingUnit.Syrup, 4, "syrups")]
    [InlineData(DispensingUnit.Moisturizer, 1, "moisturizer")]
    [InlineData(DispensingUnit.Moisturizer, 3, "moisturizers")]
    [InlineData(DispensingUnit.Soap, 1, "soap")]
    [InlineData(DispensingUnit.Soap, 6, "soaps")]
    public void The_new_kinds_read_properly_in_a_sentence(DispensingUnit unit, int count, string expected)
        => Assert.Equal(expected, unit.Name(count));

    [Theory]
    [InlineData(1, "unit")]
    [InlineData(5, "units")]
    public void Others_reads_as_a_unit_rather_than_as_otherss(int count, string expected)
        // "Others" is already plural and is not the name of anything a shop can
        // hand over, so the naive plural would have printed "5 otherss left".
        => Assert.Equal(expected, DispensingUnit.Others.Name(count));
}
