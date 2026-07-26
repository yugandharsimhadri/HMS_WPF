using Microsoft.EntityFrameworkCore;
using Pharma.Core;

namespace Pharma.Data;

/// <summary>OPD: patients, doctors and the single Visit record that covers
/// booking, queue and consultation.</summary>
public class OpdService(IDbContextFactory<AppDbContext> factory)
{
    // ── Patients ───────────────────────────────────────────────────────────

    public async Task<List<Patient>> SearchPatientsAsync(string? term, int take = 50)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Patients.Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term.Trim();
            q = q.Where(p => p.Name.Contains(term) || p.Phone.Contains(term) || p.PatientNo.Contains(term));
        }

        return await q.OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync();
    }

    public async Task<Patient> SavePatientAsync(Patient patient)
    {
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
        return patient;
    }

    /// <summary>Every visit this patient has made, newest first.</summary>
    public async Task<List<Visit>> GetPatientHistoryAsync(Guid patientId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Visits
            .Include(v => v.Doctor)
            .Include(v => v.Prescription)
            .Where(v => !v.IsDeleted && v.PatientId == patientId)
            .OrderByDescending(v => v.ScheduledOn)
            .ToListAsync();
    }

    /// <summary>Soft-deletes a patient. Refused while visits still reference them.</summary>
    public async Task<string?> DeletePatientAsync(Guid patientId)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (await db.Visits.AnyAsync(v => v.PatientId == patientId && !v.IsDeleted))
            return "This patient has visits on record and cannot be removed.";

        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient is null) return "Patient not found.";

        patient.IsDeleted = true;
        await db.SaveChangesAsync();
        return null;
    }

    // ── Doctors ────────────────────────────────────────────────────────────

    public async Task<List<Doctor>> GetDoctorsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Doctors.Where(d => !d.IsDeleted && d.IsActive)
                               .OrderBy(d => d.Name).ToListAsync();
    }

    public async Task SaveDoctorAsync(Doctor doctor)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (doctor.Id != Guid.Empty && await db.Doctors.AnyAsync(d => d.Id == doctor.Id))
            db.Doctors.Update(doctor);
        else
            db.Doctors.Add(doctor);

        await db.SaveChangesAsync();
    }

    // ── Visits ─────────────────────────────────────────────────────────────

    public async Task<List<Visit>> GetVisitsAsync(DateTime date)
    {
        await using var db = await factory.CreateDbContextAsync();
        var from = date.Date;
        var to = from.AddDays(1);

        return await db.Visits
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .Where(v => !v.IsDeleted && v.ScheduledOn >= from && v.ScheduledOn < to)
            .OrderBy(v => v.TokenNo)
            .ToListAsync();
    }

    public async Task<Visit?> GetVisitAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Visits
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .Include(v => v.Prescription)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    /// <summary>Books a visit and allocates the next token for that day.</summary>
    public async Task<Visit> BookVisitAsync(Guid patientId, Guid doctorId, DateTime scheduledOn, string? complaint, decimal fee)
    {
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
        return visit;
    }

    public async Task SetStatusAsync(Guid visitId, VisitStatus status)
    {
        await using var db = await factory.CreateDbContextAsync();
        var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId);
        if (visit is null) return;

        visit.Status = status;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Records the consultation fee and allocates a receipt number. Collecting
    /// twice is a no-op so a double click cannot burn a number or restate the date.
    /// </summary>
    public async Task<Visit?> CollectFeeAsync(Guid visitId, PaymentMode mode = PaymentMode.Cash)
    {
        await using var db = await factory.CreateDbContextAsync();

        var visit = await db.Visits
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .FirstOrDefaultAsync(v => v.Id == visitId);

        if (visit is null) return null;
        if (visit.FeePaid) return visit;

        visit.FeePaid = true;
        visit.FeeReceiptNo = await NumberService.NextAsync(db, NumberService.FeeReceipt);
        visit.FeePaidOn = DateTime.Now;
        visit.FeePaymentMode = mode;

        await db.SaveChangesAsync();

        AppLog.Info($"Receipt {visit.FeeReceiptNo} for {visit.Fee:0.00} ({mode}) against visit {visit.VisitNo}.");
        return visit;
    }

    /// <summary>Saves the consultation and replaces the prescription in one step.</summary>
    public async Task SaveConsultationAsync(Visit edited, IEnumerable<PrescriptionItem> prescription, bool complete)
    {
        await using var db = await factory.CreateDbContextAsync();

        var visit = await db.Visits.Include(v => v.Prescription)
                                   .FirstOrDefaultAsync(v => v.Id == edited.Id)
                    ?? throw new InvalidOperationException("Visit not found.");

        visit.Complaint = edited.Complaint;
        visit.Diagnosis = edited.Diagnosis;
        visit.Notes = edited.Notes;
        visit.WeightKg = edited.WeightKg;
        visit.BloodPressure = edited.BloodPressure;
        visit.TemperatureF = edited.TemperatureF;
        visit.Fee = edited.Fee;
        visit.FollowUpOn = edited.FollowUpOn;

        db.PrescriptionItems.RemoveRange(visit.Prescription);
        foreach (var item in prescription)
        {
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

        if (complete) visit.Status = VisitStatus.Completed;

        await db.SaveChangesAsync();
    }
}
