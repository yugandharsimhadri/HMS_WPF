using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGstRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTaxInvoice",
                table: "Sales",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTaxInvoice",
                table: "Sales");
        }
    }
}
