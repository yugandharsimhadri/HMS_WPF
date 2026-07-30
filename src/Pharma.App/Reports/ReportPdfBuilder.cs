using System.Globalization;
using Pharma.App.ViewModels;
using Pharma.Core;
using Pharma.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pharma.App.Reports;

/// <summary>
/// Builds a printable PDF for whichever report tab is currently on screen, using
/// exactly the rows and totals already loaded into <see cref="ReportsViewModel"/> —
/// the export always matches what the user is looking at.
/// </summary>
public static class ReportPdfBuilder
{
    private static readonly CultureInfo InCulture = CultureInfo.GetCultureInfo("en-IN");

    private static string Inr(decimal v) => "₹" + v.ToString("N2", InCulture);
    private static string Num(decimal v) => v.ToString("N2", InCulture);

    public static IDocument Build(ReportKind kind, ReportsViewModel vm, ShopProfile shop)
    {
        // Stock Register is Excel-only by design — it's a wide, analysis-oriented
        // dump, not a printable statement. The Export PDF button is disabled on
        // that tab; this guard just makes the limitation explicit if ever bypassed.
        if (kind == ReportKind.StockRegister)
            throw new NotSupportedException("PDF export is not available for the Stock Register — use Export Excel instead.");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                // No letterhead here. It belongs on what a patient is handed —
                // a receipt, a prescription, a bill — and these are the
                // clinic's own working reports: a day book, a GST summary, a
                // stock register. Spending a third of every page of a
                // twenty-page register on a letterhead nobody outside the
                // clinic will read is paper and toner for nothing.
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(shop.Name).FontSize(16).Bold();
                    col.Item().AlignCenter().Text("OPD & Pharmacy").FontSize(10).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(6).AlignCenter().Text(ReportNaming.Title(kind)).FontSize(13).SemiBold();
                    col.Item().AlignCenter()
                        .Text(ReportNaming.DateLabel(kind, vm.Date, vm.FromDate, vm.ToDate))
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Element(c => ComposeTable(c, kind, vm));
                    col.Item().PaddingTop(10).Element(c => ComposeTotals(c, kind, vm));
                });

                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);

                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        });
    }

    // ── Cell chrome ───────────────────────────────────────────────────────

    private static IContainer HeaderCell(IContainer c) => c
        .Background(Colors.Grey.Lighten4)
        .BorderBottom(1).BorderColor(Colors.Grey.Darken1)
        .PaddingVertical(5).PaddingHorizontal(4)
        .DefaultTextStyle(x => x.SemiBold().FontSize(8).FontColor(Colors.Grey.Darken3));

    private static IContainer Cell(IContainer c) => c
        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
        .PaddingVertical(4).PaddingHorizontal(4);

    // ── Report bodies ─────────────────────────────────────────────────────

    private static void ComposeTable(IContainer container, ReportKind kind, ReportsViewModel vm)
    {
        switch (kind)
        {
            case ReportKind.DayBook: DayBookTable(container, vm); break;
            case ReportKind.GstSummary: GstTable(container, vm); break;
            case ReportKind.OpdRegister: OpdTable(container, vm); break;
            case ReportKind.ExpiringSoon: ExpiringTable(container, vm); break;
            case ReportKind.LowStock: LowStockTable(container, vm); break;
            case ReportKind.ScheduleH1: H1Table(container, vm); break;
        }
    }

    private static void DayBookTable(IContainer container, ReportsViewModel vm)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.3f); c.RelativeColumn(0.8f); c.RelativeColumn(2f);
                c.RelativeColumn(1.6f); c.RelativeColumn(0.7f); c.RelativeColumn(1f);
                c.RelativeColumn(1f); c.RelativeColumn(1f); c.RelativeColumn(1.1f); c.RelativeColumn(0.9f);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "BILL NO", "TIME", "CUSTOMER", "DOCTOR", "ITEMS", "TAXABLE", "CGST", "SGST", "NET", "MODE" })
                    h.Cell().Element(HeaderCell).Text(title);
            });

            foreach (var s in vm.Sales)
            {
                table.Cell().Element(Cell).Text(s.BillNo);
                table.Cell().Element(Cell).Text(s.BillDate.ToString("HH:mm"));
                table.Cell().Element(Cell).Text(s.CustomerName);
                table.Cell().Element(Cell).Text(s.DoctorName ?? "");
                table.Cell().Element(Cell).AlignRight().Text(s.Items.Count.ToString());
                table.Cell().Element(Cell).AlignRight().Text(Num(s.TaxableAmount));
                table.Cell().Element(Cell).AlignRight().Text(Num(s.CgstAmount));
                table.Cell().Element(Cell).AlignRight().Text(Num(s.SgstAmount));
                table.Cell().Element(Cell).AlignRight().Text(Num(s.NetAmount));
                table.Cell().Element(Cell).Text(s.PaymentMode.ToString());
            }
        });
    }

    private static void GstTable(IContainer container, ReportsViewModel vm)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1f); c.RelativeColumn(1.4f); c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f); c.RelativeColumn(1.2f); c.RelativeColumn(1.4f);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "GST RATE", "TAXABLE VALUE", "CGST", "SGST", "TOTAL TAX", "INVOICE VALUE" })
                    h.Cell().Element(HeaderCell).Text(title);
            });

            foreach (var g in vm.GstSummary)
            {
                table.Cell().Element(Cell).Text($"{g.Rate:0.#}%");
                table.Cell().Element(Cell).AlignRight().Text(Num(g.Taxable));
                table.Cell().Element(Cell).AlignRight().Text(Num(g.Cgst));
                table.Cell().Element(Cell).AlignRight().Text(Num(g.Sgst));
                table.Cell().Element(Cell).AlignRight().Text(Num(g.Cgst + g.Sgst));
                table.Cell().Element(Cell).AlignRight().Text(Num(g.Total));
            }
        });
    }

    private static void OpdTable(IContainer container, ReportsViewModel vm)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.1f); c.RelativeColumn(0.9f); c.RelativeColumn(1.8f);
                c.RelativeColumn(1f); c.RelativeColumn(1.6f); c.RelativeColumn(1f); c.RelativeColumn(0.8f);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "VISIT NO", "TIME", "PATIENT", "AGE/GENDER", "DOCTOR", "FEE", "PAID" })
                    h.Cell().Element(HeaderCell).Text(title);
            });

            foreach (var v in vm.Visits)
            {
                table.Cell().Element(Cell).Text(v.VisitNo);
                table.Cell().Element(Cell).Text(v.ScheduledOn.ToString("HH:mm"));
                table.Cell().Element(Cell).Text(v.Patient.Name);
                table.Cell().Element(Cell).Text($"{v.Patient.Age}/{v.Patient.Gender.ToString()[0]}");
                table.Cell().Element(Cell).Text(v.Doctor.Name);
                table.Cell().Element(Cell).AlignRight().Text(Num(v.Fee));
                table.Cell().Element(Cell).Text(v.FeePaid ? "Yes" : "No");
            }
        });
    }

    private static void ExpiringTable(IContainer container, ReportsViewModel vm)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.2f); c.RelativeColumn(1f); c.RelativeColumn(1.1f);
                c.RelativeColumn(0.9f); c.RelativeColumn(0.9f); c.RelativeColumn(1.6f); c.RelativeColumn(1f);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "MEDICINE", "BATCH", "EXPIRY", "QTY LEFT", "MRP", "SUPPLIER", "DAYS REMAINING" })
                    h.Cell().Element(HeaderCell).Text(title);
            });

            foreach (var row in vm.Expiring)
            {
                var b = row.Batch;
                table.Cell().Element(Cell).Text(b.Product.Name);
                table.Cell().Element(Cell).Text(b.BatchNo);
                table.Cell().Element(Cell).Text(b.ExpiryDate.ToString("dd MMM yyyy"));
                table.Cell().Element(Cell).AlignRight().Text(b.QtyOnHand.ToString());
                table.Cell().Element(Cell).AlignRight().Text(Num(b.Mrp));
                table.Cell().Element(Cell).Text(b.SupplierName ?? "");

                var days = table.Cell().Element(Cell).AlignRight()
                    .Text(row.IsExpired ? "EXPIRED" : row.DaysRemaining.ToString());
                if (row.IsExpired) days.FontColor(Colors.Red.Medium).Bold();
            }
        });
    }

    private static void LowStockTable(IContainer container, ReportsViewModel vm)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.4f); c.RelativeColumn(1.2f); c.RelativeColumn(1f);
                c.RelativeColumn(1.1f); c.RelativeColumn(1.1f); c.RelativeColumn(1f);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "MEDICINE", "PACK", "RACK", "IN STOCK", "REORDER AT", "SHORTAGE" })
                    h.Cell().Element(HeaderCell).Text(title);
            });

            foreach (var p in vm.LowStock)
            {
                table.Cell().Element(Cell).Text(p.Name);
                table.Cell().Element(Cell).Text(p.PackSize ?? "");
                table.Cell().Element(Cell).Text(p.RackLocation ?? "");
                table.Cell().Element(Cell).AlignRight().Text(p.StockOnHand.ToString());
                table.Cell().Element(Cell).AlignRight().Text(p.ReorderLevel.ToString());
                table.Cell().Element(Cell).AlignRight().Text(p.Shortage.ToString());
            }
        });
    }

    private static void H1Table(IContainer container, ReportsViewModel vm)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.4f); c.RelativeColumn(1.1f); c.RelativeColumn(1.6f);
                c.RelativeColumn(1.6f); c.RelativeColumn(2f); c.RelativeColumn(1.1f); c.RelativeColumn(0.7f);
            });

            table.Header(h =>
            {
                foreach (var title in new[] { "DATE", "BILL NO", "PATIENT/CUSTOMER", "DOCTOR", "MEDICINE", "BATCH", "QTY" })
                    h.Cell().Element(HeaderCell).Text(title);
            });

            foreach (var e in vm.H1Register)
            {
                table.Cell().Element(Cell).Text(e.SoldOn.ToString("dd MMM yyyy HH:mm"));
                table.Cell().Element(Cell).Text(e.BillNo);
                table.Cell().Element(Cell).Text(e.PatientName);
                table.Cell().Element(Cell).Text(e.DoctorName ?? "");
                table.Cell().Element(Cell).Text(e.ProductName);
                table.Cell().Element(Cell).Text(e.BatchNo);
                table.Cell().Element(Cell).AlignRight().Text(e.Quantity.ToString());
            }
        });
    }

    // ── Totals ────────────────────────────────────────────────────────────

    private static void ComposeTotals(IContainer container, ReportKind kind, ReportsViewModel vm)
    {
        switch (kind)
        {
            case ReportKind.DayBook:
                TotalsBar(container,
                    ("Taxable", Inr(vm.DayBookTaxableTotal)),
                    ("CGST", Inr(vm.DayBookCgstTotal)),
                    ("SGST", Inr(vm.DayBookSgstTotal)),
                    ("Net sales", Inr(vm.DayBookNetTotal)),
                    ("Cash", Inr(vm.CashTotal)),
                    ("UPI", Inr(vm.UpiTotal)));
                break;

            case ReportKind.GstSummary:
                TotalsBar(container,
                    ("Taxable", Inr(vm.GstGrandTaxable)),
                    ("CGST", Inr(vm.GstGrandCgst)),
                    ("SGST", Inr(vm.GstGrandSgst)),
                    ("Total tax", Inr(vm.GstGrandCgst + vm.GstGrandSgst)),
                    ("Invoice value", Inr(vm.GstGrandTotal)));
                break;

            case ReportKind.OpdRegister:
                TotalsBar(container,
                    ("Total OPD visits", vm.VisitCount.ToString()),
                    ("Total consultation fees", Inr(vm.ConsultationTotal)));
                break;

            case ReportKind.ExpiringSoon:
                TotalsBar(container, ("Batches listed", vm.Expiring.Count.ToString()));
                break;

            case ReportKind.LowStock:
                TotalsBar(container, ("Medicines below reorder level", vm.LowStock.Count.ToString()));
                break;

            case ReportKind.ScheduleH1:
                TotalsBar(container,
                    ("Entries", vm.H1Register.Count.ToString()),
                    ("Total quantity", vm.H1TotalQuantity.ToString()));
                break;
        }
    }

    private static void TotalsBar(IContainer container, params (string Label, string Value)[] items)
    {
        container.PaddingTop(6).BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingTop(6).Row(row =>
        {
            row.RelativeItem();

            foreach (var (label, value) in items)
            {
                row.AutoItem().PaddingLeft(18).Text(t =>
                {
                    t.Span(label + ": ").FontSize(9).FontColor(Colors.Grey.Darken2);
                    t.Span(value).FontSize(10).Bold();
                });
            }
        });
    }
}
