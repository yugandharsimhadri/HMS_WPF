using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <summary>
    /// The clinic takes no credit, so PaymentMode.Credit (4) was removed.
    ///
    /// No table changes — but a bill saved as Credit before this would now hold a
    /// number the enum no longer knows, which reads back as an unnamed value on
    /// screen and on a reprint. Those rows are moved to Cash: the money was
    /// collected in some form, and Credit was never tracked as a debt anyway.
    /// </summary>
    public partial class RemoveCreditPaymentMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Sales SET PaymentMode = 1 WHERE PaymentMode = 4;");
            migrationBuilder.Sql("UPDATE Visits SET FeePaymentMode = 1 WHERE FeePaymentMode = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which rows had been Credit is not recorded, so this cannot be undone.
        }
    }
}
