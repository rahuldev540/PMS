using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Controllers;

public sealed class WizardLineDto
{
    [Range(1, int.MaxValue)] public int MedicineId { get; set; }
    [Range(0, 100)] public int Morning { get; set; }
    [Range(0, 100)] public int Afternoon { get; set; }
    [Range(0, 100)] public int Evening { get; set; }
    [Range(1, 365)] public int DurationDays { get; set; } = 1;
    public int? Quantity { get; set; } // Optional override; must not be below calculated doses.
    public string? Instructions { get; set; }
}
public sealed class PrescriptionWizardDto
{
    public int? BranchId { get; set; } // Public pharmacy-scoped branch ID, required for Admin.
    public int? PatientId { get; set; } // Public pharmacy-scoped patient ID.
    [Required, MaxLength(200)] public string PatientName { get; set; } = "";
    [MaxLength(30)] public string? PhoneNumber { get; set; }
    [Range(0, 125)] public int Age { get; set; }
    [MaxLength(30)] public string? Gender { get; set; }
    [Required, MaxLength(200)] public string DoctorName { get; set; } = "";
    public DateTime? PrescriptionDate { get; set; }
    public string? Notes { get; set; }
    public List<WizardLineDto> Medicines { get; set; } = new();
}

[ApiController]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
[Route("api/pharmacy/dispensing")]
public sealed class PharmacyPrescriptionWizardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyWorkflowAccess _access;
    public PharmacyPrescriptionWizardController(AppDbContext db, PharmacyWorkflowAccess access)
    { _db = db; _access = access; }

    [HttpGet("patients")]
    public async Task<IActionResult> Patients([FromQuery] string? search, [FromQuery] int? branchId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Prescriptions", "View")) return Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Patients.AsNoTracking().Where(x => x.PharmacyId == a.Pharmacy);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim(); query = query.Where(x => x.Name.Contains(q) || x.Phone.Contains(q));
        }
        var total = await query.CountAsync();
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { id = x.PharmacyPatientId, name = x.Name, phoneNumber = x.Phone, age = x.Age, gender = x.Gender }).ToListAsync();
        return Ok(new { data = new { items = rows, total, hasMore = (long)page * pageSize < total } });
    }

    [HttpGet("medicines")]
    public async Task<IActionResult> Medicines([FromQuery] string? search, [FromQuery] int? branchId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Medicines", "View")) return Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Medicines.AsNoTracking().Where(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch && x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        { var q = search.Trim(); query = query.Where(x => x.Name.Contains(q) || x.GenericName.Contains(q) || x.MedicineCode.Contains(q)); }
        var total = await query.CountAsync();
        var rows = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { medicineId = x.Id, medicineName = x.Name, x.Strength, unitPrice = x.SellingPrice, stockQuantity = x.StockQuantity, x.GST }).ToListAsync();
        return Ok(new { data = new { items = rows, total, hasMore = (long)page * pageSize < total } });
    }

    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft([FromBody] PrescriptionWizardDto dto)
    {
        var a = await _access.ResolveAsync(User, dto.BranchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Prescriptions", "Create")) return Forbid();
        var (lines, error) = await BuildLines(dto, a.Pharmacy, a.Branch);
        if (error != null) return BadRequest(new { message = error });
        var draft = new PharmacyPrescriptionDraft { PharmacyId = a.Pharmacy, BranchId = a.Branch, CreatedByUserId = a.User };
        CopyDraft(draft, dto); draft.Lines = lines!;
        _db.PharmacyPrescriptionDrafts.Add(draft);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { data = new { draftId = draft.Id, status = draft.Status, totalQuantity = draft.Lines.Sum(x => x.Quantity), totalPayable = draft.Lines.Sum(x => x.Amount) } });
    }

    [HttpPut("drafts/{id:int}")]
    public async Task<IActionResult> UpdateDraft(int id, [FromBody] PrescriptionWizardDto dto)
    {
        var draft = await _db.PharmacyPrescriptionDrafts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (draft == null) return NotFound();
        var a = await _access.ResolveAsync(User, dto.BranchId, draft.BranchId);
        if (a.Error != null || a.Pharmacy != draft.PharmacyId || a.User != draft.CreatedByUserId) return Forbid();
        if (!await _access.CanAsync(a.User, "Prescriptions", "Edit")) return Forbid();
        if (draft.Status != "Draft") return Conflict(new { message = "Submitted drafts cannot be edited." });
        var (lines, error) = await BuildLines(dto, a.Pharmacy, a.Branch);
        if (error != null) return BadRequest(new { message = error });
        _db.PharmacyPrescriptionDraftLines.RemoveRange(draft.Lines);
        CopyDraft(draft, dto); draft.Lines = lines!;
        await _db.SaveChangesAsync();
        return Ok(new { data = new { draftId = draft.Id, totalQuantity = draft.Lines.Sum(x => x.Quantity), totalPayable = draft.Lines.Sum(x => x.Amount) } });
    }

    [HttpGet("drafts")]
    public async Task<IActionResult> ListDrafts([FromQuery] int? branchId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Prescriptions", "View")) return Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.PharmacyPrescriptionDrafts.AsNoTracking().Where(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch && x.CreatedByUserId == a.User && x.Status == "Draft");
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { draftId = x.Id, x.PatientName, x.DoctorName, x.UpdatedAt, medicineCount = x.Lines.Count, totalPayable = x.Lines.Sum(l => l.Amount) }).ToListAsync();
        return Ok(new { data = new { items, total, hasMore = (long)page * pageSize < total } });
    }

    [HttpGet("drafts/{id:int}")]
    public async Task<IActionResult> GetDraft(int id)
    {
        var draft = await _db.PharmacyPrescriptionDrafts.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (draft == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, draft.BranchId);
        if (a.Error != null || a.Pharmacy != draft.PharmacyId || a.User != draft.CreatedByUserId) return Forbid();
        if (!await _access.CanAsync(a.User, "Prescriptions", "View")) return Forbid();
        var photos = await _db.PharmacyUploadedEvidences.AsNoTracking().Where(x => x.DraftId == id && x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch)
            .Select(x => new { id = x.Id, x.FileName, x.ContentType, x.UploadedAt }).ToListAsync();
        return Ok(new { data = new { draft.Id, draft.PublicPatientId, draft.PatientName, draft.PatientPhone, draft.PatientAge,
            draft.PatientGender, draft.DoctorName, draft.PrescriptionDate, draft.Notes, draft.Status, draft.SubmittedPrescriptionId,
            medicines = draft.Lines.Select(x => new { x.MedicineId, x.MedicineName, x.Strength, morning = x.MorningUnits,
                afternoon = x.AfternoonUnits, evening = x.EveningUnits, durationDays = x.DurationDays, x.Quantity, x.UnitPrice, x.Amount, x.Instructions }),
            totalQuantity = draft.Lines.Sum(x => x.Quantity), totalPayable = draft.Lines.Sum(x => x.Amount), photos } });
    }

    [HttpGet("prescriptions/{prescriptionId:int}/details")]
    public async Task<IActionResult> PrescriptionDetails(int prescriptionId)
    {
        var rx = await _db.Prescriptions.AsNoTracking().Include(x => x.Medicines)
            .FirstOrDefaultAsync(x => x.Id == prescriptionId);
        if (rx == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, rx.BranchId);
        if (a.Error != null || a.Pharmacy != rx.PharmacyId) return Forbid();
        if (!await _access.CanAsync(a.User, "Prescriptions", "View")) return Forbid();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rx.PatientId && x.PharmacyId == a.Pharmacy);
        var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == rx.DoctorId && x.PharmacyId == a.Pharmacy);
        var itemIds = rx.Medicines.Select(x => x.Id).ToArray();
        var details = await _db.PharmacyPrescriptionDoseDetails.AsNoTracking().Where(x => itemIds.Contains(x.PrescriptionItemId)).ToListAsync();
        return Ok(new { data = new { prescriptionId = rx.Id, patientId = patient?.PharmacyPatientId, patientName = patient?.Name,
            phoneNumber = patient?.Phone, age = patient?.Age, gender = patient?.Gender, doctorName = doctor?.Name,
            rx.PrescriptionDate, rx.Notes, rx.Status,
            medicines = rx.Medicines.Select(x => {
                var d = details.FirstOrDefault(v => v.PrescriptionItemId == x.Id);
                return new { prescriptionItemId = x.Id, x.MedicineId, x.MedicineName, x.Quantity,
                    morning = d?.MorningUnits ?? (x.Morning ? 1 : 0), afternoon = d?.AfternoonUnits ?? (x.Afternoon ? 1 : 0),
                    evening = d?.EveningUnits ?? (x.Night ? 1 : 0), durationDays = d?.DurationDays ?? x.Days,
                    unitPrice = d?.UnitPriceAtPrescription, amount = d == null ? (decimal?)null : x.Quantity * d.UnitPriceAtPrescription,
                    x.Instructions }; }).ToList() } });
    }

    [HttpPost("drafts/{id:int}/submit")]
    public async Task<IActionResult> Submit(int id)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var draft = await _db.PharmacyPrescriptionDrafts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (draft == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, draft.BranchId);
        if (a.Error != null || a.Pharmacy != draft.PharmacyId || a.User != draft.CreatedByUserId) return Forbid();
        if (!await _access.CanAsync(a.User, "Prescriptions", "Create")) return Forbid();
        if (draft.Status == "Submitted") return Ok(new { data = new { prescriptionId = draft.SubmittedPrescriptionId, status = "Submitted" } });
        if (draft.Status != "Draft" || draft.Lines.Count == 0) return Conflict(new { message = "Invalid draft state." });
        var ids = draft.Lines.Select(x => x.MedicineId).Distinct().ToArray();
        if (await _db.Medicines.CountAsync(x => ids.Contains(x.Id) && x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch && x.IsActive) != ids.Length)
            return BadRequest(new { message = "A medicine is no longer available in this branch." });
        Patient? patient = null;
        if (draft.PublicPatientId.HasValue)
            patient = await _db.Patients.FirstOrDefaultAsync(x => x.PharmacyId == a.Pharmacy && x.PharmacyPatientId == draft.PublicPatientId);
        if (draft.PublicPatientId.HasValue && patient == null) return BadRequest(new { message = "Patient ID not found in this pharmacy." });
        if (patient != null && !string.Equals(patient.Name, draft.PatientName, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Patient ID and patient name do not match." });
        if (patient == null && !string.IsNullOrWhiteSpace(draft.PatientPhone))
        {
            var existing = await _db.Patients.Where(x => x.PharmacyId == a.Pharmacy && x.Phone == draft.PatientPhone).ToListAsync();
            patient = existing.FirstOrDefault(x => string.Equals(x.Name, draft.PatientName, StringComparison.OrdinalIgnoreCase));
            if (patient == null && existing.Count > 0) return Conflict(new { message = "Phone is already associated with another patient. Select the correct public Patient ID." });
        }
        if (patient == null)
        {
            var next = (await _db.Patients.Where(x => x.PharmacyId == a.Pharmacy).MaxAsync(x => (int?)x.PharmacyPatientId) ?? 0) + 1;
            patient = new Patient { PharmacyId = a.Pharmacy, BranchId = a.Branch, PharmacyPatientId = next,
                PatientCode = $"PH-{a.Pharmacy}-{next}", Name = draft.PatientName, Phone = draft.PatientPhone,
                Age = draft.PatientAge, Gender = draft.PatientGender, SourceSystem = "PharmacyStandalone" };
            _db.Patients.Add(patient); await _db.SaveChangesAsync();
        }
        var doctor = await _db.Doctors.FirstOrDefaultAsync(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch && x.Name == draft.DoctorName && x.SourceSystem == "PharmacyStandalone");
        if (doctor == null)
        {
            doctor = new Doctor { PharmacyId = a.Pharmacy, BranchId = a.Branch, Name = draft.DoctorName,
                SourceSystem = "PharmacyStandalone", IsActive = true, CreatedAt = DateTime.UtcNow };
            _db.Doctors.Add(doctor); await _db.SaveChangesAsync();
        }
        var appt = new Appointment { PharmacyId = a.Pharmacy, BranchId = a.Branch, PatientId = patient.Id,
            DoctorId = doctor.Id, BookingType = "Offline", Date = draft.PrescriptionDate.Date,
            StartTime = DateTime.UtcNow.TimeOfDay, TokenNumber = $"RX-{Guid.NewGuid():N}",
            Status = "PrescriptionAdded", SourceSystem = "PharmacyStandalone" };
        _db.Appointments.Add(appt); await _db.SaveChangesAsync();
        var rx = new Prescription { PharmacyId = a.Pharmacy, BranchId = a.Branch, PatientId = patient.Id,
            DoctorId = doctor.Id, AppointmentId = appt.Id, PrescriptionDate = draft.PrescriptionDate,
            Notes = draft.Notes, SourceSystem = "PharmacyStandalone", Status = Prescription.StatusPending,
            Medicines = draft.Lines.Select(x => new PrescriptionItem { MedicineId = x.MedicineId, MedicineName = x.MedicineName,
                Days = x.DurationDays, Quantity = x.Quantity, Morning = x.MorningUnits > 0,
                Afternoon = x.AfternoonUnits > 0, Night = x.EveningUnits > 0,
                Dosage = $"{x.MorningUnits}-{x.AfternoonUnits}-{x.EveningUnits}",
                Instructions = x.Instructions, Status = "Pending" }).ToList() };
        _db.Prescriptions.Add(rx); await _db.SaveChangesAsync();
        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i]; var item = rx.Medicines.ElementAt(i);
            _db.PharmacyPrescriptionDoseDetails.Add(new PharmacyPrescriptionDoseDetail {
                PrescriptionItemId = item.Id, MorningUnits = line.MorningUnits, AfternoonUnits = line.AfternoonUnits,
                EveningUnits = line.EveningUnits, DurationDays = line.DurationDays, UnitPriceAtPrescription = line.UnitPrice });
        }
        draft.Status = "Submitted"; draft.SubmittedPrescriptionId = rx.Id; draft.UpdatedAt = DateTime.UtcNow;
        var photos = await _db.PharmacyUploadedEvidences.Where(x => x.DraftId == id && x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch).ToListAsync();
        foreach (var photo in photos) photo.PrescriptionId = rx.Id;
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return StatusCode(201, new { data = new { prescriptionId = rx.Id, patientId = patient.PharmacyPatientId, status = rx.Status,
            totalQuantity = draft.Lines.Sum(x => x.Quantity), estimatedMedicineTotal = draft.Lines.Sum(x => x.Amount),
            note = "Use existing generate-bill and dispensing APIs; price estimate is not a final tax invoice." } });
    }

    private async Task<(List<PharmacyPrescriptionDraftLine>? Lines, string? Error)> BuildLines(PrescriptionWizardDto dto, int pharmacy, int branch)
    {
        if (string.IsNullOrWhiteSpace(dto.PatientName) || string.IsNullOrWhiteSpace(dto.DoctorName)) return (null, "Patient and doctor names are required.");
        if (dto.Medicines == null || dto.Medicines.Count > 100) return (null, "Add no more than 100 medicines.");
        if (dto.Medicines.Count == 0) return (new List<PharmacyPrescriptionDraftLine>(), null);
        if (dto.Medicines.Any(x => x.MedicineId <= 0 || x.DurationDays is < 1 or > 365 ||
            x.Morning is < 0 or > 100 || x.Afternoon is < 0 or > 100 || x.Evening is < 0 or > 100))
            return (null, "Invalid medicine or dosage schedule.");
        var ids = dto.Medicines.Select(x => x.MedicineId).Distinct().ToArray();
        var medicines = await _db.Medicines.AsNoTracking().Where(x => ids.Contains(x.Id) && x.PharmacyId == pharmacy && x.BranchId == branch && x.IsActive).ToDictionaryAsync(x => x.Id);
        if (medicines.Count != ids.Length) return (null, "Medicine is not active in the selected branch.");
        var lines = new List<PharmacyPrescriptionDraftLine>();
        foreach (var x in dto.Medicines)
        {
            var calculated = (long)(x.Morning + x.Afternoon + x.Evening) * x.DurationDays;
            if (calculated < 1 || calculated > 100000 || x.Quantity.HasValue && (x.Quantity < 1 || x.Quantity > 100000))
                return (null, "Quantity must be between 1 and 100000.");
            var med = medicines[x.MedicineId];
            var qty = x.Quantity ?? (int)calculated;
            lines.Add(new PharmacyPrescriptionDraftLine { MedicineId = x.MedicineId, MedicineName = med.Name, Strength = med.Strength,
                MorningUnits = x.Morning, AfternoonUnits = x.Afternoon, EveningUnits = x.Evening,
                DurationDays = x.DurationDays, Quantity = qty, UnitPrice = med.SellingPrice,
                Amount = Math.Round(qty * med.SellingPrice, 2), Instructions = x.Instructions?.Trim() ?? "" });
        }
        return (lines, null);
    }

    private static void CopyDraft(PharmacyPrescriptionDraft draft, PrescriptionWizardDto dto)
    {
        draft.PublicPatientId = dto.PatientId; draft.PatientName = dto.PatientName.Trim();
        draft.PatientPhone = dto.PhoneNumber?.Trim() ?? ""; draft.PatientAge = dto.Age;
        draft.PatientGender = string.IsNullOrWhiteSpace(dto.Gender) ? "Unknown" : dto.Gender.Trim();
        draft.DoctorName = dto.DoctorName.Trim(); draft.Notes = dto.Notes?.Trim() ?? "";
        draft.PrescriptionDate = dto.PrescriptionDate ?? DateTime.UtcNow;
        draft.UpdatedAt = DateTime.UtcNow;
    }
}
