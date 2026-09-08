using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPediatricsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrowthMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VisitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MeasuredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AgeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightKg = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    HeightCm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    HeadCircumferenceCm = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    BmiValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrowthMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrowthMeasurements_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrowthMeasurements_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PediatricProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FatherName = table.Column<string>(type: "TEXT", nullable: true),
                    MotherName = table.Column<string>(type: "TEXT", nullable: true),
                    ParentPhone = table.Column<string>(type: "TEXT", nullable: true),
                    ParentOccupation = table.Column<string>(type: "TEXT", nullable: true),
                    BirthWeightKg = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PediatricProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PediatricProfiles_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BillNo = table.Column<string>(type: "TEXT", nullable: false),
                    BillDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientName = table.Column<string>(type: "TEXT", nullable: false),
                    PatientNo = table.Column<string>(type: "TEXT", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PaymentMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionNo = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VisitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReferredBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcedureBills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureBills_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcedureBills_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Procedures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Procedures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VaccineMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    DoseNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RecommendedAgeDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    SequenceOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Active = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccineMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureBillItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureName = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ProcedureBillItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureBillItems_ProcedureBills_BillId",
                        column: x => x.BillId,
                        principalTable: "ProcedureBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcedureBillItems_Procedures_ProcedureId",
                        column: x => x.ProcedureId,
                        principalTable: "Procedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientName = table.Column<string>(type: "TEXT", nullable: false),
                    VaccineId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VaccineName = table.Column<string>(type: "TEXT", nullable: false),
                    DoseNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    GivenOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BatchNo = table.Column<string>(type: "TEXT", nullable: true),
                    SiteOfInjection = table.Column<string>(type: "TEXT", nullable: true),
                    AdministeredBy = table.Column<string>(type: "TEXT", nullable: true),
                    NextDueOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcedureBillItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaccinationRecords_VaccineMasters_VaccineId",
                        column: x => x.VaccineId,
                        principalTable: "VaccineMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrowthMeasurements_PatientId",
                table: "GrowthMeasurements",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthMeasurements_VisitId",
                table: "GrowthMeasurements",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_PediatricProfiles_PatientId",
                table: "PediatricProfiles",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureBillItems_BillId",
                table: "ProcedureBillItems",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureBillItems_ProcedureId",
                table: "ProcedureBillItems",
                column: "ProcedureId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureBills_BillDate",
                table: "ProcedureBills",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureBills_BillNo",
                table: "ProcedureBills",
                column: "BillNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureBills_PatientId",
                table: "ProcedureBills",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureBills_VisitId",
                table: "ProcedureBills",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_Procedures_Department_Name",
                table: "Procedures",
                columns: new[] { "Department", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_PatientId",
                table: "VaccinationRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_VaccineId",
                table: "VaccinationRecords",
                column: "VaccineId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccineMasters_Name",
                table: "VaccineMasters",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrowthMeasurements");

            migrationBuilder.DropTable(
                name: "PediatricProfiles");

            migrationBuilder.DropTable(
                name: "ProcedureBillItems");

            migrationBuilder.DropTable(
                name: "VaccinationRecords");

            migrationBuilder.DropTable(
                name: "ProcedureBills");

            migrationBuilder.DropTable(
                name: "Procedures");

            migrationBuilder.DropTable(
                name: "VaccineMasters");
        }
    }
}
