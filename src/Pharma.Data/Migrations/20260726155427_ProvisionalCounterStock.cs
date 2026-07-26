using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProvisionalCounterStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnteredBy",
                table: "StockEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProvisional",
                table: "StockEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProvisional",
                table: "Batches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnteredBy",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "IsProvisional",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "IsProvisional",
                table: "Batches");
        }
    }
}
