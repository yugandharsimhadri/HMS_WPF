using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDentistModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnesthesiaTypeMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultCost = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnesthesiaTypeMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DentalPackageMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    PackagePrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalPackageMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DentalReplacementMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalReplacementMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DentalCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientName = table.Column<string>(type: "TEXT", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureName = table.Column<string>(type: "TEXT", nullable: true),
                    PackageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PackageName = table.Column<string>(type: "TEXT", nullable: true),
                    ToothNumber = table.Column<string>(type: "TEXT", nullable: true),
                    DoctorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    BaseCost = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalCases_DentalPackageMasters_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DentalPackageMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DentalCases_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalCases_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalCases_Procedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "Procedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DentalPackageItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PackageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureName = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalPackageItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalPackageItems_DentalPackageMasters_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DentalPackageMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DentalPackageItems_Procedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "Procedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DentalCaseReplacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DentalCaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplacementId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalCaseReplacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalCaseReplacements_DentalCases_DentalCaseId",
                        column: x => x.DentalCaseId,
                        principalTable: "DentalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DentalCaseReplacements_DentalReplacementMasters_ReplacementId",
                        column: x => x.ReplacementId,
                        principalTable: "DentalReplacementMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DentalPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DentalCaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReceiptNo = table.Column<string>(type: "TEXT", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PaymentMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionNo = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalPayments_DentalCases_DentalCaseId",
                        column: x => x.DentalCaseId,
                        principalTable: "DentalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DentalSittings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DentalCaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SittingNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SittingDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DoctorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkDone = table.Column<string>(type: "TEXT", nullable: true),
                    AnesthesiaTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnesthesiaTypeName = table.Column<string>(type: "TEXT", nullable: true),
                    AnesthesiaCost = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    NextSittingOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalSittings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalSittings_AnesthesiaTypeMasters_AnesthesiaTypeId",
                        column: x => x.AnesthesiaTypeId,
                        principalTable: "AnesthesiaTypeMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DentalSittings_DentalCases_DentalCaseId",
                        column: x => x.DentalCaseId,
                        principalTable: "DentalCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DentalSittings_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnesthesiaTypeMasters_Name",
                table: "AnesthesiaTypeMasters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DentalCaseReplacements_DentalCaseId",
                table: "DentalCaseReplacements",
                column: "DentalCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalCaseReplacements_ReplacementId",
                table: "DentalCaseReplacements",
                column: "ReplacementId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalCases_DoctorId",
                table: "DentalCases",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalCases_PackageId",
                table: "DentalCases",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalCases_PatientId",
                table: "DentalCases",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalCases_ProcedureId",
                table: "DentalCases",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalPackageItems_PackageId",
                table: "DentalPackageItems",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalPackageItems_ProcedureId",
                table: "DentalPackageItems",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalPackageMasters_Name",
                table: "DentalPackageMasters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DentalPayments_DentalCaseId",
                table: "DentalPayments",
                column: "DentalCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalPayments_PaidOn",
                table: "DentalPayments",
                column: "PaidOn");

            migrationBuilder.CreateIndex(
                name: "IX_DentalPayments_ReceiptNo",
                table: "DentalPayments",
                column: "ReceiptNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentalReplacementMasters_Name",
                table: "DentalReplacementMasters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DentalSittings_AnesthesiaTypeId",
                table: "DentalSittings",
                column: "AnesthesiaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalSittings_DentalCaseId",
                table: "DentalSittings",
                column: "DentalCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalSittings_DoctorId",
                table: "DentalSittings",
                column: "DoctorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DentalCaseReplacements");

            migrationBuilder.DropTable(
                name: "DentalPackageItems");

            migrationBuilder.DropTable(
                name: "DentalPayments");

            migrationBuilder.DropTable(
                name: "DentalSittings");

            migrationBuilder.DropTable(
                name: "DentalReplacementMasters");

            migrationBuilder.DropTable(
                name: "AnesthesiaTypeMasters");

            migrationBuilder.DropTable(
                name: "DentalCases");

            migrationBuilder.DropTable(
                name: "DentalPackageMasters");
        }
    }
}
