using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class PatientCompleteHistoryController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    public PatientCompleteHistoryController(AppDbContext db) => _db = db;

    [HttpGet("complete-history")]
    public async Task<IActionResult> CompleteHistory([FromQuery] string mobile)
    {
        mobile = mobile?.Trim() ?? string.Empty;
        if (mobile.Length < 10 || mobile.Length > 20 || !mobile.All(char.IsDigit))
            return BadRequest(new { Success = false, Message = "Enter a valid mobile number (10-20 digits)." });

        var patients = await _db.Patients.AsNoTracking()
            .Where(p => p.PharmacyId == PharmacyId && p.Phone == mobile)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.PharmacyPatientId, p.PatientCode, p.Name, p.Phone,
                p.Age, p.Gender, p.CreatedAt })
            .ToListAsync();
        if (patients.Count == 0)
            return Ok(new { Success = true, PatientFound = false, Patients = Array.Empty<object>() });

        var ids = patients.Select(p => p.Id).ToList();
        var prescriptions = await _db.Prescriptions.AsNoTracking()
            .Where(p => p.PharmacyId == PharmacyId && ids.Contains(p.PatientId))
            .Select(p => new {
                p.Id, p.PatientId, p.BranchId, p.AppointmentId, p.PrescriptionDate,
                p.Diagnosis, p.Advice, p.Notes, p.Status,
                Medicines = p.Medicines.Select(i => new {
                    i.MedicineId, i.MedicineName, i.Quantity, i.DispensedQuantity,
                    i.Dosage, i.Days, i.Instructions, i.Morning, i.Afternoon, i.Night,
                    i.Status
                }).ToList()
            }).ToListAsync();
        var bills = await _db.PharmacyBills.AsNoTracking()
            .Where(b => b.PharmacyId == PharmacyId && ids.Contains(b.PatientId))
            .Select(b => new {
                b.Id, b.PatientId, b.BranchId, b.PrescriptionId, b.BillDate,
                b.SubTotal, b.Discount, b.GSTAmount, b.GrandTotal, b.PaymentStatus,
                PurchasedMedicines = b.BillItems.Select(i => new {
                    i.MedicineId, i.MedicineName, i.Quantity, i.Rate, i.GST, i.Total
                }).ToList()
            }).ToListAsync();
        var branchMap = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId)
            .ToDictionaryAsync(x => x.ExistingBranchId, x => x.BranchId);
        int PublicBranch(int internalId) => branchMap.TryGetValue(internalId, out var visible) ? visible : throw new InvalidOperationException("Missing public branch mapping for patient history.");

        var results = patients.Select(patient => {
            var rx = prescriptions.Where(x => x.PatientId == patient.Id)
                .OrderByDescending(x => x.PrescriptionDate).ThenByDescending(x => x.Id)
                .Select(x => new {
                    PrescriptionId = x.Id, x.AppointmentId, x.PrescriptionDate,
                    BranchId = PublicBranch(x.BranchId), x.Diagnosis, x.Advice, x.Notes, x.Status,
                    PrescribedMedicines = x.Medicines
                }).ToList();
            var purchases = bills.Where(x => x.PatientId == patient.Id)
                .OrderByDescending(x => x.BillDate).ThenByDescending(x => x.Id)
                .Select(x => new {
                    BillId = x.Id, BranchId = PublicBranch(x.BranchId), x.PrescriptionId,
                    x.BillDate, x.SubTotal, x.Discount, x.GSTAmount, x.GrandTotal,
                    x.PaymentStatus, x.PurchasedMedicines
                }).ToList();
            return new {
                PatientId = patient.PharmacyPatientId,
                patient.PatientCode, PatientName = patient.Name, MobileNumber = patient.Phone,
                patient.Age, patient.Gender, patient.CreatedAt,
                TotalPrescriptions = rx.Count, TotalBills = purchases.Count,
                LastPrescription = rx.FirstOrDefault(), LastPurchase = purchases.FirstOrDefault(),
                Prescriptions = rx, Bills = purchases
            };
        }).ToList();
        return Ok(new { Success = true, PatientFound = true, PharmacyId, Patients = results });
    }
}
