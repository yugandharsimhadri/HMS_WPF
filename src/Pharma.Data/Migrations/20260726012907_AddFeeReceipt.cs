using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeePaidOn",
                table: "Visits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeePaymentMode",
                table: "Visits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeReceiptNo",
                table: "Visits",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeePaidOn",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "FeePaymentMode",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "FeeReceiptNo",
                table: "Visits");
        }
    }
}
