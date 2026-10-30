using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientAgeMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgeMonths",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeMonths",
                table: "Patients");
        }
    }
}
