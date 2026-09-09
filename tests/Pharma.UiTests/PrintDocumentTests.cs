using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Pharma.App.Printing;
using Pharma.Core;
using Pharma.Data;

namespace Pharma.UiTests;

/// <summary>
/// Builds each printed document and reads the text back. No printer is involved,
/// so these run anywhere — what they guard is that the statutory details actually
/// reach the paper, and that a document only ever carries the identity it is
/// meant to speak as.
/// </summary>
public class PrintDocumentTests
{
    private static ClinicProfile Clinic() => new()
    {
        Name = "Twinkle Children's Hospital",
        AddressLine = "12 Main Road, Hyderabad",
        Phone = "040-1234567"
    };

    private static PharmacyProfile Pharmacy() => new()
    {
        Name = "Twinkle Pharmacy",
        AddressLine = "12 Main Road, Hyderabad",
        Phone = "040-1234567",
        Gstin = "36ABCDE1234F1Z5",
        DrugLicenceNo = "AP/21B/2024/1234",
        PharmacistName = "S. Rao"
    };

    private static DocumentTheme Theme() => new() { Footer = "Get well soon." };

    private static string TextOf(FlowDocument doc)
        => new TextRange(doc.ContentStart, doc.ContentEnd).Text;

    private static Visit Visit(bool paid = true) => new()
    {
        VisitNo = "V00007",
        TokenNo = 7,
        ScheduledOn = new DateTime(2026, 7, 25, 10, 30, 0),
        Fee = 350m,
        FeePaid = paid,
        FeeReceiptNo = paid ? "RCP00004" : null,
        FeePaidOn = paid ? new DateTime(2026, 7, 25, 10, 35, 0) : null,
        FeePaymentMode = paid ? PaymentMode.Upi : null,
        Diagnosis = "Acute pharyngitis",
        FollowUpOn = new DateTime(2026, 8, 1),
        Patient = new Patient { Name = "Baby Anika", PatientNo = "P00012", Age = 4, Gender = Gender.Female },
        Doctor = new Doctor
        {
            Name = "Dr. A. Kumar", Speciality = "Paediatrics", RegistrationNo = "REG-4471",
            Qualification = "MBBS, MD (Paediatrics)"
        }
    };

    // ── Fee receipt ────────────────────────────────────────────────────────

    [StaFact]
    public void A_fee_receipt_carries_its_number_amount_and_payment_mode()
    {
        var text = TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

        Assert.Contains("CASH RECEIPT", text);
        Assert.Contains("RCP00004", text);
        Assert.Contains("Baby Anika", text);
        Assert.Contains("Dr. A. Kumar", text);
        Assert.Contains("350.00", text);
        Assert.Contains("Upi", text);
        Assert.Contains("Rupees Three Hundred Fifty only", text);
    }

    /// <summary>
    /// The registration gets its own label rather than being run together after
    /// the name, and the degrees sit beside the name — who gave the
    /// consultation is what a receipt is expected to state plainly.
    ///
    /// The speciality is deliberately not a label of its own: it already names
    /// the consultation on the particulars line, and a second copy of it in the
    /// identity block was a field earning nothing.
    /// </summary>
    [StaFact]
    public void A_fee_receipt_labels_the_doctors_registration_and_degrees()
    {
        var doc = FeeReceiptDocument.Build(Visit(), Clinic(), Theme());
        var text = TextOf(doc);

        Assert.Contains("Dr. A. Kumar, MBBS, MD (Paediatrics)", text);
        Assert.Contains("Reg. No", text);
        Assert.Contains("REG-4471", text);

        // Gone from the identity grid, still on the fee line below it.
        var labels = doc.Blocks.OfType<Table>().First().RowGroups.First().Rows
            .SelectMany(r => r.Cells).Select(IdentityCellText).Select(c => c.Label).ToList();

        Assert.DoesNotContain("Speciality", labels);
        Assert.Contains("Consultation fee — Paediatrics", text);
    }

    /// <summary>Two rows, and nothing left blank in them.</summary>
    [StaFact]
    public void The_fee_receipts_identity_block_is_two_full_rows()
    {
        var rows = FeeReceiptDocument.Build(Visit(), Clinic(), Theme())
            .Blocks.OfType<Table>().First().RowGroups.First().Rows;

        Assert.Equal(2, rows.Count);

        var cells = rows.SelectMany(r => r.Cells).Select(IdentityCellText).ToList();
        Assert.All(cells, c => Assert.False(string.IsNullOrWhiteSpace(c.Label),
                                            "an identity cell was left without a label"));
    }

    /// <summary>
    /// The reverse of what this document used to do, at the clinic's request.
    ///
    /// The token — the patient's place in the day's queue — is what the desk and
    /// the parent both used to refer to the visit while they were in the
    /// building, so it goes on the receipt. The visit number and the patient
    /// number come off: both are internal references the parent cannot act on,
    /// and the receipt number is what they are asked to quote.
    /// </summary>
    [StaFact]
    public void A_fee_receipt_carries_the_days_token_and_not_the_internal_numbers()
    {
        var text = TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

        Assert.Contains("Token No", text);
        Assert.Contains("RCP00004", text);

        Assert.DoesNotContain("V00007", text);
        Assert.DoesNotContain("P00012", text);
    }

