using ClosedXML.Excel;
using Pharma.App.ViewModels;
using Pharma.Data;

namespace Pharma.App.Reports;

/// <summary>
/// Builds an .xlsx workbook for whichever report tab is currently on screen, from
/// the same rows and totals already loaded into <see cref="ReportsViewModel"/>.
/// </summary>
public static class ReportExcelBuilder
{
    private const string CurrencyFormat = "\"₹\"#,##0.00";
    private const string IntFormat = "#,##0";
    private const string DateFormat = "dd-mmm-yyyy";
    private const string TimeFormat = "hh:mm";
    private const string DateTimeFormat = "dd-mmm-yyyy hh:mm";

    public static void Build(ReportKind kind, ReportsViewModel vm, string shopName, string path)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(SheetName(kind));

        var lastRow = kind switch
        {
            ReportKind.DayBook => DayBookSheet(ws, vm, shopName),
            ReportKind.GstSummary => GstSheet(ws, vm, shopName),
            ReportKind.OpdRegister => OpdSheet(ws, vm, shopName),
            ReportKind.ExpiringSoon => ExpiringSheet(ws, vm, shopName),
            ReportKind.LowStock => LowStockSheet(ws, vm, shopName),
            ReportKind.StockRegister => StockRegisterSheet(ws, vm, shopName),
            ReportKind.ScheduleH1 => H1Sheet(ws, vm, shopName),
            _ => 1
        };

