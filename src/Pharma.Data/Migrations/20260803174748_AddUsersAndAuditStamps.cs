using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndAuditStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Visits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Visits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "VisitDiagnosticRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "VisitDiagnosticRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "VendorProductCodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "VendorProductCodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "StockEntryItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "StockEntryItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "StockEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "StockEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "StockAdjustments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "StockAdjustments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Sales",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "PrescriptionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "PrescriptionItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "ImportProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "ImportProfiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "H1Register",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "H1Register",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Doctors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Doctors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "DiagnosticTests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "DiagnosticTests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "DiagnosticBills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "DiagnosticBills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "DiagnosticBillItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "DiagnosticBillItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Counters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Counters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Batches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "Batches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "VisitDiagnosticRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "VisitDiagnosticRequests");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "VendorProductCodes");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "VendorProductCodes");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "StockEntryItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "StockEntryItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "ImportProfiles");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "ImportProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "H1Register");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "H1Register");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "DiagnosticTests");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DiagnosticTests");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "DiagnosticBills");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DiagnosticBills");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "DiagnosticBillItems");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "DiagnosticBillItems");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Counters");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Batches");
        }
    }
}