    /// <summary>
    /// A doctor with nothing on file still prints. The labels stay — a receipt
    /// with a gap where the registration should be is a question the desk gets
    /// asked, and an em dash answers it — but the name must not be left with a
    /// dangling comma where the degrees would have gone.
    /// </summary>
    [StaFact]
    public void A_fee_receipt_still_prints_when_the_doctor_has_no_registration_or_degrees()
    {
        var visit = Visit();
        visit.Doctor.RegistrationNo = null;
        visit.Doctor.Qualification = null;

        var text = TextOf(FeeReceiptDocument.Build(visit, Clinic(), Theme()));

        Assert.Contains("Dr. A. Kumar", text);
        Assert.DoesNotContain("Dr. A. Kumar,", text);
        Assert.DoesNotContain("REG-4471", text);
    }

    [StaFact]
    public void A_paid_first_visit_reads_as_new_and_prints_the_date_its_cover_runs_to()
    {
        var visit = Visit();
        visit.FreeFollowUpUntil = new DateTime(2026, 8, 1);

        var text = TextOf(FeeReceiptDocument.Build(visit, Clinic(), Theme()));

        Assert.Contains("New", text);

        // A date, never a number of days to count from something. This is the
        // line the desk points at when a parent asks if they must pay again.
        Assert.Contains("01 Aug 2026", text);
        Assert.DoesNotContain("7 days", text);
    }

    [StaFact]
    public void A_visit_with_no_cover_prints_no_review_promise()
    {
        var text = TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

        Assert.Contains("New", text);
        Assert.DoesNotContain("Free renewal until", text);
        Assert.DoesNotContain("Renewal free with", text);
    }

    /// <summary>
    /// A return inside the window reads Renew and is receipted for nil — the
    /// amount is stated rather than left blank, because a receipt with no
    /// figure on it invites exactly the question the figure answers. It also
    /// names the receipt the fee was actually paid on, which is what the
    /// patient needs if anyone later asks why they were seen free.
    /// </summary>
    [StaFact]
    public void A_renewal_is_receipted_for_nil_and_reads_renew()
    {
        var paid = Visit();
        var renewal = Visit(paid: false);
        renewal.Fee = 0m;
        renewal.TokenNo = 12;
        renewal.FeeWaivedAgainstVisitId = paid.Id;
        renewal.FeeWaivedAgainstVisit = paid;

        var text = TextOf(FeeReceiptDocument.Build(renewal, Clinic(), Theme()));

        Assert.Contains("CASH RECEIPT", text);
        Assert.Contains("Renew", text);
        Assert.Contains("0.00", text);
        Assert.Contains("Rupees Zero only", text);

        // Carries the number the money was actually taken on, and is given none
        // of its own — a nil receipt burning an RCP number would leave a hole in
        // the day when the collection is reconciled. Same label as any other
        // receipt, so this checks the value rather than the wording.
        var cells = FeeReceiptDocument.Build(renewal, Clinic(), Theme())
            .Blocks.OfType<Table>().First().RowGroups.First().Rows
            .SelectMany(r => r.Cells).Select(IdentityCellText).ToList();

        Assert.Equal("RCP00004", cells.Find(c => c.Label == "Receipt No").Value);

        // A renewal does not open a window of its own, so it must not promise
        // one — and it never reads as a payment that was collected.
        Assert.DoesNotContain("Renewal free with", text);
        Assert.DoesNotContain("Paid by", text);
    }

