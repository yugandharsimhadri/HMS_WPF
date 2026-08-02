using Microsoft.EntityFrameworkCore;
using Pharma.Core;

namespace Pharma.Data;

/// <summary>OPD: patients, doctors and the single Visit record that covers
/// booking, queue and consultation.</summary>
public class OpdService(IDbContextFactory<AppDbContext> factory)
{
    // ── Patients ───────────────────────────────────────────────────────────

    /// <summary>
    /// Finds patients by name, patient number or phone. A phone number is matched
    /// on its digits alone, so "98765 00011", "+91 9876500011" and "9876500011"
    /// all return the same family — and all of them, not just the first.
    /// </summary>
    public async Task<List<Patient>> SearchPatientsAsync(string? term, int take = 50)
    {
        using var log = AppLog.Enter(nameof(SearchPatientsAsync), $"term='{term}' take={take}");

        if (LooksLikePhone(term))
        {
            var family = await GetPatientsByPhoneAsync(term);

            if (family.Count > 0)
            {
                log.Ok($"{family.Count} on this phone number");
                return family.Take(take).ToList();
            }
        }

        await using var db = await factory.CreateDbContextAsync();
        var q = db.Patients.AsNoTracking().Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(term))
        {
            // Like, not Contains — Contains becomes instr(), which is case
            // sensitive, so a name typed in lower case found nothing.
            var pattern = $"%{term.Trim()}%";

            q = q.Where(p => EF.Functions.Like(p.Name, pattern)
                          || EF.Functions.Like(p.Phone, pattern)
                          || EF.Functions.Like(p.PatientNo, pattern));
        }

        var found = await q.OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync();

