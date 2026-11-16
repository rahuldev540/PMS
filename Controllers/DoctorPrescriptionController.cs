using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/doctor/prescriptions")]
[Authorize(Roles = "Doctor")]
public class DoctorPrescriptionController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    public DoctorPrescriptionController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<IActionResult> Create(CreateDoctorPrescriptionDto dto)
    {
        var doctorId = await GetDoctorId();
        if (dto.Medicines.Count == 0 || dto.Medicines.Any(x => x.Quantity <= 0 || x.Days <= 0)) return BadRequest("Valid medicine items are required.");
        if (!await _db.Patients.AnyAsync(x => x.Id == dto.PatientId && x.PharmacyId == PharmacyId))
            return BadRequest("Patient does not belong to this pharmacy.");
        var medicineIds = dto.Medicines.Select(x => x.MedicineId).Distinct().ToList();
        if (await _db.Medicines.CountAsync(x => medicineIds.Contains(x.Id) && x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.IsActive) != medicineIds.Count)
            return BadRequest("One or more medicines are invalid.");
        var p = new Prescription { PharmacyId = PharmacyId, BranchId = BranchId, AppointmentId = dto.AppointmentId,
            DoctorId = doctorId, PatientId = dto.PatientId, Diagnosis = dto.Diagnosis, Advice = dto.Advice, Notes = dto.Notes,
            Instructions = dto.Instructions, FollowUpDate = dto.FollowUpDate, Status = "Draft",
            Medicines = dto.Medicines.Select(x => new PrescriptionItem { MedicineId = x.MedicineId, Dosage = x.Dosage,
                Instructions = x.Instructions, Morning = x.Morning, Afternoon = x.Afternoon, Night = x.Night,
                Days = x.Days, Quantity = x.Quantity, Notes = x.Notes, Status = "Pending" }).ToList() };
        _db.Prescriptions.Add(p); await _db.SaveChangesAsync(); return CreatedAtAction(nameof(Get), new { id = p.Id }, new { p.Id, p.Status });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var doctorId = await GetDoctorId();
        var p = await _db.Prescriptions.AsNoTracking().Where(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.DoctorId == doctorId)
            .Select(x => new { x.Id, x.AppointmentId, x.PatientId, x.PrescriptionDate, x.Diagnosis, x.Advice, x.Notes,
                x.Instructions, x.FollowUpDate, x.Status, Medicines = x.Medicines.Select(i => new { i.Id, i.MedicineId,
                    i.Medicine.Name, i.Medicine.Strength, i.Dosage, i.Instructions, i.Morning, i.Afternoon, i.Night, i.Days, i.Quantity, i.Notes, i.Status }).ToList() }).FirstOrDefaultAsync();
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateDoctorPrescriptionDto dto)
    {
        var doctorId = await GetDoctorId();
        var p = await _db.Prescriptions.Include(x => x.Medicines).FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.DoctorId == doctorId);
        if (p is null) return NotFound(); if (p.Status != "Draft") return BadRequest("Only draft prescriptions can be updated.");
        if (!await _db.Patients.AnyAsync(x => x.Id == dto.PatientId && x.PharmacyId == PharmacyId))
            return BadRequest("Patient does not belong to this pharmacy.");
        var medicineIds = dto.Medicines.Select(x => x.MedicineId).Distinct().ToList();
        if (dto.Medicines.Count == 0 || dto.Medicines.Any(x => x.Quantity <= 0 || x.Days <= 0) ||
            await _db.Medicines.CountAsync(x => medicineIds.Contains(x.Id) && x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.IsActive) != medicineIds.Count)
            return BadRequest("One or more medicines are invalid.");
        p.PatientId = dto.PatientId; p.AppointmentId = dto.AppointmentId; p.Diagnosis = dto.Diagnosis; p.Advice = dto.Advice;
        p.Notes = dto.Notes; p.Instructions = dto.Instructions; p.FollowUpDate = dto.FollowUpDate;
        _db.PrescriptionItems.RemoveRange(p.Medicines); p.Medicines = dto.Medicines.Select(x => new PrescriptionItem
        { MedicineId = x.MedicineId, Dosage = x.Dosage, Instructions = x.Instructions, Morning = x.Morning,
          Afternoon = x.Afternoon, Night = x.Night, Days = x.Days, Quantity = x.Quantity, Notes = x.Notes, Status = "Pending" }).ToList();
        await _db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id) => await ChangeStatus(id, "Completed");

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id) => await ChangeStatus(id, "Cancelled");

    private async Task<int> GetDoctorId()
    {
        var doctorId = await _db.Users.Where(x => x.Id == UserId && x.DoctorId.HasValue).Select(x => x.DoctorId!.Value).FirstOrDefaultAsync();
        if (doctorId == 0) throw new UnauthorizedAccessException("Doctor account is not linked."); return doctorId;
    }

    private async Task<IActionResult> ChangeStatus(int id, string status)
    {
        var doctorId = await GetDoctorId();
        var p = await _db.Prescriptions.FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.DoctorId == doctorId);
        if (p is null) return NotFound(); if (p.Status != "Draft") return BadRequest("Only draft prescriptions can change state.");
        p.Status = status; await _db.SaveChangesAsync(); return NoContent();
    }
}
