using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackUnitsAndVendorCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPack",
                table: "StockEntryItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "StockEntries",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImportProfile",
                table: "StockEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportedFile",
                table: "StockEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "StockEntries",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PackLabel",
                table: "SaleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPack",
                table: "SaleItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowLooseSale",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPack",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerPack",
                table: "Batches",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "VendorProductCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VendorProfile = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorProductCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorProductCodes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockEntries_SupplierInvoiceNo",
                table: "StockEntries",
                column: "SupplierInvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductCodes_ProductId",
                table: "VendorProductCodes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductCodes_VendorProfile_Code",
                table: "VendorProductCodes",
                columns: new[] { "VendorProfile", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorProductCodes");

            migrationBuilder.DropIndex(
                name: "IX_StockEntries_SupplierInvoiceNo",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "UnitsPerPack",
                table: "StockEntryItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "ImportProfile",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "ImportedFile",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "StockEntries");

            migrationBuilder.DropColumn(
                name: "PackLabel",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "UnitsPerPack",
                table: "SaleItems");

            migrationBuilder.DropColumn(
                name: "AllowLooseSale",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsPerPack",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnitsPerPack",
                table: "Batches");
        }
    }
}
