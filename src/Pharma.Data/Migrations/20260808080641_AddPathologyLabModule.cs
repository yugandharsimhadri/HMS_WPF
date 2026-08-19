using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPathologyLabModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabAnalytes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    Units = table.Column<string>(type: "TEXT", nullable: false),
                    ResultType = table.Column<int>(type: "INTEGER", nullable: false),
                    DecimalPlaces = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_LabAnalytes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabPackageMasters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_LabPackageMasters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_LabReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabAnalyteReferenceRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalyteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: true),
                    MinAgeYears = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    MaxAgeYears = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    LowValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    HighValue = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    TextRange = table.Column<string>(type: "TEXT", nullable: true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAnalyteReferenceRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabAnalyteReferenceRanges_LabAnalytes_AnalyteId",
                        column: x => x.AnalyteId,
                        principalTable: "LabAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderNo = table.Column<string>(type: "TEXT", nullable: false),
                    OrderDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PatientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatientName = table.Column<string>(type: "TEXT", nullable: false),
                    PatientNo = table.Column<string>(type: "TEXT", nullable: false),
                    PackageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VisitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReferredBy = table.Column<string>(type: "TEXT", nullable: true),
                    SpecimenId = table.Column<string>(type: "TEXT", nullable: true),
                    CollectedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Discount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    PaymentMode = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionNo = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrders_LabPackageMasters_PackageId",
                        column: x => x.PackageId,
                        principalTable: "LabPackageMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabOrders_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabPackageReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PackageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabPackageReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabPackageReports_LabPackageMasters_PackageId",
                        column: x => x.PackageId,
                        principalTable: "LabPackageMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabPackageReports_LabReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabReportAnalytes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalyteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SequenceOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabReportAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabReportAnalytes_LabAnalytes_AnalyteId",
                        column: x => x.AnalyteId,
                        principalTable: "LabAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabReportAnalytes_LabReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabOrderReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReportName = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrderReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrderReports_LabOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrderReports_LabReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "LabReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderReportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnalyteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnalyteName = table.Column<string>(type: "TEXT", nullable: false),
                    Units = table.Column<string>(type: "TEXT", nullable: false),
                    ResultValue = table.Column<string>(type: "TEXT", nullable: false),
                    ReferenceRangeDisplay = table.Column<string>(type: "TEXT", nullable: false),
                    Flag = table.Column<int>(type: "INTEGER", nullable: false),
                    EnteredOn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnteredBy = table.Column<string>(type: "TEXT", nullable: true),
                    VerifiedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VerifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResults_LabAnalytes_AnalyteId",
                        column: x => x.AnalyteId,
                        principalTable: "LabAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabResults_LabOrderReports_OrderReportId",
                        column: x => x.OrderReportId,
                        principalTable: "LabOrderReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabAnalyteReferenceRanges_AnalyteId",
                table: "LabAnalyteReferenceRanges",
                column: "AnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_LabAnalytes_Name",
                table: "LabAnalytes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderReports_OrderId",
                table: "LabOrderReports",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrderReports_ReportId",
                table: "LabOrderReports",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_OrderDate",
                table: "LabOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_OrderNo",
                table: "LabOrders",
                column: "OrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_PackageId",
                table: "LabOrders",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_PatientId",
                table: "LabOrders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_VisitId",
                table: "LabOrders",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_LabPackageMasters_Name",
                table: "LabPackageMasters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_LabPackageReports_PackageId",
                table: "LabPackageReports",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_LabPackageReports_ReportId",
                table: "LabPackageReports",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LabReportAnalytes_AnalyteId",
                table: "LabReportAnalytes",
                column: "AnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_LabReportAnalytes_ReportId",
                table: "LabReportAnalytes",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_LabReports_Name",
                table: "LabReports",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_AnalyteId",
                table: "LabResults",
                column: "AnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_OrderReportId",
                table: "LabResults",
                column: "OrderReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabAnalyteReferenceRanges");

            migrationBuilder.DropTable(
                name: "LabPackageReports");

            migrationBuilder.DropTable(
                name: "LabReportAnalytes");

            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropTable(
                name: "LabAnalytes");

            migrationBuilder.DropTable(
                name: "LabOrderReports");

            migrationBuilder.DropTable(
                name: "LabOrders");

            migrationBuilder.DropTable(
                name: "LabReports");

            migrationBuilder.DropTable(
                name: "LabPackageMasters");
        }
    }
}