    [StaFact]
    public void A_reprinted_receipt_is_marked_duplicate()
    {
        Assert.Contains("DUPLICATE", TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme(), isReprint: true)));
        Assert.DoesNotContain("DUPLICATE", TextOf(FeeReceiptDocument.Build(Visit(), Clinic(), Theme())));
    }

    [Theory]
    [InlineData(0, "Rupees Zero only")]
    [InlineData(7, "Rupees Seven only")]
    [InlineData(19, "Rupees Nineteen only")]
    [InlineData(350, "Rupees Three Hundred Fifty only")]
    [InlineData(1120, "Rupees One Thousand One Hundred Twenty only")]
    [InlineData(15334, "Rupees Fifteen Thousand Three Hundred Thirty Four only")]
    [InlineData(250000, "Rupees Two Lakh Fifty Thousand only")]
    public void Amounts_are_written_out_in_indian_words(int amount, string expected)
        => Assert.Equal(expected, FeeReceiptDocument.InWords(amount));

    [Fact]
    public void Paise_are_spelled_out_too()
        => Assert.Equal("Rupees Ninety Three and Eighteen Paise only", FeeReceiptDocument.InWords(93.18m));

    // ── Appointment slip ───────────────────────────────────────────────────

    private static Appointment Appointment() => new()
    {
        AppointmentNo = "APT00003",
        ScheduledOn = new DateTime(2026, 8, 20, 11, 30, 0),
        PatientName = "Baby Anika",
        PatientPhone = "9000000000",
        DoctorName = "Dr. A. Kumar",
        ModuleContext = AppointmentModuleContext.General,
        Reason = "Fever review"
    };

    [StaFact]
    public void An_appointment_slip_carries_its_number_patient_doctor_and_reason()
    {
        var text = TextOf(AppointmentSlipDocument.Build(Appointment(), Clinic(), Theme()));

        Assert.Contains("APT00003", text);
        Assert.Contains("Baby Anika", text);
        Assert.Contains("Dr. A. Kumar", text);
        Assert.Contains("9000000000", text);
        Assert.Contains("Fever review", text);
    }

    // ── Prescription ───────────────────────────────────────────────────────

    [StaFact]
    public void A_prescription_carries_the_doctors_registration_and_every_medicine()
    {
        var visit = Visit();
        visit.Prescription.Add(new PrescriptionItem
        {
            MedicineName = "Paracetamol 250mg", Dosage = "5 ml", Frequency = "1-1-1", Days = 3, Quantity = 1
        });
        visit.Prescription.Add(new PrescriptionItem
        {
            MedicineName = "ORS Powder", Dosage = "1 sachet", Frequency = "SOS", Days = 2, Quantity = 4,
            Instructions = "Dissolve in 200 ml water"
        });

        var doc = PrescriptionPrinter.Build(visit, Clinic(), Theme());
        var text = TextOf(doc);

        Assert.Contains("Twinkle Children's Hospital", text);
        Assert.Contains("Reg. No: REG-4471", text);
        Assert.Contains("Acute pharyngitis", text);
        Assert.Contains("Paracetamol 250mg", text);
        Assert.Contains("ORS Powder", text);
        Assert.Contains("Dissolve in 200 ml water", text);
        Assert.Contains("Review on 01 Aug 2026", text);
    }

    [StaFact]
    public void A_prescription_lists_every_test_the_doctor_requested()
    {
        var visit = Visit();
        visit.DiagnosticRequests.Add(new VisitDiagnosticRequest { TestName = "Complete Blood Picture (CBP/CBC)" });
        visit.DiagnosticRequests.Add(new VisitDiagnosticRequest { TestName = "CRP" });

        var text = TextOf(PrescriptionPrinter.Build(visit, Clinic(), Theme()));

        Assert.Contains("Investigations advised", text);
        Assert.Contains("Complete Blood Picture (CBP/CBC)", text);
        Assert.Contains("CRP", text);
    }

    [StaFact]
    public void A_prescription_with_no_medicines_still_prints()
    {
        var text = TextOf(PrescriptionPrinter.Build(Visit(), Clinic(), Theme()));

        Assert.Contains("No medicines prescribed.", text);
    }

    // ── The clinic/pharmacy split: neither identity carries the other's credentials ──

    [StaFact]
    public void A_prescription_never_carries_the_drug_licence_number()
    {
        // A drug licence is a pharmacy credential. A GST-registered clinic
        // would previously have had the pharmacy's GSTIN and licence printed
        // on a doctor's own document by accident, because both used to share
        // one identity. Neither should ever appear here now.
        var clinic = Clinic();
        clinic.GstRegistered = true;
        clinic.Gstin = "36ABCDE1234F1Z5";

        var text = TextOf(PrescriptionPrinter.Build(Visit(), clinic, Theme()));

        Assert.DoesNotContain("D.L. No", text);
    }

    [StaFact]
    public void A_prescription_shows_the_clinics_own_gstin_only_when_the_clinic_is_registered()
    {
        var registered = Clinic();
        registered.GstRegistered = true;
        registered.Gstin = "36CLINIC1234F1Z5";
        Assert.Contains("36CLINIC1234F1Z5", TextOf(PrescriptionPrinter.Build(Visit(), registered, Theme())));

        var unregistered = Clinic();
        unregistered.GstRegistered = false;
        unregistered.Gstin = "36CLINIC1234F1Z5";
        Assert.DoesNotContain("36CLINIC1234F1Z5", TextOf(PrescriptionPrinter.Build(Visit(), unregistered, Theme())));
    }

    // ── Pharmacy bill ──────────────────────────────────────────────────────

    private static Sale Sale() => new()
    {
        BillNo = "INV00021",
        BillDate = new DateTime(2026, 7, 25, 11, 5, 0),
        CustomerName = "Baby Anika",
        DoctorName = "Dr. A. Kumar",
        PaymentMode = PaymentMode.Cash,
        IsTaxInvoice = true,
        GrossAmount = 1120m,
        TaxableAmount = 1000m,
        CgstAmount = 60m,
        SgstAmount = 60m,
        RoundOff = 0m,
        NetAmount = 1120m,
        Items =
        [
            new SaleItem
            {
                ProductName = "RELENT PLUS SYRUP 60ML", BatchNo = "D260374",
                ExpiryDate = new DateTime(2028, 4, 30), HsnCode = "30049099",
                Quantity = 10, Mrp = 112m, GstRate = 12m,
                TaxableAmount = 1000m, GstAmount = 120m, LineTotal = 1120m
            }
        ]
    };

    [StaFact]
    public void A_bill_shows_batch_expiry_and_hsn_against_every_line()
    {
        var text = TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("INV00021", text);
        Assert.Contains("RELENT PLUS SYRUP 60ML", text);
        Assert.Contains("D260374", text);       // batch — legally required
        Assert.Contains("04/28", text);         // expiry — legally required
        Assert.Contains("30049099", text);      // HSN
    }

    // ── Diagnostic bill ────────────────────────────────────────────────────

    private static DiagnosticBill DiagnosticBill() => new()
    {
        BillNo = "DX00003",
        BillDate = new DateTime(2026, 7, 25, 11, 5, 0),
        PatientName = "Baby Anika",
        PatientNo = "P00012",
        PaymentMode = PaymentMode.Cash,
        Status = DiagnosticBillStatus.Ordered,
        TotalAmount = 450m,
        Discount = 50m,
        FinalAmount = 400m,
        Items =
        [
            new DiagnosticBillItem { TestName = "Complete Blood Picture (CBP/CBC)", Price = 300m, Quantity = 1, Amount = 300m },
            new DiagnosticBillItem { TestName = "ESR", Price = 150m, Quantity = 1, Amount = 150m }
        ]
    };

    [StaFact]
    public void A_diagnostic_bill_carries_no_gst_and_keeps_its_billed_prices()
    {
        var text = TextOf(DiagnosticBillPrinter.Build(DiagnosticBill(), Clinic(), Theme()));

        Assert.Contains("DIAGNOSTIC BILL", text);
        Assert.Contains("DX00003", text);
        Assert.Contains("Complete Blood Picture (CBP/CBC)", text);
        Assert.Contains("ESR", text);
        Assert.Contains("400.00", text);   // final amount, after the discount
        Assert.DoesNotContain("GST", text);
    }

    [StaFact]
    public void A_reprinted_diagnostic_bill_is_marked_duplicate()
        => Assert.Contains("DUPLICATE", TextOf(DiagnosticBillPrinter.Build(DiagnosticBill(), Clinic(), Theme(), isReprint: true)));

    // ── Procedure bill (Pediatrics, Dentist) ─────────────────────────────────

    private static ProcedureBill ProcedureBill() => new()
    {
        BillNo = "PRC00003",
        BillDate = new DateTime(2026, 7, 25, 11, 5, 0),
        PatientName = "Baby Anika",
        PatientNo = "P00012",
        PaymentMode = PaymentMode.Cash,
        Status = ProcedureBillStatus.Ordered,
        TotalAmount = 300m,
        Discount = 50m,
        FinalAmount = 250m,
        Items =
        [
            new ProcedureBillItem { ProcedureName = "BCG (dose 1)", Price = 200m, Quantity = 1, Amount = 200m },
            new ProcedureBillItem { ProcedureName = "Ear piercing", Price = 100m, Quantity = 1, Amount = 100m }
        ]
    };

    [StaFact]
    public void A_procedure_bill_carries_no_gst_and_keeps_its_billed_prices()
    {
        var text = TextOf(ProcedureBillDocument.Build(ProcedureBill(), Clinic(), Theme()));

        Assert.Contains("PROCEDURE BILL", text);
        Assert.Contains("PRC00003", text);
        Assert.Contains("BCG (dose 1)", text);
        Assert.Contains("Ear piercing", text);
        Assert.Contains("250.00", text);
        Assert.DoesNotContain("GST", text);
    }

    [StaFact]
    public void A_reprinted_procedure_bill_is_marked_duplicate()
        => Assert.Contains("DUPLICATE", TextOf(ProcedureBillDocument.Build(ProcedureBill(), Clinic(), Theme(), isReprint: true)));

    // ── Dental payment receipt ────────────────────────────────────────────

    private static DentalCase DentalCaseWithPayments() => new()
    {
        PatientName = "Aarav Rao",
        ProcedureName = "Root Canal",
        ToothNumber = "36",
        BaseCost = 5000m,
        Sittings = [new DentalSitting { AnesthesiaCost = 300m }],
        Payments = [new DentalPayment { Amount = 2000m }]
    };

    private static DentalPayment DentalPayment() => new()
    {
        ReceiptNo = "DPR00002",
        PaidOn = new DateTime(2026, 7, 25, 11, 5, 0),
        Amount = 2000m,
        PaymentMode = PaymentMode.Cash
    };

    [StaFact]
    public void A_dental_receipt_shows_the_payment_and_the_cases_remaining_balance()
    {
        var text = TextOf(DentalReceiptDocument.Build(DentalPayment(), DentalCaseWithPayments(), Clinic(), Theme()));

        Assert.Contains("DPR00002", text);
        Assert.Contains("Aarav Rao", text);
        Assert.Contains("Root Canal", text);
        Assert.Contains("2000.00", text);
        // Total 5300 (5000 base + 300 anesthesia), paid 2000 -> balance 3300.
        Assert.Contains("3300.00", text);
    }

    // ── Pathology lab report ──────────────────────────────────────────────

    private static LabOrder LabOrder() => new()
    {
        OrderNo = "LAB00002",
        OrderDate = new DateTime(2026, 7, 25, 11, 5, 0),
        PatientName = "Baby Anika",
        PatientNo = "P00012",
        ReferredBy = "Dr. A. Kumar",
        PaymentMode = PaymentMode.Cash,
        Status = LabOrderStatus.Verified,
        TotalAmount = 450m,
        Discount = 50m,
        FinalAmount = 400m,
        Reports =
        [
            new LabOrderReport
            {
                ReportName = "CBP", Price = 450m, Amount = 450m,
                Results =
                [
                    new LabResult
                    {
                        AnalyteName = "Hemoglobin", Units = "g/dL", ResultValue = "9.2",
                        ReferenceRangeDisplay = "12 – 16", Flag = LabResultFlag.Low,
                        VerifiedOn = new DateTime(2026, 7, 25, 12, 0, 0), VerifiedBy = "Dr. Rao"
                    },
                    new LabResult
                    {
                        AnalyteName = "WBC Count", Units = "/cumm", ResultValue = "7200",
                        ReferenceRangeDisplay = "4000 – 11000", Flag = LabResultFlag.Normal,
                        VerifiedOn = new DateTime(2026, 7, 25, 12, 0, 0), VerifiedBy = "Dr. Rao"
                    }
                ]
            }
        ]
    };

    [StaFact]
    public void A_lab_report_carries_its_analytes_results_and_flags_the_abnormal_one()
    {
        var text = TextOf(LabReportDocument.Build(LabOrder(), Clinic(), Theme()));

        Assert.Contains("LABORATORY REPORT", text);
        Assert.Contains("LAB00002", text);
        Assert.Contains("Baby Anika", text);
        Assert.Contains("CBP", text);
        Assert.Contains("Hemoglobin", text);
        Assert.Contains("9.2", text);
        Assert.Contains("LOW", text);
        Assert.Contains("WBC Count", text);
        Assert.Contains("400.00", text);
        Assert.Contains("Verified by Dr. Rao", text);
    }

    [StaFact]
    public void A_normal_result_carries_no_flag_text()
    {
        var text = TextOf(LabReportDocument.Build(LabOrder(), Clinic(), Theme()));

        // The Normal-flagged WBC Count row prints no flag word beside it —
        // only the Low-flagged Hemoglobin row does.
        Assert.DoesNotContain("NORMAL", text);
    }

    [StaFact]
    public void A_lab_report_is_black_ink_on_a_white_page()
        => AssertPrintSafe(LabReportDocument.Build(LabOrder(), Clinic(), Theme()));

    [StaFact]
    public void Every_paragraph_on_the_lab_report_has_its_own_brush()
        => AssertEveryParagraphHasAnExplicitBrush(LabReportDocument.Build(LabOrder(), Clinic(), Theme()));

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_lab_report()
        => AssertDateImmediatelyFollowedByTime(LabReportDocument.Build(LabOrder(), Clinic(), Theme()));

    [StaFact]
    public void A_bill_shows_the_licences_and_the_gst_split()
    {
        var text = TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

        Assert.Contains("36ABCDE1234F1Z5", text);
        Assert.Contains("AP/21B/2024/1234", text);
        Assert.Contains("GST SUMMARY", text);
        Assert.Contains("CGST", text);
        Assert.Contains("SGST", text);
        Assert.Contains("Rupees One Thousand One Hundred Twenty only", text);
        Assert.Contains("S. Rao", text);
    }

    // ── GST registered, or not ─────────────────────────────────────────────

    [StaFact]
    public void An_unregistered_pharmacy_issues_a_plain_invoice()
    {
        var sale = Sale();
        sale.IsTaxInvoice = false;

        // No tax was charged, so none is shown.
        sale.CgstAmount = sale.SgstAmount = 0m;
        sale.TaxableAmount = sale.NetAmount;
        foreach (var item in sale.Items) { item.GstRate = 0m; item.GstAmount = 0m; }

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

        Assert.Contains("INVOICE", text);
        Assert.DoesNotContain("TAX INVOICE", text);

        // Claiming a GSTIN without being registered would be a false statement.
        Assert.DoesNotContain("GSTIN", text);
        Assert.DoesNotContain("GST SUMMARY", text);
        Assert.DoesNotContain("CGST", text);

        // The drug licence is still required, and the medicine details still show.
        // It prints as its own labelled field in the identity grid now, the
        // same way Patient/Date/Time do — label and value in separate lines,
        // not one combined "D.L. No: …" line the way it used to.
        Assert.Contains("D.L. No", text);
        Assert.Contains("AP/21B/2024/1234", text);
        Assert.Contains("D260374", text);
        Assert.Contains("INV00021", text);
    }

    [StaFact]
    public void A_registered_pharmacy_issues_a_tax_invoice()
    {
        var sale = Sale();
        sale.IsTaxInvoice = true;

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("GSTIN: 36ABCDE1234F1Z5", text);
        Assert.Contains("GST SUMMARY", text);
        Assert.Contains("CGST", text);
    }

    [StaFact]
    public void A_registered_pharmacy_selling_zero_rated_goods_still_issues_a_tax_invoice()
    {
        // Being a tax invoice is about registration, not about whether this
        // particular basket happened to carry tax.
        var sale = Sale();
        sale.IsTaxInvoice = true;
        sale.CgstAmount = sale.SgstAmount = 0m;
        foreach (var item in sale.Items) { item.GstRate = 0m; item.GstAmount = 0m; }

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

        Assert.Contains("TAX INVOICE", text);
        Assert.Contains("GSTIN", text);
    }

    [StaFact]
    public void A_reprinted_bill_is_marked_duplicate()
    {
        Assert.Contains("DUPLICATE", TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme(), isReprint: true)));
        Assert.DoesNotContain("DUPLICATE", TextOf(BillPrinter.Build(Sale(), Pharmacy(), Theme())));
    }

    [StaFact]
    public void A_bill_prints_even_when_the_pharmacy_details_were_never_filled_in()
    {
        // A clinic that has not visited Settings yet must still be able to bill.
        var text = TextOf(BillPrinter.Build(Sale(), new PharmacyProfile { Gstin = "" }, new DocumentTheme()));

        Assert.Contains("INV00021", text);
        Assert.Contains("RELENT PLUS SYRUP 60ML", text);
        Assert.DoesNotContain("GSTIN:", text);
    }

    [StaFact]
    public void A_long_bill_keeps_every_line()
    {
        var sale = Sale();
        sale.Items.Clear();

        for (var i = 1; i <= 60; i++)
        {
            sale.Items.Add(new SaleItem
            {
                ProductName = $"MEDICINE {i:D2}", BatchNo = $"B{i:D4}",
                ExpiryDate = new DateTime(2028, 1, 31), HsnCode = "30049099",
                Quantity = 1, Mrp = 10m, GstRate = 5m,
                TaxableAmount = 9.52m, GstAmount = 0.48m, LineTotal = 10m
            });
        }

        var text = TextOf(BillPrinter.Build(sale, Pharmacy(), Theme()));

        Assert.Contains("MEDICINE 01", text);
        Assert.Contains("MEDICINE 60", text);
    }

    // ── Print-safe palette ─────────────────────────────────────────────────
    //
    // Regression guard for a bug where the preview page picked up a dark
    // background from somewhere while the ink stayed dark too, making a
    // receipt unreadable until the text was selected. Every document must
    // carry its own literal white/black — never null, never a theme colour —
    // so this can never come back silently.

    private static void AssertPrintSafe(FlowDocument doc)
    {
        var background = Assert.IsType<SolidColorBrush>(doc.Background);
        Assert.Equal(Colors.White, background.Color);

        var foreground = Assert.IsType<SolidColorBrush>(doc.Foreground);
        Assert.Equal(Colors.Black, foreground.Color);
    }

    [StaFact]
    public void A_fee_receipt_is_black_ink_on_a_white_page()
        => AssertPrintSafe(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void A_prescription_is_black_ink_on_a_white_page()
        => AssertPrintSafe(PrescriptionPrinter.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void A_bill_is_black_ink_on_a_white_page()
        => AssertPrintSafe(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

    // ── Every paragraph carries its own ink ─────────────────────────────────
    //
    // A stronger guard than AssertPrintSafe above, added after a real bug:
    // the identity header's clinic name and document title printed in the
    // app's teal accent colour and the contact line printed blue and
    // underlined, on a real clinic's machine, because two Paragraphs in
    // DocumentBuilder.IdentityBlock left Foreground unset and inherited
    // whatever the hosting window's resources supplied instead of the
    // literal black AssertPrintSafe checks at the document level. That
    // check passed the whole time — doc.Foreground was correctly black, the
    // paragraph just did not inherit it the way it was assumed to. This
    // walks every actual Paragraph a built document contains and fails if
    // any one of them was left to inherit rather than told what colour it is.

    private static IEnumerable<Paragraph> AllParagraphs(FlowDocument doc)
        => doc.Blocks.SelectMany(AllParagraphsIn);

    private static IEnumerable<Paragraph> AllParagraphsIn(Block block)
    {
        switch (block)
        {
            case Paragraph p:
                yield return p;
                break;

            case Section s:
                foreach (var inner in s.Blocks.SelectMany(AllParagraphsIn))
                    yield return inner;
                break;

            case Table t:
                foreach (var inner in t.RowGroups
                             .SelectMany(g => g.Rows)
                             .SelectMany(r => r.Cells)
                             .SelectMany(c => c.Blocks)
                             .SelectMany(AllParagraphsIn))
                    yield return inner;
                break;
        }
    }

    // The two literal inks DocumentBuilder prints with (PrintForeground and
    // PrintSecondaryForeground). Duplicated here rather than referenced,
    // because DocumentBuilder is internal to Pharma.App and this is the
    // check that must not pass by accident: asserting only "some
    // SolidColorBrush" would have let the real bug through, since the
    // theme's accent green and a Hyperlink's default blue are each a
    // SolidColorBrush too — checking the exact colour is the point.
    private static readonly Color BlackInk = Color.FromRgb(0x00, 0x00, 0x00);
    private static readonly Color MutedInk = Color.FromRgb(0x33, 0x33, 0x33);

    private static void AssertEveryParagraphHasAnExplicitBrush(FlowDocument doc)
    {
        var paragraphs = AllParagraphs(doc).ToList();
        Assert.NotEmpty(paragraphs);

        foreach (var paragraph in paragraphs)
        {
            var brush = Assert.IsType<SolidColorBrush>(paragraph.Foreground);
            Assert.True(brush.Color == BlackInk || brush.Color == MutedInk,
                $"A paragraph printed in {brush.Color}, not the black or muted grey every " +
                $"document is meant to be. Text: \"{new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim()}\"");
        }
    }

    [StaFact]
    public void Every_paragraph_on_the_receipt_has_its_own_brush()
        => AssertEveryParagraphHasAnExplicitBrush(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void Every_paragraph_on_the_prescription_has_its_own_brush()
    {
        var visit = Visit();
        visit.Prescription.Add(new PrescriptionItem
        {
            MedicineName = "Paracetamol 250mg", Dosage = "5 ml", Frequency = "1-1-1", Days = 3, Quantity = 1
        });

        AssertEveryParagraphHasAnExplicitBrush(PrescriptionPrinter.Build(visit, Clinic(), Theme()));
    }

    [StaFact]
    public void Every_paragraph_on_the_bill_has_its_own_brush()
        => AssertEveryParagraphHasAnExplicitBrush(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

    [StaFact]
    public void Every_paragraph_on_the_diagnostic_bill_has_its_own_brush()
        => AssertEveryParagraphHasAnExplicitBrush(DiagnosticBillPrinter.Build(DiagnosticBill(), Clinic(), Theme()));

    // ── Date and time sit next to each other ────────────────────────────────
    //
    // Structural, not text-search: the fee receipt and the prescription
    // already contained both words before this changed, just in different
    // rows of the identity grid — searching the flattened text for "Date"
    // and "Time" would have passed before the fix as easily as after it.
    // This reads the actual Table the identity grid is built as and checks
    // that the cell right after "Date" is "Time", in the same row.

    private static (string Label, string Value) IdentityCellText(TableCell cell)
    {
        var paragraphs = cell.Blocks.OfType<Paragraph>().ToList();
        string Text(int i) => paragraphs.Count > i
            ? new TextRange(paragraphs[i].ContentStart, paragraphs[i].ContentEnd).Text.Trim()
            : "";

        return (Text(0), Text(1));
    }

    /// <summary>The identity grid is always the first Table directly under
    /// the document's own Blocks — the header's Section (and any Table
    /// nested inside it for a logo) comes before it, but is never itself a
    /// direct child of doc.Blocks.</summary>
    private static TableRow FirstIdentityRow(FlowDocument doc)
        => doc.Blocks.OfType<Table>().First().RowGroups.First().Rows.First();

    private static void AssertDateImmediatelyFollowedByTime(FlowDocument doc)
    {
        var cells = FirstIdentityRow(doc).Cells.Select(IdentityCellText).ToList();
        var dateIndex = cells.FindIndex(c => c.Label == "Date");

        Assert.True(dateIndex >= 0, "No 'Date' cell found in the identity grid's first row.");
        Assert.True(dateIndex + 1 < cells.Count, "'Date' was the last cell in the row — no room for 'Time' beside it.");
        Assert.Equal("Time", cells[dateIndex + 1].Label);
    }

    /// <summary>
    /// The receipt goes one better than the other two documents and puts the
    /// date and the time in a single field — they are one fact, since nobody
    /// reads the hour without the day. The sibling tests below still hold the
    /// prescription and the bill to keeping their two cells side by side.
    /// </summary>
    [StaFact]
    public void Date_and_time_are_one_field_on_the_fee_receipt()
    {
        var cells = FirstIdentityRow(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()))
            .Cells.Select(IdentityCellText).ToList();

        var when = cells.Find(c => c.Label == "Date & time");

        // Not pinned to a separator: the running culture decides whether a
        // date prints with slashes or dashes, and that is not what this is about.
        Assert.NotNull(when.Label);
        Assert.Matches(@"25.07.2026", when.Value);
        Assert.Contains("10:35", when.Value);

        // Split back apart, the pair would be two of the three columns and
        // crowd out everything else the row has to carry.
        Assert.DoesNotContain(cells, c => c.Label == "Date");
        Assert.DoesNotContain(cells, c => c.Label == "Time");
    }

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_prescription()
        => AssertDateImmediatelyFollowedByTime(PrescriptionPrinter.Build(Visit(), Clinic(), Theme()));

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_pharmacy_bill()
        => AssertDateImmediatelyFollowedByTime(BillPrinter.Build(Sale(), Pharmacy(), Theme()));

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_diagnostic_bill()
        => AssertDateImmediatelyFollowedByTime(DiagnosticBillPrinter.Build(DiagnosticBill(), Clinic(), Theme()));

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_appointment_slip()
        => AssertDateImmediatelyFollowedByTime(AppointmentSlipDocument.Build(Appointment(), Clinic(), Theme()));

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_procedure_bill()
        => AssertDateImmediatelyFollowedByTime(ProcedureBillDocument.Build(ProcedureBill(), Clinic(), Theme()));

    [StaFact]
    public void Date_and_time_are_adjacent_on_the_dental_receipt()
        => AssertDateImmediatelyFollowedByTime(DentalReceiptDocument.Build(DentalPayment(), DentalCaseWithPayments(), Clinic(), Theme()));

    // ── Configurable print font (Settings → Reports) ────────────────────────

    [StaFact]
    public void With_no_print_font_configured_the_document_keeps_the_compiled_in_default()
    {
        var doc = FeeReceiptDocument.Build(Visit(), Clinic(), Theme());
        Assert.Equal("Segoe UI", doc.FontFamily.Source);
    }

    [StaFact]
    public void A_configured_print_font_family_is_applied_to_the_document()
    {
        var theme = new DocumentTheme { Footer = "x", PrintFontFamily = "Georgia" };
        var doc = FeeReceiptDocument.Build(Visit(), Clinic(), theme);

        Assert.Equal("Georgia", doc.FontFamily.Source);
    }

    [StaFact]
    public void A_print_font_size_delta_shifts_every_paragraphs_size_by_the_same_amount()
    {
        var plain = AllParagraphs(FeeReceiptDocument.Build(Visit(), Clinic(), Theme()))
            .Select(p => p.FontSize).OrderBy(s => s).ToList();
        var bigger = AllParagraphs(FeeReceiptDocument.Build(Visit(), Clinic(), new DocumentTheme { Footer = "x", PrintFontSizeDelta = 3 }))
            .Select(p => p.FontSize).OrderBy(s => s).ToList();

        Assert.Equal(plain.Count, bigger.Count);
        for (var i = 0; i < plain.Count; i++)
            Assert.Equal(plain[i] + 3, bigger[i], precision: 3);
    }

    [StaFact]
    public void This_applies_to_the_pharmacy_bill_and_diagnostic_bill_too()
    {
        var theme = new DocumentTheme { Footer = "x", PrintFontFamily = "Verdana", PrintFontSizeDelta = 2 };

        var bill = BillPrinter.Build(Sale(), Pharmacy(), theme);
        Assert.Equal("Verdana", bill.FontFamily.Source);

        var diagnostic = DiagnosticBillPrinter.Build(DiagnosticBill(), Clinic(), theme);
        Assert.Equal("Verdana", diagnostic.FontFamily.Source);
    }

    // ── Title font — the clinic/pharmacy name, separate from the print font ──

    /// <summary>The clinic/pharmacy name is always the first Paragraph a
    /// built document contains — the first block of the header, which is
    /// itself the first thing every printer adds.</summary>
    private static Paragraph TitleParagraph(FlowDocument doc) => AllParagraphs(doc).First();

    [StaFact]
    public void With_no_title_font_configured_the_name_inherits_the_print_font()
    {
        var theme = new DocumentTheme { Footer = "x", PrintFontFamily = "Georgia" };
        var doc = FeeReceiptDocument.Build(Visit(), Clinic(), theme);

        // Not set locally on the paragraph — it falls through to the
        // document's own FontFamily, which ApplyPrintSettings already set.
        Assert.Equal("Georgia", doc.FontFamily.Source);
        Assert.True(TitleParagraph(doc).ReadLocalValue(TextElement.FontFamilyProperty) == DependencyProperty.UnsetValue);
    }

    [StaFact]
    public void A_configured_title_font_is_applied_to_the_name_only()
    {
        var theme = new DocumentTheme { Footer = "x", PrintFontFamily = "Georgia", TitleFontFamily = "Playfair Display" };
        var doc = FeeReceiptDocument.Build(Visit(), Clinic(), theme);

        var title = TitleParagraph(doc);
        Assert.Equal("Playfair Display", title.FontFamily.Source);

        // Everything else keeps printing in the general print font — the
        // title font is not a document-wide override.
        var others = AllParagraphs(doc).Skip(1).ToList();
        Assert.NotEmpty(others);
        Assert.All(others, p => Assert.True(
            p.ReadLocalValue(TextElement.FontFamilyProperty) == DependencyProperty.UnsetValue,
            "A paragraph other than the title carries its own FontFamily; the title font would have leaked."));
    }

    [StaFact]
    public void A_title_size_delta_stacks_on_top_of_the_general_print_size_delta_on_the_name_only()
    {
        var plainTitleSize = TitleParagraph(FeeReceiptDocument.Build(Visit(), Clinic(), Theme())).FontSize;

        var theme = new DocumentTheme { Footer = "x", PrintFontSizeDelta = 2, TitleFontSizeDelta = 5 };
        var doc = FeeReceiptDocument.Build(Visit(), Clinic(), theme);

        // Both deltas landed on the name (+2 general, +5 title-only = +7).
        Assert.Equal(plainTitleSize + 7, TitleParagraph(doc).FontSize, precision: 3);

        // A body paragraph only got the general +2, not the title's own +5.
        var bodyBefore = AllParagraphs(FeeReceiptDocument.Build(Visit(), Clinic(), Theme())).Skip(1).First().FontSize;
        var bodyAfter = AllParagraphs(doc).Skip(1).First().FontSize;
        Assert.Equal(bodyBefore + 2, bodyAfter, precision: 3);
    }

    // ── Drug licence number as its own labelled field ───────────────────────

    [StaFact]
    public void The_drug_licence_number_prints_as_its_own_labelled_field_not_folded_into_the_header()
    {
        var doc = BillPrinter.Build(Sale(), Pharmacy(), Theme());
        var text = TextOf(doc);

        // Not the old combined "D.L. No: …" line in the header text.
        Assert.DoesNotContain("D.L. No:", text);

        // Instead, its own label/value cell in the identity grid, same as Patient.
        var cells = FirstIdentityRow(doc).Cells
            .Concat(doc.Blocks.OfType<Table>().First().RowGroups.First().Rows.Skip(1).SelectMany(r => r.Cells))
            .Select(IdentityCellText)
            .ToList();

        Assert.Contains(cells, c => c.Label == "D.L. No" && c.Value == "AP/21B/2024/1234");
    }

    // ── Vaccination history ─────────────────────────────────────────────────

    private static Patient VaccinatedPatient() => new()
    {
        Name = "Baby Anika", PatientNo = "P00012", Age = 2, Gender = Gender.Female, GuardianName = "R. Kumar"
    };

    private static List<VaccinationRecord> VaccinationRecords() =>
    [
        new()
        {
            VaccineName = "BCG", DoseNumber = 1, GivenOn = new DateTime(2024, 3, 1), BatchNo = "B001", SiteOfInjection = "Left arm",
            ProductName = "BCG Vaccine IP", Manufacturer = "Serum Institute"
        },
        new() { VaccineName = "OPV", DoseNumber = 1, GivenOn = new DateTime(2024, 4, 12), BatchNo = "B002", SiteOfInjection = "Oral" }
    ];

    [StaFact]
    public void A_vaccination_history_document_lists_every_dose_for_the_patient()
    {
        var text = TextOf(VaccinationHistoryDocument.Build(VaccinatedPatient(), VaccinationRecords(), Clinic(), Theme()));

        Assert.Contains("VACCINATION RECORD", text);
        Assert.Contains("Baby Anika", text);
        Assert.Contains("P00012", text);
        Assert.Contains("BCG", text);
        Assert.Contains("B001", text);
        Assert.Contains("OPV", text);
        Assert.Contains("B002", text);
    }

    /// <summary>The type (from Vaccine Master) and the actual brand given
    /// (from Pharmacy) are two different facts — the printed record must
    /// carry both, not just the clinical type.</summary>
    [StaFact]
    public void A_vaccination_history_document_shows_the_brand_given_alongside_the_vaccine_type()
    {
        var text = TextOf(VaccinationHistoryDocument.Build(VaccinatedPatient(), VaccinationRecords(), Clinic(), Theme()));

        Assert.Contains("BRAND GIVEN", text);
        Assert.Contains("BCG Vaccine IP", text);
        Assert.Contains("Serum Institute", text);
    }

    [StaFact]
    public void A_vaccination_history_document_with_no_doses_says_so_rather_than_printing_a_blank_table()
    {
        var text = TextOf(VaccinationHistoryDocument.Build(VaccinatedPatient(), [], Clinic(), Theme()));

        Assert.Contains("No doses recorded yet.", text);
    }

    [StaFact]
    public void A_vaccination_history_document_is_black_ink_on_a_white_page()
        => AssertPrintSafe(VaccinationHistoryDocument.Build(VaccinatedPatient(), VaccinationRecords(), Clinic(), Theme()));
}
