using AuthDemo.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacist/inventory")]
[Authorize(Roles = "Pharmacist")]
public sealed class PharmacistInventoryController
    : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;

    public PharmacistInventoryController(
        AppDbContext db)
    {
        _db = db;
    }

    // =====================================================
    // CHECK PHARMACIST INVENTORY PERMISSION
    //
    // Pharmacist permissions come from:
    // UserPermissions
    //
    // Module:
    // Inventory
    // =====================================================

    // Pharmacists have read-only access to inventory for their assigned branch.
    // Authorization is enforced by the controller role and database assignment;
    // every inventory query remains scoped to PharmacyId and BranchId.
    private Task<IActionResult?> CheckInventoryPermission(
        string action,
        CancellationToken ct = default)
    {
        if (!string.Equals(action?.Trim(), "View", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IActionResult?>(StatusCode(
                StatusCodes.Status403Forbidden,
                new { Success = false, Message = "Pharmacists have read-only inventory access." }));
        return Task.FromResult<IActionResult?>(null);
    }

    // =====================================================
    // LIST INVENTORY
    //
    // GET /api/pharmacist/inventory
    //
    // Inventory -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var permissionError =
            await CheckInventoryPermission(
                "View",
                ct);

        if (permissionError != null)
            return permissionError;

        // Verify the current assignment against the database (not only JWT claims).
        var assigned = await _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == UserId && u.Role == "Pharmacist" && u.IsActive &&
            u.PharmacyId == PharmacyId && u.BranchId == BranchId &&
            u.Branch != null && u.Branch.PharmacyId == PharmacyId && u.Branch.IsActive,
            ct);
        if (!assigned)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "No active pharmacist assignment for this pharmacy branch." });

        page =
            Math.Max(
                1,
                page);

        pageSize =
            Math.Clamp(
                pageSize,
                1,
                100);

        var query =
            _db.Inventories
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword =
                search.Trim();

            query =
                query.Where(x =>
                    x.Medicine.Name.Contains(
                        keyword) ||
                    x.Medicine.MedicineCode.Contains(
                        keyword) ||
                    x.Medicine.BrandName.Contains(
                        keyword) ||
                    x.Medicine.GenericName.Contains(
                        keyword));
        }

        var total =
            await query.CountAsync(ct);

        var items =
            await query
                .OrderBy(x =>
                    x.Medicine.Name)
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.MedicineId,

                    x.Medicine.MedicineCode,

                    x.Medicine.Name,

                    x.Medicine.BrandName,

                    x.Medicine.GenericName,

                    x.Medicine.Strength,

                    x.Medicine.DosageForm,

                    x.Medicine.Category,

                    x.CurrentStock,

                    x.AvailableStock,

                    x.ReservedStock,

                    x.MinimumStock,

                    x.MaximumStock,

                    x.ReorderLevel,

                    x.StorageLocation,

                    x.LastUpdated,

                    Status =
                        x.CurrentStock <= 0
                            ? "OutOfStock"
                            : x.CurrentStock <=
                              x.ReorderLevel
                                ? "LowStock"
                                : "Available"
                })
                .ToListAsync(ct);

        return Ok(new
        {
            Success = true,

            Page =
                page,

            PageSize =
                pageSize,

            Total =
                total,

            TotalPages =
                total == 0
                    ? 0
                    : (int)Math.Ceiling(
                        total /
                        (double)pageSize),

            Items =
                items
        });
    }

    // =====================================================
    // GET MEDICINE BATCHES
    //
    // GET /api/pharmacist/inventory/{medicineId}/batches
    //
    // Inventory -> View
    // =====================================================

    [HttpGet("{medicineId:int}/batches")]
    public async Task<IActionResult> Batches(
        int medicineId,
        CancellationToken ct = default)
    {
        var permissionError =
            await CheckInventoryPermission(
                "View",
                ct);

        if (permissionError != null)
            return permissionError;

        // Verify the current assignment against the database (not only JWT claims).
        var assigned = await _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == UserId && u.Role == "Pharmacist" && u.IsActive &&
            u.PharmacyId == PharmacyId && u.BranchId == BranchId &&
            u.Branch != null && u.Branch.PharmacyId == PharmacyId && u.Branch.IsActive,
            ct);
        if (!assigned)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "No active pharmacist assignment for this pharmacy branch." });

        if (medicineId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid medicine id is required."
            });
        }

        var inventoryExists =
            await _db.Inventories
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId &&
                    x.MedicineId ==
                    medicineId,
                    ct);

        if (!inventoryExists)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Medicine inventory was not found."
            });
        }

        var today =
            DateTime.UtcNow.Date;

        var items =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId ==
                    PharmacyId &&
                    x.Inventory.BranchId ==
                    BranchId &&
                    x.Inventory.MedicineId ==
                    medicineId)
                .OrderBy(x =>
                    x.ExpiryDate)
                .Select(x => new
                {
                    x.Id,

                    x.BatchNumber,

                    x.ManufactureDate,

                    x.ExpiryDate,

                    x.QuantityReceived,

                    x.RemainingQuantity,

                    x.PurchasePrice,

                    x.SellingPrice,

                    x.GST,

                    x.IsQuarantined,

                    Status =
                        x.IsQuarantined
                            ? "Quarantined"
                            : x.ExpiryDate <
                              today
                                ? "Expired"
                                : x.RemainingQuantity <= 0
                                    ? "OutOfStock"
                                    : "Available"
                })
                .ToListAsync(ct);

        return Ok(new
        {
            Success = true,

            MedicineId =
                medicineId,

            Total =
                items.Count,

            Items =
                items
        });
    }

    // =====================================================
    // INVENTORY ALERTS
    //
    // GET /api/pharmacist/inventory/alerts
    //
    // Inventory -> View
    // =====================================================

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(
        [FromQuery] int expiryDays = 30,
        CancellationToken ct = default)
    {
        var permissionError =
            await CheckInventoryPermission(
                "View",
                ct);

        if (permissionError != null)
            return permissionError;

        // Verify the current assignment against the database (not only JWT claims).
        var assigned = await _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == UserId && u.Role == "Pharmacist" && u.IsActive &&
            u.PharmacyId == PharmacyId && u.BranchId == BranchId &&
            u.Branch != null && u.Branch.PharmacyId == PharmacyId && u.Branch.IsActive,
            ct);
        if (!assigned)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "No active pharmacist assignment for this pharmacy branch." });

        expiryDays =
            Math.Clamp(
                expiryDays,
                1,
                365);

        var today =
            DateTime.UtcNow.Date;

        var expiryLimit =
            today.AddDays(
                expiryDays);

        // =================================================
        // LOW / OUT OF STOCK
        // =================================================

        var lowStock =
            await _db.Inventories
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId &&
                    x.CurrentStock <=
                    x.ReorderLevel)
                .OrderBy(x =>
                    x.CurrentStock)
                .Select(x => new
                {
                    x.MedicineId,

                    Medicine =
                        x.Medicine.Name,

                    x.Medicine.MedicineCode,

                    x.CurrentStock,

                    x.AvailableStock,

                    x.ReorderLevel,

                    Status =
                        x.CurrentStock <= 0
                            ? "OutOfStock"
                            : "LowStock"
                })
                .ToListAsync(ct);

        // =================================================
        // NEAR EXPIRY / EXPIRED
        // =================================================

        var expiry =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId ==
                    PharmacyId &&
                    x.Inventory.BranchId ==
                    BranchId &&
                    x.RemainingQuantity > 0 &&
                    x.ExpiryDate <=
                    expiryLimit)
                .OrderBy(x =>
                    x.ExpiryDate)
                .Select(x => new
                {
                    x.Inventory.MedicineId,

                    Medicine =
                        x.Inventory.Medicine.Name,

                    x.Inventory
                        .Medicine
                        .MedicineCode,

                    x.Id,

                    x.BatchNumber,

                    x.ExpiryDate,

                    x.RemainingQuantity,

                    x.IsQuarantined,

                    Status =
                        x.ExpiryDate <
                        today
                            ? "Expired"
                            : "NearExpiry"
                })
                .ToListAsync(ct);

        return Ok(new
        {
            Success = true,

            ExpiryDays =
                expiryDays,

            Counts = new
            {
                LowStock =
                    lowStock.Count(x =>
                        x.Status ==
                        "LowStock"),

                OutOfStock =
                    lowStock.Count(x =>
                        x.Status ==
                        "OutOfStock"),

                NearExpiry =
                    expiry.Count(x =>
                        x.Status ==
                        "NearExpiry"),

                Expired =
                    expiry.Count(x =>
                        x.Status ==
                        "Expired")
            },

            LowStock =
                lowStock,

            Expiry =
                expiry
        });
    }
}