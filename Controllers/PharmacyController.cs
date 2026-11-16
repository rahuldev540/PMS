using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class PharmacyController : PharmacyApiControllerBase
{
    private readonly IPharmacyService _pharmacyService;
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public PharmacyController(
        IPharmacyService pharmacyService,
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _pharmacyService = pharmacyService;
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // COMMON PERMISSION CHECK
    //
    // PharmacyAdmin
    // -> PharmacyPermissionRole
    //
    // Pharmacist
    // -> UserPermission
    // =====================================================

    private async Task<IActionResult?> CheckPermission(
        string module,
        string action)
    {
        if (string.IsNullOrWhiteSpace(module) ||
            string.IsNullOrWhiteSpace(action))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        "Invalid permission request."
                });
        }

        var moduleName =
            module.Trim();

        var actionName =
            action.Trim();

        // =================================================
        // PHARMACY ADMIN
        // =================================================

        if (User.IsInRole("PharmacyAdmin"))
        {
            var allowed =
                await _permissionChecker
                    .HasPermissionAsync(
                        UserId,
                        moduleName,
                        actionName);

            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            $"You do not have {actionName} permission for {moduleName}."
                    });
            }

            return null;
        }

        // =================================================
        // PHARMACIST
        // =================================================

        if (User.IsInRole("Pharmacist"))
        {
            var normalizedModule =
                moduleName.ToLowerInvariant();

            var permission =
                await _db.UserPermissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UserId == UserId &&
                        x.Module.ToLower() ==
                        normalizedModule);

            if (permission == null)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            $"You do not have access to {moduleName}."
                    });
            }

            var allowed =
                actionName
                    .ToLowerInvariant() switch
                {
                    "view" =>
                        permission.CanView,

                    "create" =>
                        permission.CanCreate,

                    "edit" =>
                        permission.CanEdit,

                    "delete" =>
                        permission.CanDelete,

                    _ =>
                        false
                };

            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            $"You do not have {actionName} permission for {moduleName}."
                    });
            }

            return null;
        }

        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                Success = false,
                Message =
                    "You do not have permission to access this resource."
            });
    }

    // =====================================================
    // CREATE MANUAL PRESCRIPTION (STANDALONE PHARMACY)
    // POST /api/pharmacy/prescriptions/manual
    // =====================================================
    [HttpPost("prescriptions/manual")]
    public async Task<IActionResult> CreateManualPrescription(
        [FromBody] CreateManualPharmacyPrescriptionDto dto)
    {
        var permissionError = await CheckPermission("Prescriptions", "Create");
        if (permissionError != null) return permissionError;

        if (string.IsNullOrWhiteSpace(dto.PatientName) || dto.Medicines.Count == 0 ||
            dto.Medicines.Any(x => x.MedicineId <= 0 || x.Quantity <= 0 || x.Days <= 0))
            return BadRequest(new { Success = false, Message = "Patient name and at least one valid medicine are required." });

        var medicineIds = dto.Medicines.Select(x => x.MedicineId).Distinct().ToList();
        var medicines = await _db.Medicines.AsNoTracking()
            .Where(x => medicineIds.Contains(x.Id) && x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.IsActive)
            .ToDictionaryAsync(x => x.Id);

        if (medicines.Count != medicineIds.Count)
            return BadRequest(new { Success = false, Message = "One or more medicines do not belong to this pharmacy." });

        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            // Match only inside this pharmacy. A phone can belong to multiple family members.
            var phone = dto.PatientPhone?.Trim() ?? string.Empty;
            var matches = string.IsNullOrWhiteSpace(phone)
                ? new List<Patient>()
                : await _db.Patients.Where(x => x.PharmacyId == PharmacyId && x.Phone == phone).ToListAsync();
            var exactMatches = matches.Where(x =>
                string.Equals(x.Name?.Trim(), dto.PatientName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.Age == Math.Max(0, dto.PatientAge) &&
                string.Equals(x.Gender?.Trim(), (string.IsNullOrWhiteSpace(dto.PatientGender) ? "Unknown" : dto.PatientGender.Trim()), StringComparison.OrdinalIgnoreCase)).ToList();
            if (exactMatches.Count > 1)
                return Conflict(new { Success = false, Message = "Multiple matching patients. Verify identity before creating a prescription." });
            Patient patient;
            if (exactMatches.Count == 1)
            {
                patient = exactMatches[0];
            }
            else
            {
                if (matches.Count > 0)
                    return Conflict(new { Success = false, Message = "This mobile number is registered to another patient in this pharmacy. Verify the patient and use the correct record; shared family numbers are supported.", Matches = matches.Select(x => new { x.PharmacyPatientId, x.Name, x.Age, x.Gender }) });
                var nextNumber = (await _db.Patients.Where(x => x.PharmacyId == PharmacyId)
                    .MaxAsync(x => (int?)x.PharmacyPatientId) ?? 0) + 1;
                patient = new Patient
                {
                    PharmacyPatientId = nextNumber,
                    PatientCode = $"PH-{PharmacyId}-{nextNumber}",
                    Name = dto.PatientName.Trim(),
                    Phone = phone,
                    Age = Math.Max(0, dto.PatientAge),
                    Gender = string.IsNullOrWhiteSpace(dto.PatientGender) ? "Unknown" : dto.PatientGender.Trim(),
                    PharmacyId = PharmacyId,
                    BranchId = BranchId,

                    SourceSystem = "PharmacyStandalone",
                    CreatedAt = DateTime.UtcNow
                };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync();
            }

            var doctorName = string.IsNullOrWhiteSpace(dto.DoctorName) ? "Walk-in / External Doctor" : dto.DoctorName.Trim();
            var doctor = await _db.Doctors.FirstOrDefaultAsync(x =>
                x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.SourceSystem == "PharmacyStandalone" && x.Name == doctorName);
            if (doctor == null)
            {
                doctor = new AuthDemo.Models.Doctor
                {
                    Name = doctorName,
                    PharmacyId = PharmacyId,

                    BranchId = BranchId,
                    SourceSystem = "PharmacyStandalone",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Doctors.Add(doctor);
                await _db.SaveChangesAsync();
            }

            var appointment = new AuthDemo.Models.Appointment
            {
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                PharmacyId = PharmacyId,

                BranchId = BranchId,
                BookingType = "Offline",
                Date = DateTime.UtcNow.Date,
                StartTime = DateTime.UtcNow.TimeOfDay,
                TokenNumber = $"RX-{DateTime.UtcNow:HHmmssfff}",
                Status = "PrescriptionAdded",
                SourceSystem = "PharmacyStandalone",
                CreatedAt = DateTime.UtcNow
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();

            var prescription = new AuthDemo.Models.Prescription
            {
                PharmacyId = PharmacyId,

                BranchId = BranchId,
                AppointmentId = appointment.Id,
                DoctorId = doctor.Id,
                PatientId = patient.Id,
                Diagnosis = dto.Diagnosis?.Trim() ?? string.Empty,
                Advice = dto.Advice?.Trim() ?? string.Empty,
                Notes = dto.Notes?.Trim() ?? string.Empty,
                SourceSystem = "PharmacyStandalone",
                Status = AuthDemo.Models.Prescription.StatusPending,
                PrescriptionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                Medicines = dto.Medicines.Select(x => new AuthDemo.Models.PrescriptionItem
                {
                    MedicineId = x.MedicineId,
                    MedicineName = medicines[x.MedicineId].Name,
                    Quantity = x.Quantity,
                    Days = x.Days,
                    Dosage = x.Dosage ?? string.Empty,
                    Instructions = x.Instructions ?? string.Empty,
                    Morning = x.Morning,
                    Afternoon = x.Afternoon,
                    Night = x.Night,
                    Status = "Pending"
                }).ToList()
            };
            _db.Prescriptions.Add(prescription);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return StatusCode(StatusCodes.Status201Created, new
            {
                Success = true,
                Message = "Standalone pharmacy prescription created successfully.",
                PrescriptionId = prescription.Id,
                PatientId = patient.PharmacyPatientId
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // =====================================================
    // DISPENSE PRESCRIPTION
    //
    // POST /api/pharmacy/dispense
    //
    // Prescriptions -> Edit
    // =====================================================

    [HttpPost("dispense")]
    public async Task<IActionResult> Dispense(
        [FromBody] DispensePrescriptionDto dto)
    {
        var permissionError =
            await CheckPermission(
                "Dispensing",
                "Edit");

        if (permissionError != null)
            return permissionError;

        try
        {
            var result =
                await _pharmacyService
                    .DispensePrescriptionAsync(
                        dto,
                        PharmacyId,
                        BranchId,
                        UserId);

            return Ok(new
            {
                Success = true,
                Message = result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    // =====================================================
    // GET PENDING PRESCRIPTIONS
    //
    // GET /api/pharmacy/pending
    //
    // Prescriptions -> View
    // =====================================================

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var permissionError =
            await CheckPermission(
                "Prescriptions",
                "View");

        if (permissionError != null)
            return permissionError;

        var result =
            await _pharmacyService
                .GetPendingPrescriptionsAsync(
                    PharmacyId,
                    BranchId);

        return Ok(new
        {
            Success = true,

            Total =
                result.Count,

            Items =
                result
        });
    }

    // =====================================================
    // GET PRESCRIPTION DETAILS
    //
    // GET /api/pharmacy/prescriptions/{id}
    //
    // Prescriptions -> View
    // =====================================================

    [HttpGet("prescriptions/{id:int}")]
    public async Task<IActionResult> GetPrescription(
        int id)
    {
        var permissionError =
            await CheckPermission(
                "Prescriptions",
                "View");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid prescription id is required."
            });
        }

        var result =
            await _pharmacyService
                .GetPrescriptionAsync(
                    id,
                    PharmacyId,
                    BranchId);

        if (result == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Prescription not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Prescription = result
        });
    }

    // =====================================================
    // PHARMACY ADMIN / PHARMACIST PRESCRIPTION LIST
    // GET /api/pharmacy/prescriptions
    // Prescriptions -> View
    // =====================================================

    [HttpGet("prescriptions")]
    public async Task<IActionResult> Prescriptions(
        [FromQuery] string? status,
        [FromQuery] string? search)
    {
        var permissionError = await CheckPermission("Prescriptions", "View");
        if (permissionError != null)
            return permissionError;

        var query = _db.Prescriptions.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId);

        // Frontend status labels are normalized to the status values used by
        // the prescription workflow in the database.
        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status.Trim(), "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status.Trim(), "All Statuses", StringComparison.OrdinalIgnoreCase))
        {
            var value = status.Trim().ToLowerInvariant();
            query = value switch
            {
                "pending" => query.Where(x => x.Status == AuthDemo.Models.Prescription.StatusPending),
                "in progress" or "inprogress" or "partiallydispensed" or "partially dispensed" =>
                    query.Where(x => x.Status == AuthDemo.Models.Prescription.StatusPartiallyDispensed),
                "completed" or "dispensed" =>
                    query.Where(x => x.Status == AuthDemo.Models.Prescription.StatusDispensed),
                "cancelled" or "canceled" => query.Where(x => x.Status == AuthDemo.Models.Prescription.StatusCancelled),
                _ => query.Where(x => x.Status.ToLower() == value)
            };
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x =>
                x.Id.ToString().Contains(keyword) ||
                (x.Patient != null && (x.Patient.Name.Contains(keyword) || x.Patient.Phone.Contains(keyword))) ||
                (x.Doctor != null && x.Doctor.Name.Contains(keyword)) ||
                x.Medicines.Any(m => m.Medicine.Name.Contains(keyword)));
        }

        var items = await query
            .OrderByDescending(x => x.PrescriptionDate)
            .Select(x => new
            {
                PrescriptionId = x.Id,
                x.PatientId,
                PatientName = x.Patient != null ? x.Patient.Name : string.Empty,
                PatientMobile = x.Patient != null ? x.Patient.Phone : string.Empty,
                x.DoctorId,
                Doctor = x.Doctor != null ? x.Doctor.Name : string.Empty,
                x.PrescriptionDate,
                Medicines = x.Medicines.Select(m => m.Medicine.Name).ToList(),
                Items = x.Medicines.Count,
                Status = x.Status
            })
            .ToListAsync();

        var all = _db.Prescriptions.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId);

        return Ok(new
        {
            Success = true,
            Summary = new
            {
                TotalPrescriptions = await all.CountAsync(),
                Pending = await all.CountAsync(x => x.Status == AuthDemo.Models.Prescription.StatusPending),
                PartiallyDispensed = await all.CountAsync(x => x.Status == AuthDemo.Models.Prescription.StatusPartiallyDispensed),
                Dispensed = await all.CountAsync(x => x.Status == AuthDemo.Models.Prescription.StatusDispensed),
                Cancelled = await all.CountAsync(x => x.Status == AuthDemo.Models.Prescription.StatusCancelled)
            },
            Items = items
        });
    }

    // =====================================================
    // DISPENSING / BILLING OVERVIEW
    // GET /api/pharmacy/dispensing
    // Frontend fields:
    // Patient, Prescription ID, Medicine, Quantity, Amount,
    // Payment Status, Dispensing Status, Date
    // =====================================================

    [HttpGet("dispensing")]
    public async Task<IActionResult> DispensingOverview(
        [FromQuery] string? search,
        [FromQuery] string? paymentStatus,
        [FromQuery] string? dispensingStatus)
    {
        var permissionError = await CheckPermission("Dispensing", "View");
        if (permissionError != null)
            return permissionError;

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var prescriptions = _db.Prescriptions.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId);

        var bills = _db.PharmacyBills.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            prescriptions = prescriptions.Where(x =>
                x.Id.ToString().Contains(keyword) ||
                (x.Patient != null && x.Patient.Name.Contains(keyword)) ||
                x.Medicines.Any(m => m.Medicine.Name.Contains(keyword)) ||
                bills.Any(b => b.PrescriptionId == x.Id && b.Id.ToString().Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(dispensingStatus) &&
            !string.Equals(dispensingStatus.Trim(), "All", StringComparison.OrdinalIgnoreCase))
        {
            var value = dispensingStatus.Trim().ToLowerInvariant();
            prescriptions = value switch
            {
                "pending" => prescriptions.Where(x => x.Status == "Completed" || x.Status == "Pending"),
                "in progress" or "inprogress" or "partiallydispensed" or "partially dispensed" =>
                    prescriptions.Where(x => x.Status == "PartiallyDispensed"),
                "dispensed" or "completed" => prescriptions.Where(x => x.Status == "Dispensed"),
                "cancelled" or "canceled" => prescriptions.Where(x => x.Status == "Cancelled"),
                _ => prescriptions.Where(x => x.Status.ToLower() == value)
            };
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus) &&
            !string.Equals(paymentStatus.Trim(), "All", StringComparison.OrdinalIgnoreCase))
        {
            var payment = paymentStatus.Trim();
            prescriptions = prescriptions.Where(x =>
                bills.Any(b => b.PrescriptionId == x.Id && b.PaymentStatus == payment));
        }

        var records = await prescriptions
            .OrderByDescending(x => x.PrescriptionDate)
            .Take(200)
            .Select(x => new
            {
                Patient = x.Patient != null ? x.Patient.Name : string.Empty,
                PrescriptionId = x.Id,
                Medicine = x.Medicines.Select(m => m.Medicine.Name).FirstOrDefault() ?? string.Empty,
                Medicines = x.Medicines.Select(m => m.Medicine.Name).ToList(),
                Quantity = x.Medicines.Sum(m => m.Quantity),
                InvoiceId = bills.Where(b => b.PrescriptionId == x.Id).Select(b => (int?)b.Id).FirstOrDefault(),
                Amount = bills.Where(b => b.PrescriptionId == x.Id).Select(b => (decimal?)b.GrandTotal).FirstOrDefault() ?? 0m,
                PaymentStatus = bills.Where(b => b.PrescriptionId == x.Id).Select(b => b.PaymentStatus).FirstOrDefault() ?? "Pending",
                DispensingStatus = x.Status == "PartiallyDispensed" ? "In Progress" :
                    (x.Status == "Completed" || x.Status == "Pending" || x.Status == "Draft" ? "Pending" : x.Status),
                Date = x.PrescriptionDate
            })
            .ToListAsync();

        // "Pending prescriptions" on the dispensing page means prescriptions
        // that are ready for pharmacy dispensing, not doctor drafts.
        var readyForDispensing = await _db.Prescriptions.AsNoTracking()
            .CountAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId &&
                (x.Status == "Completed" || x.Status == "Pending" || x.Status == "PartiallyDispensed"));

        // Count prescriptions dispensed today rather than stock transaction rows,
        // because a single prescription can create multiple stock transactions.
        var todaysDispensing = await _db.StockTransactions.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId &&
                x.TransactionType == "Dispense" && x.TransactionDate >= today && x.TransactionDate < tomorrow &&
                x.ReferenceType == "Prescription")
            .Select(x => x.ReferenceId)
            .Distinct()
            .CountAsync();

        return Ok(new
        {
            Success = true,
            Summary = new
            {
                PendingPrescriptions = readyForDispensing,
                TodaysDispensing = todaysDispensing,
                PendingBills = await bills.CountAsync(x => x.PaymentStatus == "Pending"),
                PaidBills = await bills.CountAsync(x => x.PaymentStatus == "Paid"),
                TodaysRevenue = await bills
                    .Where(x => x.PaymentStatus == "Paid" && x.BillDate >= today && x.BillDate < tomorrow)
                    .SumAsync(x => (decimal?)x.GrandTotal) ?? 0m
            },
            Items = records
        });
    }

    // =====================================================
    // GENERATE PHARMACY BILL
    //
    // POST /api/pharmacy/generate-bill
    //
    // Sales -> Create
    // =====================================================

    [HttpPost("generate-bill")]
    public async Task<IActionResult> GenerateBill(
        [FromBody] GeneratePharmacyBillDto dto)
    {
        var permissionError =
            await CheckPermission(
                "Bills",
                "Create");

        if (permissionError != null)
            return permissionError;

        try
        {
            var billId =
                await _pharmacyService
                    .GenerateBillAsync(
                        dto,
                        PharmacyId,
                        BranchId);

            return Ok(new
            {
                Success = true,

                BillId =
                    billId,

                Message =
                    "Bill generated successfully."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    // =====================================================
    // PAY PHARMACY BILL
    //
    // POST /api/pharmacy/payment
    //
    // Sales -> Create
    // =====================================================

    [HttpPost("payment")]
    public async Task<IActionResult> PayBill(
        [FromBody] PayPharmacyBillDto dto)
    {
        var permissionError =
            await CheckPermission(
                "Bills",
                "Create");

        if (permissionError != null)
            return permissionError;

        try
        {
            var result =
                await _pharmacyService
                    .PayBillAsync(
                        dto,
                        PharmacyId,
                        BranchId);

            return Ok(new
            {
                Success = true,
                Message = result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    // =====================================================
    // GET PHARMACY INVOICE
    //
    // GET /api/pharmacy/invoice/{billId}
    //
    // Sales -> View
    // =====================================================

    [HttpGet("invoice/{billId:int}")]
    public async Task<IActionResult> GetInvoice(
        int billId)
    {
        var permissionError =
            await CheckPermission(
                "Bills",
                "View");

        if (permissionError != null)
            return permissionError;

        if (billId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid bill id is required."
            });
        }

        var result =
            await _pharmacyService
                .GetInvoiceAsync(
                    billId,
                    PharmacyId,
                    BranchId);

        if (result == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Invoice not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Invoice = result
        });
    }

    // =====================================================
    // GET DAILY SALES REPORT
    //
    // GET /api/pharmacy/reports/daily-sales
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/daily-sales")]
    public async Task<IActionResult> DailySales(
        [FromQuery] DateTime date)
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        var reportDate =
            date == default
                ? DateTime.UtcNow.Date
                : date.Date;

        var result =
            await _pharmacyService
                .GetDailySalesReportAsync(
                    reportDate,
                    PharmacyId,
                    BranchId);

        return Ok(new
        {
            Success = true,
            Report = result
        });
    }

    // =====================================================
    // PHARMACY ADMIN - BRANCH MANAGEMENT
    //
    // Standalone PMS ownership:
    // PharmacyTenant -> Branches
    //
    // IMPORTANT:
    // - Only PharmacyAdmin can use these endpoints.
    // - PharmacyId comes only from the authenticated user/token.
    // - Standalone branches belong to a pharmacy.
    // - Client-supplied tenant identifiers are never trusted.
    // =====================================================


    [HttpGet("branches")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> GetBranches(
        [FromQuery] string? search,
        [FromQuery] bool? isActive)
    {
        var permissionError =
            await CheckPermission("Branches", "View");

        if (permissionError != null)
            return permissionError;

        var pharmacy = await _db.Pharmacies
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == PharmacyId &&
                x.IsActive);

        if (pharmacy == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        "No active pharmacy is assigned to this Pharmacy Admin."
                });
        }

        var query = _db.Branches
            .AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId);

        if (isActive.HasValue)
        {
            query = query.Where(x =>
                x.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(x =>
                (x.Name ?? "").Contains(keyword) ||
                (x.Phone ?? "").Contains(keyword) ||
                (x.Email ?? "").Contains(keyword) ||
                (x.Address ?? "").Contains(keyword) ||
                (x.City ?? "").Contains(keyword) ||
                (x.State ?? "").Contains(keyword) ||
                (x.District ?? "").Contains(keyword) ||
                (x.Country ?? "").Contains(keyword) ||
                (x.PostalCode ?? "").Contains(keyword));
        }

        // Execute SQL query first.
        var rows = await query
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
                x.PharmacyId,
                BranchName = x.Name,
                x.Phone,
                x.Email,
                x.Address,
                x.City,
                x.State,
                x.District,
                x.Country,
                x.PostalCode,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        // Calculate Location in C# after SQL execution.
        var branches = rows.Select(x => new
        {
            x.BranchId,
            x.PharmacyId,
            x.BranchName,
            x.Phone,
            x.Email,
            x.Address,
            x.City,
            x.State,
            x.District,
            x.Country,
            x.PostalCode,

            Location = string.Join(
                ", ",
                new[]
                {
                x.Address,
                x.City,
                x.District,
                x.State,
                x.Country,
                x.PostalCode
                }
                .Where(v => !string.IsNullOrWhiteSpace(v))),

            x.IsActive,
            Status = x.IsActive ? "Active" : "Inactive",
            x.CreatedAt
        }).ToList();

        var allBranches = _db.Branches
            .AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId);

        return Ok(new
        {
            Success = true,

            Pharmacy = new
            {
                pharmacy.Id,
                PharmacyName = pharmacy.Name,
                OperationMode = "Standalone"
            },

            Summary = new
            {
                TotalBranches = await allBranches.CountAsync(),
                ActiveBranches = await allBranches.CountAsync(
                    x => x.IsActive),
                InactiveBranches = await allBranches.CountAsync(
                    x => !x.IsActive)
            },

            Branches = branches
        });
    }


    [HttpGet("branches/{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> GetBranch(
        int id)
    {
        var permissionError =
            await CheckPermission("Branches", "View");

        if (permissionError != null)
            return permissionError;

        var pharmacyExists =
            await _db.Pharmacies
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == PharmacyId &&
                    x.IsActive);

        if (!pharmacyExists)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        "No active pharmacy is assigned to this Pharmacy Admin."
                });
        }

        var branch =
            await _db.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    (_db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == id) &&
                    x.PharmacyId == PharmacyId);

        if (branch == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Branch was not found in your assigned pharmacy."
            });
        }

        return Ok(new
        {
            Success = true,

            Branch = new
            {
                BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == branch.PharmacyId && i.ExistingBranchId == branch.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
                branch.PharmacyId,
                BranchName = branch.Name,
                branch.Phone,
                branch.Email,
                branch.Address,
                branch.City,
                branch.State,
                branch.District,
                branch.Country,
                branch.PostalCode,
                branch.IsActive,

                Status =
                    branch.IsActive
                        ? "Active"
                        : "Inactive",

                branch.CreatedAt
            }
        });
    }


    [HttpPost("branches")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> CreateBranch(
        [FromBody] CreatePharmacyBranchDto dto)
    {
        var permissionError =
            await CheckPermission("Branches", "Create");

        if (permissionError != null)
            return permissionError;

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var pharmacy =
            await _db.Pharmacies
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == PharmacyId &&
                    x.IsActive);

        if (pharmacy == null)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        "No active pharmacy is assigned to this Pharmacy Admin."
                });
        }

        var branchName =
            dto.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(branchName))
        {
            return BadRequest(new
            {
                Success = false,
                Message = "Branch name is required."
            });
        }

        var normalizedName =
            branchName.ToLower();

        var duplicate =
            await _db.Branches.AnyAsync(x =>
                x.PharmacyId == PharmacyId &&
                x.Name.ToLower() == normalizedName);

        if (duplicate)
        {
            return Conflict(new
            {
                Success = false,
                Message =
                    "A branch with this name already exists for your pharmacy."
            });
        }

        await using var branchNumberTx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await _db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {"BranchIdentity-" + PharmacyId}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {15000}; IF @r < 0 THROW 51000, 'Unable to allocate branch ID', 1;");
        var nextBranchNumber = (await _db.PharmacyBranchIdentities.Where(x => x.PharmacyId == PharmacyId).MaxAsync(x => (int?)x.BranchId)) ?? 0;

        var branch =
            new AuthDemo.Models.Branch
            {
                // Never accept PharmacyId from the client.
                PharmacyId = PharmacyId,

                // Standalone PMS branch does not require Hospital.

                Name = branchName,

                Phone =
                    string.IsNullOrWhiteSpace(dto.Phone)
                        ? null
                        : dto.Phone.Trim(),

                Email =
                    string.IsNullOrWhiteSpace(dto.Email)
                        ? null
                        : dto.Email.Trim(),

                Address =
                    string.IsNullOrWhiteSpace(dto.Address)
                        ? null
                        : dto.Address.Trim(),

                City =
                    string.IsNullOrWhiteSpace(dto.City)
                        ? null
                        : dto.City.Trim(),

                State =
                    string.IsNullOrWhiteSpace(dto.State)
                        ? null
                        : dto.State.Trim(),

                District =
                    string.IsNullOrWhiteSpace(dto.District)
                        ? null
                        : dto.District.Trim(),

                Country =
                    string.IsNullOrWhiteSpace(dto.Country)
                        ? null
                        : dto.Country.Trim(),

                PostalCode =
                    string.IsNullOrWhiteSpace(dto.PostalCode)
                        ? null
                        : dto.PostalCode.Trim(),

                IsActive = true,
                SourceSystem = "PharmacyStandalone",
                CreatedAt = DateTime.UtcNow
            };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        {
            _db.PharmacyBranchIdentities.Add(new PharmacyBranchIdentity
            {
                PharmacyId = PharmacyId,
                BranchId = nextBranchNumber + 1,
                ExistingBranchId = branch.Id
            });
            await _db.SaveChangesAsync();
        }
        await branchNumberTx.CommitAsync();

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                Success = true,
                Message = "Branch created successfully.",

                Pharmacy = new
                {
                    pharmacy.Id,
                    PharmacyName = pharmacy.Name
                },

                Branch = new
                {
                    BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == branch.PharmacyId && i.ExistingBranchId == branch.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
                    branch.PharmacyId,
                    BranchName = branch.Name,
                    branch.Phone,
                    branch.Email,
                    branch.Address,
                    branch.City,
                    branch.State,
                    branch.District,
                    branch.Country,
                    branch.PostalCode,
                    branch.IsActive,
                    Status = "Active",
                    branch.CreatedAt
                }
            });
    }


    [HttpPut("branches/{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> UpdateBranch(
        int id,
        [FromBody] UpdatePharmacyBranchDto dto)
    {
        var permissionError =
            await CheckPermission("Branches", "Edit");

        if (permissionError != null)
            return permissionError;

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var branch =
            await _db.Branches
                .FirstOrDefaultAsync(x =>
                    (_db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == id) &&
                    x.PharmacyId == PharmacyId);

        if (branch == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Branch was not found in your assigned pharmacy."
            });
        }

        var branchName =
            dto.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(branchName))
        {
            return BadRequest(new
            {
                Success = false,
                Message = "Branch name is required."
            });
        }

        var normalizedName =
            branchName.ToLower();

        var duplicate =
            await _db.Branches.AnyAsync(x =>
                x.Id != id &&
                x.PharmacyId == PharmacyId &&
                x.Name.ToLower() == normalizedName);

        if (duplicate)
        {
            return Conflict(new
            {
                Success = false,
                Message =
                    "Another branch with this name already exists for your pharmacy."
            });
        }

        branch.Name = branchName;

        branch.Phone =
            string.IsNullOrWhiteSpace(dto.Phone)
                ? null
                : dto.Phone.Trim();

        branch.Email =
            string.IsNullOrWhiteSpace(dto.Email)
                ? null
                : dto.Email.Trim();

        branch.Address =
            string.IsNullOrWhiteSpace(dto.Address)
                ? null
                : dto.Address.Trim();

        branch.City =
            string.IsNullOrWhiteSpace(dto.City)
                ? null
                : dto.City.Trim();

        branch.State =
            string.IsNullOrWhiteSpace(dto.State)
                ? null
                : dto.State.Trim();

        branch.District =
            string.IsNullOrWhiteSpace(dto.District)
                ? null
                : dto.District.Trim();

        branch.Country =
            string.IsNullOrWhiteSpace(dto.Country)
                ? null
                : dto.Country.Trim();

        branch.PostalCode =
            string.IsNullOrWhiteSpace(dto.PostalCode)
                ? null
                : dto.PostalCode.Trim();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Branch updated successfully.",

            Branch = new
            {
                BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == branch.PharmacyId && i.ExistingBranchId == branch.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
                branch.PharmacyId,
                BranchName = branch.Name,
                branch.Phone,
                branch.Email,
                branch.Address,
                branch.City,
                branch.State,
                branch.District,
                branch.Country,
                branch.PostalCode,
                branch.IsActive,

                Status =
                    branch.IsActive
                        ? "Active"
                        : "Inactive",

                branch.CreatedAt
            }
        });
    }


    [HttpPatch("branches/{id:int}/status")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> ChangeBranchStatus(
        int id,
        [FromBody] StatusDto dto)
    {
        var permissionError =
            await CheckPermission("Branches", "Edit");

        if (permissionError != null)
            return permissionError;

        var branch =
            await _db.Branches
                .FirstOrDefaultAsync(x =>
                    (_db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == id) &&
                    x.PharmacyId == PharmacyId);

        if (branch == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Branch was not found in your assigned pharmacy."
            });
        }

        branch.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                dto.IsActive
                    ? "Branch activated successfully."
                    : "Branch deactivated successfully.",

            BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == branch.PharmacyId && i.ExistingBranchId == branch.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
            branch.PharmacyId,
            branch.IsActive,

            Status =
                branch.IsActive
                    ? "Active"
                    : "Inactive"
        });
    }


    [HttpDelete("branches/{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> DeleteBranch(
        int id)
    {
        var permissionError =
            await CheckPermission("Branches", "Delete");

        if (permissionError != null)
            return permissionError;

        var branch =
            await _db.Branches
                .FirstOrDefaultAsync(x =>
                    (_db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == id) &&
                    x.PharmacyId == PharmacyId);

        if (branch == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Branch was not found in your assigned pharmacy."
            });
        }

        if (branch.IsMainBranch)
        {
            return Conflict(new
            {
                Success = false,
                Message = "The Main Branch cannot be deleted. Update or deactivate the pharmacy instead."
            });
        }

        var hasUsers =
            await _db.Users.AnyAsync(x =>
                x.BranchId == branch.Id);

        var hasMedicines =
            await _db.Medicines.AnyAsync(x =>
                x.BranchId == branch.Id);

        var hasInventory =
            await _db.Inventories.AnyAsync(x =>
                x.BranchId == branch.Id);

        var hasMedicineImports =
            await _db.MedicineImports.AnyAsync(x =>
                x.BranchId == branch.Id);

        var hasStockTransactions =
            await _db.StockTransactions.AnyAsync(x =>
                x.BranchId == branch.Id);

        var hasPrescriptions =
            await _db.Prescriptions.AnyAsync(x =>
                x.BranchId == branch.Id);

        var hasBills =
            await _db.PharmacyBills.AnyAsync(x =>
                x.BranchId == branch.Id);

        if (hasUsers ||
            hasMedicines ||
            hasInventory ||
            hasMedicineImports ||
            hasStockTransactions ||
            hasPrescriptions ||
            hasBills)
        {
            return Conflict(new
            {
                Success = false,
                Message =
                    "This branch contains pharmacy data and cannot be permanently deleted. Deactivate the branch instead.",
                CanDeactivate = true
            });
        }

        var identity = await _db.PharmacyBranchIdentities
            .FirstOrDefaultAsync(x => x.PharmacyId == PharmacyId && x.ExistingBranchId == branch.Id);
        if (identity != null) _db.PharmacyBranchIdentities.Remove(identity);
        _db.Branches.Remove(branch);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Branch deleted successfully."
        });
    }

}