using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductSearchKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchKey",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Every existing row needs a key before a unique index can go on it,
            // and the key has to match what Product.KeyFor builds in C#.
            migrationBuilder.Sql(
                """
                UPDATE Products
                SET SearchKey = lower(trim(Name)) || '|'
                             || lower(trim(coalesce(Manufacturer, ''))) || '|'
                             || lower(trim(coalesce(PackSize, '')));
                """);

            // A shop that already has the same medicine twice must still be able
            // to open the application. The older record keeps the real key, so the
            // duplicate check finds it; the rest are parked under their own id and
            // still reported by the health check, which compares names, not keys.
            migrationBuilder.Sql(
                """
                UPDATE Products
                SET SearchKey = SearchKey || '|dup:' || Id
                WHERE IsDeleted = 0
                  AND rowid NOT IN (SELECT MIN(rowid) FROM Products WHERE IsDeleted = 0 GROUP BY SearchKey);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Products_SearchKey",
                table: "Products",
                column: "SearchKey",
                unique: true,
                filter: "\"IsDeleted\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_SearchKey",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SearchKey",
                table: "Products");
        }
    }
}
