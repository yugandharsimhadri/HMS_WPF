namespace Pharma.App.Charts;

/// <summary>
/// Approximate median growth-by-age reference curves — commonly used
/// quick-estimation milestones from general pediatric teaching (e.g.
/// weight(kg) ≈ (age in months + 9) / 2 for 3–12 months), not the full WHO
/// percentile (LMS) tables, which this app does not embed. Meant to give
/// the growth chart something sensible to compare a patient's own
/// measurements against at a glance — a rough middle-of-the-road line, not
/// a diagnostic percentile band. A doctor reading a real growth concern
/// should still go to a proper WHO/IAP percentile chart, not this one.
/// Piecewise-linear between the named control points below.
/// </summary>
public static class GrowthReference
{
    private static readonly (double Months, double Value)[] WeightKgPoints =
    [
        (0, 3.25), (3, 6), (6, 7.5), (9, 9), (12, 10.5), (24, 12), (36, 14),
        (48, 16), (60, 18), (72, 20), (84, 22), (96, 25.5), (108, 29),
        (120, 32.5), (132, 36), (144, 39.5)
    ];

    private static readonly (double Months, double Value)[] HeightCmPoints =
    [
        (0, 50), (12, 75), (24, 87), (36, 95), (48, 101), (60, 107), (72, 113),
        (84, 119), (96, 125), (108, 131), (120, 137), (132, 143), (144, 149)
    ];

    private static readonly (double Months, double Value)[] HeadCircumferenceCmPoints =
    [
        (0, 35), (3, 40), (6, 43), (9, 45), (12, 46), (18, 47), (24, 48),
        (36, 49), (48, 50), (60, 50.5), (72, 51), (96, 51.4), (120, 51.8), (144, 52)
    ];

    /// <summary>The oldest age these curves cover — 12 years, past which
    /// growth charting belongs to a different (adult-adjacent) reference
    /// entirely and Pediatrics itself stops being the relevant module.</summary>
    public const double MaxAgeMonths = 144;

    public static double ExpectedWeightKg(double ageMonths) => Interpolate(WeightKgPoints, ageMonths);
    public static double ExpectedHeightCm(double ageMonths) => Interpolate(HeightCmPoints, ageMonths);
    public static double ExpectedHeadCircumferenceCm(double ageMonths) => Interpolate(HeadCircumferenceCmPoints, ageMonths);

    private static double Interpolate((double Months, double Value)[] points, double ageMonths)
    {
        var x = Math.Clamp(ageMonths, points[0].Months, points[^1].Months);

        for (var i = 0; i < points.Length - 1; i++)
        {
            var (x0, y0) = points[i];
            var (x1, y1) = points[i + 1];
            if (x < x0 || x > x1) continue;

            var t = x1 - x0 < 0.0001 ? 0 : (x - x0) / (x1 - x0);
            return y0 + t * (y1 - y0);
        }

        return points[^1].Value;
    }
}