        log.Ok($"{found.Count} match(es)");
        return found;
    }

    /// <summary>Digits and phone punctuation only, and enough of them to be a number.</summary>
    public static bool LooksLikePhone(string? term)
        => !string.IsNullOrWhiteSpace(term)
           && term.All(c => char.IsDigit(c) || c is ' ' or '-' or '+' or '(' or ')')
           && term.Count(char.IsDigit) >= 6;

    /// <summary>
    /// Everyone registered on one phone number. A family shares a number, so a
    /// paediatric clinic routinely has three or four children behind one contact.
    /// </summary>
    public async Task<List<Patient>> GetPatientsByPhoneAsync(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return [];

        var digits = Digits(phone);
        if (digits.Length < 6) return [];

        await using var db = await factory.CreateDbContextAsync();

        // Numbers get stored with spaces, dashes or a +91, so compare on digits.
        var candidates = await db.Patients
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Phone != "")
            .ToListAsync();

        return candidates
            .Where(p => Digits(p.Phone).EndsWith(digits, StringComparison.Ordinal)
                     || digits.EndsWith(Digits(p.Phone), StringComparison.Ordinal))
            .OrderBy(p => p.Name)
            .ToList();
    }

    private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

    public async Task<Patient> SavePatientAsync(Patient patient)
    {
        using var log = AppLog.Enter(
            nameof(SavePatientAsync),
            $"id={patient.Id} name='{patient.Name}' phone={patient.Phone} age={patient.Age}");

        await using var db = await factory.CreateDbContextAsync();

        if (patient.Id != Guid.Empty && await db.Patients.AnyAsync(p => p.Id == patient.Id))
        {
            db.Patients.Update(patient);
        }
        else
        {
            patient.PatientNo = await NumberService.NextAsync(db, NumberService.Patient);
            db.Patients.Add(patient);
        }

        await db.SaveChangesAsync();

        log.Ok($"{patient.PatientNo} id={patient.Id}");
        return patient;
    }

    /// <summary>Every visit this patient has made, newest first.</summary>
    public async Task<List<Visit>> GetPatientHistoryAsync(Guid patientId)
    {
        using var log = AppLog.Enter(nameof(GetPatientHistoryAsync), $"patient={patientId}");

        await using var db = await factory.CreateDbContextAsync();

        var visits = await db.Visits
            .AsNoTracking()
            .Include(v => v.Doctor)
            .Include(v => v.Prescription)
            .Where(v => !v.IsDeleted && v.PatientId == patientId)
            .OrderByDescending(v => v.ScheduledOn)
            .ToListAsync();

        log.Ok($"{visits.Count} visit(s)");
        return visits;
    }

    /// <summary>
    /// Finds a visit by its number or receipt number, whatever date it was on.
    /// This is how a receipt is reprinted for someone who lost theirs months ago.
    /// </summary>
    public async Task<List<Visit>> SearchVisitsAsync(string? term, int take = 100)
    {
        using var log = AppLog.Enter(nameof(SearchVisitsAsync), $"term='{term}' take={take}");

        await using var db = await factory.CreateDbContextAsync();
        var q = db.Visits.AsNoTracking().Include(v => v.Patient).Include(v => v.Doctor).Where(v => !v.IsDeleted);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{term.Trim()}%";

            q = q.Where(v => EF.Functions.Like(v.VisitNo, pattern)
                          || (v.FeeReceiptNo != null && EF.Functions.Like(v.FeeReceiptNo, pattern))
                          || EF.Functions.Like(v.Patient.Name, pattern)
                          || EF.Functions.Like(v.Patient.Phone, pattern));
        }

        var visits = await q.OrderByDescending(v => v.ScheduledOn).Take(take).ToListAsync();

        log.Ok($"{visits.Count} visit(s)");
        return visits;
    }

    /// <summary>Soft-deletes a patient. Refused while visits still reference them.</summary>
    public async Task<string?> DeletePatientAsync(Guid patientId)
    {
        using var log = AppLog.Enter(nameof(DeletePatientAsync), $"patient={patientId}");

        await using var db = await factory.CreateDbContextAsync();

        if (await db.Visits.AnyAsync(v => v.PatientId == patientId && !v.IsDeleted))
        {
            log.Skip("refused — the patient has visits on record");
            return "This patient has visits on record and cannot be removed.";
        }

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId);

        if (patient is null)
        {
            log.Skip("not found");
            return "Patient not found.";
        }

        patient.IsDeleted = true;
        await db.SaveChangesAsync();

        log.Ok($"removed {patient.PatientNo} '{patient.Name}'");
        return null;
    }

    // ── Doctors ────────────────────────────────────────────────────────────

    public async Task<List<Doctor>> GetDoctorsAsync()
    {
        using var log = AppLog.Enter(nameof(GetDoctorsAsync));

        await using var db = await factory.CreateDbContextAsync();

        var doctors = await db.Doctors.AsNoTracking().Where(d => !d.IsDeleted && d.IsActive)
                                      .OrderBy(d => d.Name).ToListAsync();

        log.Ok($"{doctors.Count} active");
        return doctors;
    }

    public async Task SaveDoctorAsync(Doctor doctor)
    {
        using var log = AppLog.Enter(
            nameof(SaveDoctorAsync), $"id={doctor.Id} name='{doctor.Name}' fee={doctor.ConsultationFee}");

        await using var db = await factory.CreateDbContextAsync();

        if (doctor.Id != Guid.Empty && await db.Doctors.AnyAsync(d => d.Id == doctor.Id))
            db.Doctors.Update(doctor);
        else
            db.Doctors.Add(doctor);

        await db.SaveChangesAsync();
        log.Ok($"saved id={doctor.Id}");
    }

    // ── Visits ─────────────────────────────────────────────────────────────

    public async Task<List<Visit>> GetVisitsAsync(DateTime date)
    {
        using var log = AppLog.Enter(nameof(GetVisitsAsync), $"date={date:yyyy-MM-dd}");

        await using var db = await factory.CreateDbContextAsync();
        var from = date.Date;
        var to = from.AddDays(1);

        var visits = await db.Visits
            .AsNoTracking()
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .Where(v => !v.IsDeleted && v.ScheduledOn >= from && v.ScheduledOn < to)
            .OrderBy(v => v.TokenNo)
            .ToListAsync();

        log.Ok($"{visits.Count} visit(s)");
        return visits;
    }

    /// <summary>Visits across a date range, for trend reports — the OPD queue
    /// itself still uses the single-day <see cref="GetVisitsAsync(DateTime)"/>
    /// above, ordered for token display rather than a trend. No Patient/Doctor
    /// include here: a trend only ever sums Fee/FeePaid, never displays a name.</summary>
    public async Task<List<Visit>> GetVisitsAsync(DateTime from, DateTime to)
    {
        using var log = AppLog.Enter(nameof(GetVisitsAsync), $"from={from:yyyy-MM-dd} to={to:yyyy-MM-dd}");

        await using var db = await factory.CreateDbContextAsync();
        var start = from.Date;
        var end = to.Date.AddDays(1);

        var visits = await db.Visits
            .AsNoTracking()
            .Where(v => !v.IsDeleted && v.ScheduledOn >= start && v.ScheduledOn < end)
            .OrderBy(v => v.ScheduledOn)
            .ToListAsync();

        log.Ok($"{visits.Count} visit(s)");
        return visits;
    }

    public async Task<Visit?> GetVisitAsync(Guid id)
    {
        using var log = AppLog.Enter(nameof(GetVisitAsync), $"visit={id}");

        await using var db = await factory.CreateDbContextAsync();

        var visit = await db.Visits
            .AsNoTracking()
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .Include(v => v.Prescription)
            .Include(v => v.DiagnosticRequests)
            .FirstOrDefaultAsync(v => v.Id == id);

        log.Ok(visit is null
            ? "not found"
            : $"{visit.VisitNo} token={visit.TokenNo} '{visit.Patient.Name}' " +
              $"status={visit.Status} rx={visit.Prescription.Count}");

        return visit;
    }

    /// <summary>Books a visit and allocates the next token for that day.</summary>
    public async Task<Visit> BookVisitAsync(Guid patientId, Guid doctorId, DateTime scheduledOn, string? complaint, decimal fee)
    {
        using var log = AppLog.Enter(
            nameof(BookVisitAsync),
            $"patient={patientId} doctor={doctorId} on={scheduledOn:yyyy-MM-dd HH:mm} fee={fee}");

        await using var db = await factory.CreateDbContextAsync();

        var from = scheduledOn.Date;
        var to = from.AddDays(1);
        var lastToken = await db.Visits
            .Where(v => v.ScheduledOn >= from && v.ScheduledOn < to)
            .MaxAsync(v => (int?)v.TokenNo) ?? 0;

        var visit = new Visit
        {
            VisitNo = await NumberService.NextAsync(db, NumberService.Visit),
            TokenNo = lastToken + 1,
            PatientId = patientId,
            DoctorId = doctorId,
            ScheduledOn = scheduledOn,
            Complaint = complaint,
            Fee = fee,
            Status = VisitStatus.Booked
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        AppLog.Info($"Visit {visit.VisitNo} booked, token {visit.TokenNo} for {scheduledOn:dd MMM yyyy HH:mm}.");

        log.Ok($"{visit.VisitNo} id={visit.Id} token={visit.TokenNo}");
        return visit;
    }

    public async Task SetStatusAsync(Guid visitId, VisitStatus status)
    {
        using var log = AppLog.Enter(nameof(SetStatusAsync), $"visit={visitId} to={status}");

        await using var db = await factory.CreateDbContextAsync();
        var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit is null)
        {
            log.Skip("visit not found");
            return;
        }

        var was = visit.Status;
        visit.Status = status;
        await db.SaveChangesAsync();

        log.Ok($"{visit.VisitNo} {was} → {status}");
    }

    /// <summary>
    /// Records the consultation fee and allocates a receipt number. Collecting
    /// twice is a no-op so a double click cannot burn a number or restate the date.
    /// </summary>
    /// <param name="amount">
    /// What was actually taken, when it differs from what was quoted at booking
    /// — a follow-up seen at half fee, a rounding down, a family concession.
    /// Null keeps the booked fee. The receipt must say what changed hands, so
    /// this writes the visit's fee as well as the receipt.
    /// </param>
    public async Task<Visit?> CollectFeeAsync(
        Guid visitId, PaymentMode mode = PaymentMode.Cash, decimal? amount = null, string? transactionNo = null)
    {
        using var log = AppLog.Enter(nameof(CollectFeeAsync), $"visit={visitId} mode={mode} amount={amount}");

        await using var db = await factory.CreateDbContextAsync();

        var visit = await db.Visits
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit is null)
        {
            log.Skip("visit not found");
            return null;
        }

        if (visit.FeePaid)
        {
            log.Skip($"{visit.VisitNo} already paid on receipt {visit.FeeReceiptNo}");
            return visit;
        }

        // A negative fee is not a concession, it is a typo, and it would print a
        // receipt the clinic owes money on.
        if (amount is { } corrected && corrected >= 0 && corrected != visit.Fee)
        {
            AppLog.Info($"Fee on {visit.VisitNo} corrected at the desk: {visit.Fee:0.00} -> {corrected:0.00}.");
            visit.Fee = corrected;
        }

        visit.FeePaid = true;
        visit.FeeReceiptNo = await NumberService.NextAsync(db, NumberService.FeeReceipt);
        visit.FeePaidOn = DateTime.Now;
        visit.FeePaymentMode = mode;
        visit.FeeTransactionNo = string.IsNullOrWhiteSpace(transactionNo) ? null : transactionNo.Trim();

        await db.SaveChangesAsync();

        AppLog.Info($"Receipt {visit.FeeReceiptNo} for {visit.Fee:0.00} ({mode}) against visit {visit.VisitNo}.");

        log.Ok($"{visit.FeeReceiptNo} amount={visit.Fee:0.00} visit={visit.VisitNo}");
        return visit;
    }

    /// <summary>Saves the consultation and replaces the prescription and the
    /// requested-tests list in one step.</summary>
    public async Task SaveConsultationAsync(
        Visit edited, IEnumerable<PrescriptionItem> prescription,
        IEnumerable<VisitDiagnosticRequest> diagnosticRequests, bool complete)
    {
        var items = prescription.ToList();
        var tests = diagnosticRequests.ToList();

        using var log = AppLog.Enter(
            nameof(SaveConsultationAsync),
            $"visit={edited.Id} rx={items.Count} tests={tests.Count} complete={complete} fee={edited.Fee}");

        await using var db = await factory.CreateDbContextAsync();

        var visit = await db.Visits.Include(v => v.Prescription).Include(v => v.DiagnosticRequests)
                                   .FirstOrDefaultAsync(v => v.Id == edited.Id)
                    ?? throw new InvalidOperationException("Visit not found.");

        visit.Complaint = edited.Complaint;
        visit.Diagnosis = edited.Diagnosis;
        visit.Notes = edited.Notes;
        visit.WeightKg = edited.WeightKg;
        visit.BloodPressure = edited.BloodPressure;
        visit.TemperatureF = edited.TemperatureF;
        visit.HeightCm = edited.HeightCm;
        visit.HeartRateBpm = edited.HeartRateBpm;
        visit.Spo2Percent = edited.Spo2Percent;
        visit.Fee = edited.Fee;
        visit.FollowUpOn = edited.FollowUpOn;

        AppLog.Trace($"  replacing {visit.Prescription.Count} prescribed line(s) with {items.Count}");

        db.PrescriptionItems.RemoveRange(visit.Prescription);

        foreach (var item in items)
        {
            AppLog.Trace(
                $"  rx '{item.MedicineName}' product={item.ProductId} dose='{item.Dosage}' " +
                $"freq='{item.Frequency}' days={item.Days} qty={item.Quantity}");

            db.PrescriptionItems.Add(new PrescriptionItem
            {
                VisitId = visit.Id,
                ProductId = item.ProductId,
                MedicineName = item.MedicineName,
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Days = item.Days,
                Quantity = item.Quantity,
                Instructions = item.Instructions
            });
        }

        db.VisitDiagnosticRequests.RemoveRange(visit.DiagnosticRequests);

        foreach (var test in tests)
        {
            db.VisitDiagnosticRequests.Add(new VisitDiagnosticRequest
            {
                VisitId = visit.Id,
                TestId = test.TestId,
                TestName = test.TestName
            });
        }

        if (complete) visit.Status = VisitStatus.Completed;

        await db.SaveChangesAsync();

        log.Ok($"{visit.VisitNo} saved, status={visit.Status}");
    }
}