        _ = lastRow;
        workbook.SaveAs(path);
    }

    private static string SheetName(ReportKind kind) => ReportNaming.Title(kind).Length > 31
        ? ReportNaming.Title(kind)[..31]
        : ReportNaming.Title(kind);

    // ── Title block ──────────────────────────────────────────────────────

    private static int WriteTitle(IXLWorksheet ws, ReportKind kind, ReportsViewModel vm, string shopName, int columns)
    {
        ws.Cell(1, 1).Value = shopName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(2, 1).Value = "OPD & Pharmacy";
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#61707E");

        ws.Cell(3, 1).Value = ReportNaming.Title(kind);
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Style.Font.FontSize = 12;

        ws.Cell(4, 1).Value = ReportNaming.DateLabel(kind, vm.Date, vm.FromDate, vm.ToDate);
        ws.Cell(4, 1).Style.Font.FontColor = XLColor.FromHtml("#61707E");

        var span = Math.Max(columns, 1);
        for (var r = 1; r <= 4; r++) ws.Range(r, 1, r, span).Merge();

        return 6; // first header row
    }

    private static void Finish(IXLWorksheet ws, int headerRow, int lastDataRow, int columns)
    {
        var header = ws.Range(headerRow, 1, headerRow, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F5");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        if (lastDataRow >= headerRow) ws.Range(headerRow, 1, lastDataRow, columns).SetAutoFilter();

        ws.SheetView.FreezeRows(headerRow);
        ws.Columns(1, columns).AdjustToContents();
    }

    // ── Cell writers ─────────────────────────────────────────────────────

    private static void Head(IXLWorksheet ws, int row, int col, string text) => ws.Cell(row, col).Value = text;

    private static void Str(IXLWorksheet ws, int row, int col, string? value) => ws.Cell(row, col).Value = value ?? "";

    private static void Int(IXLWorksheet ws, int row, int col, int value)
    {
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.NumberFormat.Format = IntFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void Money(IXLWorksheet ws, int row, int col, decimal value)
    {
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.NumberFormat.Format = CurrencyFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void Percent(IXLWorksheet ws, int row, int col, decimal ratePercent)
    {
        var cell = ws.Cell(row, col);
        cell.Value = ratePercent;
        cell.Style.NumberFormat.Format = "0.0\"%\"";
    }

    private static void DateCell(IXLWorksheet ws, int row, int col, DateTime value)
    {
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.NumberFormat.Format = DateFormat;
    }

    private static void TimeCell(IXLWorksheet ws, int row, int col, DateTime value)
    {
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.NumberFormat.Format = TimeFormat;
    }

    private static void DateTimeCell(IXLWorksheet ws, int row, int col, DateTime value)
    {
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.NumberFormat.Format = DateTimeFormat;
    }

    private static void SumFormula(IXLWorksheet ws, int row, int col, int firstDataRow, int lastDataRow, string format)
    {
        var cell = ws.Cell(row, col);
        var letter = ws.Cell(row, col).Address.ColumnLetter;
        cell.FormulaA1 = $"=SUM({letter}{firstDataRow}:{letter}{lastDataRow})";
        cell.Style.NumberFormat.Format = format;
        cell.Style.Font.Bold = true;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void BoldLabel(IXLWorksheet ws, int row, int col, string text)
    {
        var cell = ws.Cell(row, col);
        cell.Value = text;
        cell.Style.Font.Bold = true;
    }

    private static void MoneyFormula(IXLWorksheet ws, int row, int col, string formula)
    {
        var cell = ws.Cell(row, col);
        cell.FormulaA1 = formula;
        cell.Style.NumberFormat.Format = CurrencyFormat;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    // ── Day book ─────────────────────────────────────────────────────────

    private static int DayBookSheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 10;
        var headerRow = WriteTitle(ws, ReportKind.DayBook, vm, shopName, cols);

        string[] headers = ["Bill No", "Time", "Customer", "Doctor", "Items", "Taxable", "CGST", "SGST", "Net", "Mode"];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var s in vm.Sales)
        {
            Str(ws, row, 1, s.BillNo);
            TimeCell(ws, row, 2, s.BillDate);
            Str(ws, row, 3, s.CustomerName);
            Str(ws, row, 4, s.DoctorName);
            Int(ws, row, 5, s.Items.Count);
            Money(ws, row, 6, s.TaxableAmount);
            Money(ws, row, 7, s.CgstAmount);
            Money(ws, row, 8, s.SgstAmount);
            Money(ws, row, 9, s.NetAmount);
            Str(ws, row, 10, s.PaymentMode.ToString());
            row++;
        }
        var lastData = row - 1;

        if (lastData >= firstData)
        {
            BoldLabel(ws, row, 1, "TOTAL");
            SumFormula(ws, row, 6, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 7, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 8, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 9, firstData, lastData, CurrencyFormat);
            row++;
        }

        row++;
        BoldLabel(ws, row, 1, "Cash total"); Money(ws, row, 2, vm.CashTotal); row++;
        BoldLabel(ws, row, 1, "UPI total"); Money(ws, row, 2, vm.UpiTotal);

        Finish(ws, headerRow, lastData, cols);
        return row;
    }

    // ── GST summary ──────────────────────────────────────────────────────

    private static int GstSheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 6;
        var headerRow = WriteTitle(ws, ReportKind.GstSummary, vm, shopName, cols);

        string[] headers = ["GST Rate", "Taxable Value", "CGST", "SGST", "Total Tax", "Invoice Value"];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var g in vm.GstSummary)
        {
            Percent(ws, row, 1, g.Rate);
            Money(ws, row, 2, g.Taxable);
            Money(ws, row, 3, g.Cgst);
            Money(ws, row, 4, g.Sgst);
            Money(ws, row, 5, g.Cgst + g.Sgst);
            Money(ws, row, 6, g.Total);
            row++;
        }
        var lastData = row - 1;

        if (lastData >= firstData)
        {
            BoldLabel(ws, row, 1, "GRAND TOTAL");
            SumFormula(ws, row, 2, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 3, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 4, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 5, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 6, firstData, lastData, CurrencyFormat);
            row++;
        }

        Finish(ws, headerRow, lastData, cols);
        return row;
    }

    // ── OPD register ─────────────────────────────────────────────────────

    private static int OpdSheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 9;
        var headerRow = WriteTitle(ws, ReportKind.OpdRegister, vm, shopName, cols);

        string[] headers = ["Visit No", "Date", "Time", "Patient", "Age", "Gender", "Doctor", "Fee", "Paid"];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var v in vm.Visits)
        {
            Str(ws, row, 1, v.VisitNo);
            DateCell(ws, row, 2, v.ScheduledOn);
            TimeCell(ws, row, 3, v.ScheduledOn);
            Str(ws, row, 4, v.Patient.Name);
            Int(ws, row, 5, v.Patient.Age);
            Str(ws, row, 6, v.Patient.Gender.ToString());
            Str(ws, row, 7, v.Doctor.Name);
            Money(ws, row, 8, v.Fee);
            Str(ws, row, 9, v.FeePaid ? "Yes" : "No");
            row++;
        }
        var lastData = row - 1;

        if (lastData >= firstData)
        {
            BoldLabel(ws, row, 1, "TOTAL");
            SumFormula(ws, row, 8, firstData, lastData, CurrencyFormat);
            row++;
        }

        row++;
        BoldLabel(ws, row, 1, "Total OPD visits"); Int(ws, row, 2, vm.VisitCount); row++;
        BoldLabel(ws, row, 1, "Total consultation fees"); Money(ws, row, 2, vm.ConsultationTotal);

        Finish(ws, headerRow, lastData, cols);
        return row;
    }

    // ── Expiring soon ────────────────────────────────────────────────────

    private static int ExpiringSheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 8;
        var headerRow = WriteTitle(ws, ReportKind.ExpiringSoon, vm, shopName, cols);

        string[] headers = ["Medicine", "Batch", "Expiry Date", "Qty Left", "MRP", "Supplier", "Days Remaining", "Status"];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var r in vm.Expiring)
        {
            var b = r.Batch;
            Str(ws, row, 1, b.Product.Name);
            Str(ws, row, 2, b.BatchNo);
            DateCell(ws, row, 3, b.ExpiryDate);
            Int(ws, row, 4, b.QtyOnHand);
            Money(ws, row, 5, b.Mrp);
            Str(ws, row, 6, b.SupplierName);
            Int(ws, row, 7, r.DaysRemaining);
            Str(ws, row, 8, r.IsExpired ? "EXPIRED" : "OK");

            if (r.IsExpired)
            {
                var range = ws.Range(row, 1, row, cols);
                range.Style.Font.FontColor = XLColor.FromHtml("#B42318");
                range.Style.Font.Bold = true;
            }

            row++;
        }
        var lastData = row - 1;

        Finish(ws, headerRow, lastData, cols);
        return row;
    }

    // ── Low stock ────────────────────────────────────────────────────────

    private static int LowStockSheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 6;
        var headerRow = WriteTitle(ws, ReportKind.LowStock, vm, shopName, cols);

        string[] headers = ["Medicine", "Pack", "Rack", "In Stock", "Reorder Level", "Shortage"];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var p in vm.LowStock)
        {
            Str(ws, row, 1, p.Name);
            Str(ws, row, 2, p.PackSize);
            Str(ws, row, 3, p.RackLocation);
            Int(ws, row, 4, p.StockOnHand);
            Int(ws, row, 5, p.ReorderLevel);
            Int(ws, row, 6, p.Shortage);
            row++;
        }
        var lastData = row - 1;

        if (lastData >= firstData)
        {
            BoldLabel(ws, row, 1, "TOTAL");
            SumFormula(ws, row, 6, firstData, lastData, IntFormat);
            row++;
        }

        Finish(ws, headerRow, lastData, cols);
        return row;
    }

    // ── Schedule H1 register ─────────────────────────────────────────────

    private static int H1Sheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 7;
        var headerRow = WriteTitle(ws, ReportKind.ScheduleH1, vm, shopName, cols);

        string[] headers = ["Date", "Bill No", "Patient/Customer", "Doctor", "Medicine", "Batch", "Quantity"];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var e in vm.H1Register)
        {
            DateTimeCell(ws, row, 1, e.SoldOn);
            Str(ws, row, 2, e.BillNo);
            Str(ws, row, 3, e.PatientName);
            Str(ws, row, 4, e.DoctorName);
            Str(ws, row, 5, e.ProductName);
            Str(ws, row, 6, e.BatchNo);
            Int(ws, row, 7, e.Quantity);
            row++;
        }
        var lastData = row - 1;

        if (lastData >= firstData)
        {
            BoldLabel(ws, row, 1, "TOTAL");
            SumFormula(ws, row, 7, firstData, lastData, IntFormat);
            row++;
        }

        row++;
        BoldLabel(ws, row, 1, "Entries"); Int(ws, row, 2, vm.H1Register.Count);

        Finish(ws, headerRow, lastData, cols);
        return row;
    }

    // ── Stock register ───────────────────────────────────────────────────

    private static int StockRegisterSheet(IXLWorksheet ws, ReportsViewModel vm, string shopName)
    {
        const int cols = 13;
        var headerRow = WriteTitle(ws, ReportKind.StockRegister, vm, shopName, cols);

        string[] headers =
        [
            "Medicine", "Manufacturer", "Pack Size", "Batch No", "Expiry Date", "Rack Location",
            "Current Stock", "Reorder Level", "Shortage", "Purchase Rate", "MRP",
            "Stock Value (Cost)", "Stock Value (MRP)"
        ];
        for (var i = 0; i < headers.Length; i++) Head(ws, headerRow, i + 1, headers[i]);

        var row = headerRow + 1;
        var firstData = row;
        foreach (var b in vm.StockRegister)
        {
            Str(ws, row, 1, b.Product.Name);
            Str(ws, row, 2, b.Product.Manufacturer);
            Str(ws, row, 3, b.Product.PackSize);
            Str(ws, row, 4, b.BatchNo);
            DateCell(ws, row, 5, b.ExpiryDate);
            Str(ws, row, 6, b.Product.RackLocation);
            Int(ws, row, 7, b.QtyOnHand);
            Int(ws, row, 8, b.Product.ReorderLevel);
            Int(ws, row, 9, b.Product.Shortage);
            Money(ws, row, 10, b.PurchaseRate);
            Money(ws, row, 11, b.Mrp);

            // Live formulas rather than pre-computed numbers, so the workbook stays
            // auditable if a reader edits a quantity or rate while reviewing it.
            var qtyRef = ws.Cell(row, 7).Address.ColumnLetter + row;
            var costRef = ws.Cell(row, 10).Address.ColumnLetter + row;
            var mrpRef = ws.Cell(row, 11).Address.ColumnLetter + row;
            MoneyFormula(ws, row, 12, $"={qtyRef}*{costRef}");
            MoneyFormula(ws, row, 13, $"={qtyRef}*{mrpRef}");

            row++;
        }
        var lastData = row - 1;

        if (lastData >= firstData)
        {
            // Reorder Level and Shortage are per-product figures repeated on every
            // batch row of that product — summing them across batches would double
            // count a multi-batch medicine, so only genuinely batch-level columns
            // (quantity and the two value columns) get a column total.
            BoldLabel(ws, row, 1, "TOTAL");
            SumFormula(ws, row, 7, firstData, lastData, IntFormat);
            SumFormula(ws, row, 12, firstData, lastData, CurrencyFormat);
            SumFormula(ws, row, 13, firstData, lastData, CurrencyFormat);
            row++;
        }

        row++;
        BoldLabel(ws, row, 1, "Total Products"); Int(ws, row, 2, vm.StockTotalProducts); row++;
        BoldLabel(ws, row, 1, "Total Batches"); Int(ws, row, 2, vm.StockTotalBatches); row++;
        BoldLabel(ws, row, 1, "Total Units in Stock"); Int(ws, row, 2, vm.StockTotalUnits); row++;
        BoldLabel(ws, row, 1, "Total Stock Cost Value"); Money(ws, row, 2, vm.StockTotalCostValue); row++;
        BoldLabel(ws, row, 1, "Total Stock MRP Value"); Money(ws, row, 2, vm.StockTotalMrpValue);

        Finish(ws, headerRow, lastData, cols);
        return row;
    }
}
