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

/// <summary>
/// How a bill or a consultation fee was settled.
///
/// Every sale is paid in full at the counter — the clinic does not take credit
/// and does not take part payments, so there is deliberately no Credit option
/// and no outstanding balance anywhere in the system. The numbers are fixed so
/// existing records keep their meaning.
/// </summary>
public enum PaymentMode
{
    Cash = 1,
    Upi = 2,
    Card = 3
}

/// <summary>Why a count on the shelf did not match the count in the system.</summary>
public enum AdjustmentReason
{
    /// <summary>Physically counted and the system was wrong.</summary>
    Recount = 1,

    Breakage,
    Expired,
    Lost,

    /// <summary>Keyed in wrongly when the stock was received.</summary>
    EntryError,

    Other
}

public enum SaleStatus
{
    Completed = 1,
    Returned,
    Cancelled
}
