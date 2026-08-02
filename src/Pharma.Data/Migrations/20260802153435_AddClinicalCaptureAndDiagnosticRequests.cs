using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalCaptureAndDiagnosticRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeeTransactionNo",
                table: "Visits",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HeartRateBpm",
                table: "Visits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightCm",
                table: "Visits",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Spo2Percent",
                table: "Visits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodGroup",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuardianName",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferredBy",
                table: "DiagnosticBills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId",
                table: "DiagnosticBills",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VisitDiagnosticRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VisitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TestName = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitDiagnosticRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitDiagnosticRequests_DiagnosticTests_TestId",
                        column: x => x.TestId,
                        principalTable: "DiagnosticTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitDiagnosticRequests_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticBills_VisitId",
                table: "DiagnosticBills",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitDiagnosticRequests_TestId",
                table: "VisitDiagnosticRequests",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitDiagnosticRequests_VisitId",
                table: "VisitDiagnosticRequests",
                column: "VisitId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiagnosticBills_Visits_VisitId",
                table: "DiagnosticBills",
                column: "VisitId",
                principalTable: "Visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiagnosticBills_Visits_VisitId",
                table: "DiagnosticBills");

            migrationBuilder.DropTable(
                name: "VisitDiagnosticRequests");

            migrationBuilder.DropIndex(
                name: "IX_DiagnosticBills_VisitId",
                table: "DiagnosticBills");

            migrationBuilder.DropColumn(
                name: "FeeTransactionNo",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "HeartRateBpm",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "HeightCm",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "Spo2Percent",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "BloodGroup",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "GuardianName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ReferredBy",
                table: "DiagnosticBills");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "DiagnosticBills");
        }
    }
}
