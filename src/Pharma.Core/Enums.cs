namespace Pharma.Core;

public enum Gender
{
    Male = 1,
    Female,
    Other
}

/// <summary>Lifecycle of a single OPD visit, from booking through to completion.</summary>
public enum VisitStatus
{
    Booked = 1,
    Waiting,
    InConsultation,
    Completed,
    Cancelled
}

/// <summary>
/// Drugs and Cosmetics Act schedule. H1 sales must be recorded in a register
/// retained for three years, which is why the schedule lives on the product.
/// </summary>
public enum DrugSchedule
{
    None = 0,
    H = 1,
    H1 = 2,
    X = 3
}

public enum PaymentMode
{
    Cash = 1,
    Upi,
    Card,
    Credit
}

public enum SaleStatus
{
    Completed = 1,
    Returned,
    Cancelled
}
