using System.ComponentModel.DataAnnotations;
using System.Data;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public sealed class SubmitPatientReturnDto
{
    public int? BranchId { get; set; } // Pharmacy-scoped public branch ID, required for PharmacyAdmin.
    [Range(1, int.MaxValue)] public int BillId { get; set; }
    [MaxLength(200)] public string? PatientName { get; set; }
    [Required, MaxLength(200)] public string Reason { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Notes { get; set; }
    public bool VerifiedForRestocking { get; set; }
    [Required, MinLength(1)] public List<PatientReturnedMedicineDto> Medicines { get; set; } = new();
}

public sealed class PatientReturnedMedicineDto
{
    [Range(1, int.MaxValue)] public int MedicineId { get; set; }
    [Required, MaxLength(100)] public string OriginalBatch { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    [Required, MaxLength(40)] public string Condition { get; set; } = string.Empty;
}

/// <summary>
/// Phase 13R: additive multi-medicine patient returns for the pharmacist Returns screens.
/// Leaves existing POST/GET/approve/cancel /api/pharmacy/returns APIs unchanged.
/// </summary>
[ApiController]
[Route("api/pharmacy/returns")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public sealed class PharmacyPatientReturnWorkflowController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    public PharmacyPatientReturnWorkflowController(AppDbContext db) => _db = db;

    private async Task<IActionResult?> Permit(string action)
    {
        // Check actual signed-in account and user-specific permissions. No implicit access.
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == UserId && x.IsActive && x.AccountStatus == "Active" &&
            x.PharmacyId == PharmacyId && (x.Role == "PharmacyAdmin" || x.Role == "Pharmacist"));
        if (user is null || !await _db.Pharmacies.AsNoTracking().AnyAsync(x =>
            x.Id == PharmacyId && x.IsActive && !x.IsDeleted))
            return StatusCode(403, new { success = false, message = "Account or pharmacy is not active." });
        var permissions = await _db.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == UserId && x.Module == "Returns").ToListAsync();
        var allowed = permissions.Any(x => action switch
        {
            "View" => x.CanView,
            "Create" => x.CanCreate,
            "Edit" => x.CanEdit,
            "Delete" => x.CanDelete,
            _ => false
        });
        return allowed ? null : StatusCode(403, new { success = false,
            message = $"You do not have {action} permission for Returns." });
    }

    private async Task<(int? branchId, IActionResult? error)> ResolveBranch(int? publicBranchId, bool required)
    {
        if (IsPharmacist)
        {
            // Do not trust a supplied branch ID to expand a pharmacist's assigned scope.
            var assigned = await _db.Users.AsNoTracking()
                .Where(x => x.Id == UserId && x.PharmacyId == PharmacyId && x.IsActive)
                .Select(x => x.BranchId).SingleOrDefaultAsync();
            if (!assigned.HasValue || !await _db.Branches.AsNoTracking().AnyAsync(x =>
                x.Id == assigned.Value && x.PharmacyId == PharmacyId && x.IsActive))
                return (null, StatusCode(403, new { success = false, message = "No active assigned branch." }));
            if (publicBranchId.HasValue)
            {
                var matching = await _db.PharmacyBranchIdentities.AsNoTracking().AnyAsync(x =>
                    x.PharmacyId == PharmacyId && x.BranchId == publicBranchId.Value &&
                    x.ExistingBranchId == assigned.Value);
                if (!matching)
                    return (null, StatusCode(403, new { success = false, message = "Branch is outside your assignment." }));
            }
            return (assigned.Value, null);
        }
        if (!IsPharmacyAdmin)
            return (null, StatusCode(403, new { success = false, message = "Role is not permitted." }));
        if (!publicBranchId.HasValue)
            return required
                ? (null, BadRequest(new { success = false, message = "Public branchId is required for Admin." }))
                : (null, null);
        var internalId = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == publicBranchId.Value)
            .Select(x => (int?)x.ExistingBranchId).SingleOrDefaultAsync();
        if (!internalId.HasValue || !await _db.Branches.AsNoTracking().AnyAsync(x =>
            x.Id == internalId.Value && x.PharmacyId == PharmacyId && x.IsActive))
            return (null, BadRequest(new { success = false, message = "Invalid branchId for this pharmacy." }));
        return (internalId, null);
    }

    private static string? NormalizeCondition(string? value)
    {
        return value?.Trim().Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "eligibleforrestocking" or "restock" => "EligibleForRestocking",
            "damaged" => "Damaged",
            "expired" => "Expired",
            "opened" => "Opened",
            "quarantine" => "Quarantine",
            _ => null
        };
    }

    [HttpGet("options")]
    public async Task<IActionResult> Options()
    {
        var error = await Permit("View");
        if (error != null) return error;
        return Ok(new { success = true, data = new
        {


            reasons = new[]

{

    "Unused medicines",

    "Incorrect supply",

    "Damaged packaging",

    "Expired medicine",

    "Other"

},

            conditions = new[]

{

    "EligibleForRestocking",

    "Damaged",

    "Expired"

},


            requiresOriginalBill = true,
            restockRequiresApproval = true
        }});
    }

    [HttpGet("bills/{billId:int}/items")]
    public async Task<IActionResult> BillItems(int billId, [FromQuery] int? branchId = null, [FromQuery] string? q = null)
    {
        var error = await Permit("View");
        if (error != null) return error;
        var scope = await ResolveBranch(branchId, true);
        if (scope.error != null) return scope.error;
        var bill = await _db.PharmacyBills.AsNoTracking()
            .Where(x => x.Id == billId && x.PharmacyId == PharmacyId && x.BranchId == scope.branchId)
            .Select(x => new { x.Id, x.PatientId, x.GrandTotal, x.PaymentStatus }).SingleOrDefaultAsync();
        if (bill == null) return NotFound(new { success = false, message = "Bill not found in this branch." });
        var patient = await _db.Patients.AsNoTracking().Where(x => x.Id == bill.PatientId)
            .Select(x => new { x.Name, x.PharmacyPatientId }).FirstOrDefaultAsync();
        var soldItems = await _db.PharmacyBillItems.AsNoTracking()
            .Where(x => x.PharmacyBillId == billId)
            .GroupBy(x => new { x.MedicineId, x.MedicineName })
            .Select(g => new { g.Key.MedicineId, g.Key.MedicineName, Sold = g.Sum(x => x.Quantity) })
            .OrderBy(x => x.MedicineName).ToListAsync();
        var medicineIds = soldItems.Select(x => x.MedicineId).ToList();
        var returned = await _db.PharmacyReturns.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == scope.branchId &&
                x.ReturnType == "Patient" && x.PharmacyBillId == billId &&
                x.Status != "Cancelled" && medicineIds.Contains(x.MedicineId))
            .GroupBy(x => x.MedicineId).Select(x => new { MedicineId = x.Key, Quantity = x.Sum(y => y.Quantity) })
            .ToDictionaryAsync(x => x.MedicineId, x => x.Quantity);
        var batches = await _db.MedicineBatches.AsNoTracking()
            .Where(x => x.Inventory.PharmacyId == PharmacyId && x.Inventory.BranchId == scope.branchId &&
                medicineIds.Contains(x.Inventory.MedicineId))
            .Select(x => new { x.Inventory.MedicineId, x.Id, x.BatchNumber, x.ExpiryDate,
                x.IsQuarantined, x.IsRecalled }).ToListAsync();
        var filtered = soldItems.Where(x => string.IsNullOrWhiteSpace(q) ||
            x.MedicineName.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));
        var result = filtered.Select(x => new
        {
            medicineId = x.MedicineId,
            medicineName = x.MedicineName,
            soldQuantity = x.Sold,
            alreadyReturned = returned.GetValueOrDefault(x.MedicineId),
            returnableQuantity = Math.Max(0m, x.Sold - returned.GetValueOrDefault(x.MedicineId)),
            originalBatchOptions = batches.Where(b => b.MedicineId == x.MedicineId)
                .Select(b => new { batchId = b.Id, batchNumber = b.BatchNumber, expiryDate = b.ExpiryDate,
                    eligibleForRestocking = b.ExpiryDate.Date >= DateTime.UtcNow.Date && !b.IsQuarantined && !b.IsRecalled })
                .ToArray()
        }).ToArray();
        return Ok(new { success = true, data = new { billId, patient = patient?.Name,
            patientId = patient?.PharmacyPatientId, billTotal = bill.GrandTotal, paymentStatus = bill.PaymentStatus,
            items = result, total = result.Length } });
    }

    [HttpGet("eligible-medicines")]
    public Task<IActionResult> EligibleMedicines([FromQuery] int billId, [FromQuery] int? branchId = null,
        [FromQuery] string? q = null) => BillItems(billId, branchId, q);

    [HttpPost("patient")]
    public async Task<IActionResult> Submit([FromBody] SubmitPatientReturnDto dto)
    {
        var error = await Permit("Create");
        if (error != null) return error;
        var scope = await ResolveBranch(dto.BranchId, true);
        if (scope.error != null) return scope.error;
        var branch = scope.branchId!.Value;
        if (dto.Medicines == null || dto.Medicines.Count < 1 || dto.Medicines.Count > 50 ||
            string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 200)
            return BadRequest(new { success = false, message = "Valid reason and 1–50 returned medicines are required." });
        if (dto.Medicines.Any(x => x.MedicineId <= 0 || x.Quantity <= 0 ||
            string.IsNullOrWhiteSpace(x.OriginalBatch) || x.OriginalBatch.Length > 100 ||
            NormalizeCondition(x.Condition) == null))
            return BadRequest(new { success = false, message = "Each medicine needs a valid ID, original batch, quantity and condition." });
        if (dto.Medicines.Select(x => x.MedicineId).Distinct().Count() != dto.Medicines.Count)
            return BadRequest(new { success = false, message = "Combine quantities for the same medicine into one line." });
        if (dto.Medicines.Any(x => NormalizeCondition(x.Condition) == "EligibleForRestocking") && !dto.VerifiedForRestocking)
            return BadRequest(new { success = false, message = "Confirm verification before submitting restock-eligible medicines." });

        // Serializability prevents parallel patient returns from exceeding the sold quantity.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var bill = await _db.PharmacyBills.AsNoTracking().Where(x =>
            x.Id == dto.BillId && x.PharmacyId == PharmacyId && x.BranchId == branch)
            .Select(x => new { x.Id, x.PatientId }).FirstOrDefaultAsync();
        if (bill == null) return NotFound(new { success = false, message = "Original bill not found in this branch." });
        var billPatient = await _db.Patients.AsNoTracking()
            .Where(x => x.Id == bill.PatientId).Select(x => x.Name).FirstOrDefaultAsync();
        var request = new PharmacyPatientReturnRequest
        {
            PharmacyId = PharmacyId, BranchId = branch, PharmacyBillId = dto.BillId,
            PatientName = (dto.PatientName ?? billPatient ?? string.Empty).Trim(),
            Reason = dto.Reason.Trim(), Notes = (dto.Notes ?? string.Empty).Trim(),
            VerifiedForRestocking = dto.VerifiedForRestocking, CreatedByUserId = UserId
        };
        _db.PharmacyPatientReturnRequests.Add(request);
        foreach (var line in dto.Medicines)
        {
            var medicine = await _db.Medicines.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == line.MedicineId && x.PharmacyId == PharmacyId && x.BranchId == branch);
            if (medicine == null)
                return BadRequest(new { success = false, message = $"Medicine {line.MedicineId} not found in this branch." });
            var sold = await _db.PharmacyBillItems.AsNoTracking()
                .Where(x => x.PharmacyBillId == dto.BillId && x.MedicineId == line.MedicineId)
                .SumAsync(x => (decimal?)x.Quantity) ?? 0m;
            if (sold <= 0)
                return BadRequest(new { success = false, message = $"Medicine {medicine.Name} is not on this bill." });
            var returned = await _db.PharmacyReturns.AsNoTracking()
                .Where(x => x.PharmacyId == PharmacyId && x.BranchId == branch &&
                    x.ReturnType == "Patient" && x.PharmacyBillId == dto.BillId &&
                    x.MedicineId == line.MedicineId && x.Status != "Cancelled")
                .SumAsync(x => (int?)x.Quantity) ?? 0;
            if (returned + line.Quantity > sold)
                return BadRequest(new { success = false, message = $"Return quantity exceeds sold quantity for {medicine.Name}.",
                    soldQuantity = sold, alreadyReturned = returned });
            var batch = await _db.MedicineBatches.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Inventory.PharmacyId == PharmacyId && x.Inventory.BranchId == branch &&
                x.Inventory.MedicineId == line.MedicineId && x.BatchNumber == line.OriginalBatch.Trim());
            if (batch == null)
                return BadRequest(new { success = false, message = $"Original batch was not found for {medicine.Name}." });
            var condition = NormalizeCondition(line.Condition)!;
            var restock = condition == "EligibleForRestocking";
            if (restock && (batch.ExpiryDate.Date < DateTime.UtcNow.Date || batch.IsQuarantined ||
                batch.IsRecalled || medicine.IsRecalled))
                return BadRequest(new { success = false, message = $"Batch for {medicine.Name} cannot be restocked." });
            var entity = new PharmacyReturn
            {
                PharmacyId = PharmacyId, BranchId = branch, ReturnType = "Patient",
                PharmacyBillId = dto.BillId, MedicineId = line.MedicineId,
                MedicineBatchId = batch.Id, Quantity = line.Quantity,
                RefundAmount = 0m, // Refunds must be recorded separately via billing/payment workflow.
                Reason = request.Reason, Disposition = restock ? "Restock" : "Quarantine",
                Status = "Pending", CreatedByUserId = UserId
            };
            request.Lines.Add(new PharmacyPatientReturnRequestLine
            {
                PharmacyReturn = entity, OriginalBatchNumber = line.OriginalBatch.Trim(), Condition = condition
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreatedAtAction(nameof(HistoryDetail), new { requestId = request.Id }, new
        {
            success = true, message = "Return request submitted for approval; stock is unchanged.",
            data = new { requestId = request.Id, billId = dto.BillId, status = "Pending",
                totalMedicines = request.Lines.Count,
                totalQuantity = dto.Medicines.Sum(x => x.Quantity), refundApplied = false }
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int? branchId = null, [FromQuery] string? status = null,
        [FromQuery] string? q = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var error = await Permit("View");
        if (error != null) return error;
        var scope = await ResolveBranch(branchId, false);
        if (scope.error != null) return scope.error;
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { success = false, message = "page must be positive; pageSize must be 1–100." });
        var query = _db.PharmacyReturns.AsNoTracking().Where(x =>
            x.PharmacyId == PharmacyId && x.ReturnType == "Patient");
        if (scope.branchId.HasValue) query = query.Where(x => x.BranchId == scope.branchId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var numeric = int.TryParse(term, out var parsed) ? parsed : -1;
            query = query.Where(x => x.Id == numeric || x.PharmacyBillId == numeric ||
                _db.Medicines.Any(m => m.Id == x.MedicineId && m.Name.Contains(term)) ||
                _db.PharmacyBills.Any(b => b.Id == x.PharmacyBillId &&
                    _db.Patients.Any(p => p.Id == b.PatientId && p.Name.Contains(term))) ||
                _db.PharmacyPatientReturnRequestLines.Any(l => l.PharmacyReturnId == x.Id &&
                    l.Request.PatientName.Contains(term)));
        }
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                returnId = x.Id, billId = x.PharmacyBillId, medicineId = x.MedicineId,
                medicine = _db.Medicines.Where(m => m.Id == x.MedicineId).Select(m => m.Name).FirstOrDefault(),
                patient = _db.PharmacyPatientReturnRequestLines.Where(l => l.PharmacyReturnId == x.Id)
                    .Select(l => l.Request.PatientName).FirstOrDefault() ??
                    _db.PharmacyBills.Where(b => b.Id == x.PharmacyBillId)
                    .Select(b => _db.Patients.Where(p => p.Id == b.PatientId).Select(p => p.Name).FirstOrDefault())
                    .FirstOrDefault(),
                requestId = _db.PharmacyPatientReturnRequestLines.Where(l => l.PharmacyReturnId == x.Id)
                    .Select(l => (int?)l.RequestId).FirstOrDefault(),
                x.Quantity, x.Status, x.Reason, x.Disposition, x.BranchId, x.CreatedAt
            }).ToListAsync();
        var publicMap = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId)
            .ToDictionaryAsync(x => x.ExistingBranchId, x => x.BranchId);
        // No fallback to leaking internal IDs when a mapping is missing.
        if (rows.Any(x => !publicMap.ContainsKey(x.BranchId)))
            return StatusCode(500, new { success = false, message = "Branch public-ID mapping is missing." });
        var items = rows.Select(x => new { x.returnId, x.requestId, x.billId,
            patient = x.patient ?? string.Empty, x.medicine, x.Quantity, x.Status,
            x.Reason, x.Disposition, branchId = publicMap[x.BranchId], x.CreatedAt }).ToArray();
        return Ok(new { success = true, data = new { items, total, page, pageSize,
            hasMore = ((long)page * pageSize) < total } });
    }

    [HttpGet("history/{requestId:int}")]
    public async Task<IActionResult> HistoryDetail(int requestId)
    {
        var error = await Permit("View");
        if (error != null) return error;
        var request = await _db.PharmacyPatientReturnRequests.AsNoTracking()
            .Where(x => x.Id == requestId && x.PharmacyId == PharmacyId)
            .Select(x => new { x.Id, x.BranchId, x.PharmacyBillId, x.PatientName, x.Reason,
                x.Notes, x.VerifiedForRestocking, x.CreatedAt, x.CreatedByUserId }).FirstOrDefaultAsync();
        if (request == null) return NotFound(new { success = false, message = "Return request not found." });
        var scope = await ResolveBranch(null, false);
        if (scope.error != null) return scope.error;
        if (scope.branchId.HasValue && request.BranchId != scope.branchId.Value)
            return StatusCode(403, new { success = false, message = "Request is outside your branch." });
        var pub = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.ExistingBranchId == request.BranchId)
            .Select(x => (int?)x.BranchId).FirstOrDefaultAsync();
        if (!pub.HasValue) return StatusCode(500, new { success = false, message = "Branch public-ID mapping is missing." });
        var lines = await _db.PharmacyPatientReturnRequestLines.AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .Select(x => new { returnId = x.PharmacyReturnId, medicineId = x.PharmacyReturn.MedicineId,
                medicine = _db.Medicines.Where(m => m.Id == x.PharmacyReturn.MedicineId).Select(m => m.Name).FirstOrDefault(),
                x.OriginalBatchNumber, x.Condition,
                quantity = x.PharmacyReturn.Quantity, status = x.PharmacyReturn.Status,
                disposition = x.PharmacyReturn.Disposition }).ToListAsync();
        return Ok(new { success = true, data = new { requestId = request.Id,
            billId = request.PharmacyBillId, branchId = pub.Value, patient = request.PatientName,
            request.Reason, request.Notes, request.VerifiedForRestocking, request.CreatedAt,
            status = lines.All(x => x.status == "Approved") ? "Approved" :
                lines.All(x => x.status == "Cancelled") ? "Cancelled" :
                lines.Any(x => x.status == "Approved" || x.status == "Cancelled") ? "PartiallyProcessed" : "Pending",
            items = lines } });
    }

    [HttpPost("history/{requestId:int}/approve")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> ApproveRequest(int requestId)
    {
        var error = await Permit("Edit");
        if (error != null) return error;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await _db.PharmacyPatientReturnRequests.Include(x => x.Lines)
            .ThenInclude(x => x.PharmacyReturn)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.PharmacyId == PharmacyId);
        if (request == null) return NotFound(new { success = false, message = "Return request not found." });
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.PharmacyReturn.Status != "Pending"))
            return Conflict(new { success = false, message = "Only fully pending return requests can be approved." });
        foreach (var line in request.Lines)
        {
            var item = line.PharmacyReturn;
            if (line.Condition == "EligibleForRestocking")
            {
                var inventory = await _db.Inventories.FirstOrDefaultAsync(x =>
                    x.PharmacyId == PharmacyId && x.BranchId == request.BranchId && x.MedicineId == item.MedicineId);
                if (inventory == null || !item.MedicineBatchId.HasValue)
                    return Conflict(new { success = false, message = "Inventory or batch missing; nothing changed." });
                var batch = await _db.MedicineBatches.FirstOrDefaultAsync(x =>
                    x.Id == item.MedicineBatchId.Value && x.InventoryId == inventory.Id);
                var medicineIsRecalled = await _db.Medicines.AsNoTracking()
                    .AnyAsync(x => x.Id == item.MedicineId && x.IsRecalled);
                if (batch == null || batch.ExpiryDate.Date < DateTime.UtcNow.Date ||
                    batch.IsQuarantined || batch.IsRecalled || medicineIsRecalled)
                    return Conflict(new { success = false, message = "A return batch is no longer eligible for restocking." });
                inventory.CurrentStock += item.Quantity;
                inventory.AvailableStock += item.Quantity;
                inventory.LastUpdated = DateTime.UtcNow;
                batch.RemainingQuantity += item.Quantity;
                _db.StockTransactions.Add(new StockTransaction
                {
                    PharmacyId = PharmacyId, BranchId = request.BranchId,
                    MedicineId = item.MedicineId, MedicineBatchId = batch.Id,
                    TransactionType = "PatientReturnRestock", Quantity = item.Quantity,
                    BalanceStock = inventory.CurrentStock, Remarks = request.Reason,
                    ReferenceType = "PharmacyReturn", ReferenceId = item.Id,
                    CreatedByUserId = UserId, TransactionDate = DateTime.UtcNow
                });
            }
            item.Status = "Approved";
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { success = true, message = "Return approved. Only eligible medicines were restocked.",
            data = new { requestId, status = "Approved", updatedLines = request.Lines.Count } });
    }

    [HttpPost("history/{requestId:int}/cancel")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> CancelRequest(int requestId)
    {
        var error = await Permit("Delete");
        if (error != null) return error;
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await _db.PharmacyPatientReturnRequests.Include(x => x.Lines)
            .ThenInclude(x => x.PharmacyReturn)
            .FirstOrDefaultAsync(x => x.Id == requestId && x.PharmacyId == PharmacyId);
        if (request == null) return NotFound(new { success = false, message = "Return request not found." });
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.PharmacyReturn.Status != "Pending"))
            return Conflict(new { success = false, message = "Only fully pending requests can be cancelled." });
        foreach (var line in request.Lines) line.PharmacyReturn.Status = "Cancelled";
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { success = true, message = "Return request cancelled; no stock was changed.",
            data = new { requestId, status = "Cancelled" } });
    }
}
