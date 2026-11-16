using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class InventoryOperationsController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public InventoryOperationsController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    private async Task<IActionResult?> CheckStockPermission(string action)
    {
        if (await _permissionChecker.HasPermissionAsync(UserId, "Stock", action))
            return null;

        return StatusCode(StatusCodes.Status403Forbidden,
            new { Success = false, Message = $"You do not have {action} permission for Stock." });
    }

    [HttpGet("adjustments")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> AdjustmentHistory([FromQuery] int? medicineId = null)
    {
        var error = await CheckStockPermission("View");
        if (error != null) return error;
        var query = _db.StockAdjustments.AsNoTracking().Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (medicineId.HasValue) query = query.Where(x => x.MedicineId == medicineId.Value);
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync());
    }

    [HttpGet("adjustments/{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> GetAdjustment(int id)
    {
        var error = await CheckStockPermission("View");
        if (error != null) return error;
        var adjustment = await _db.StockAdjustments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        return adjustment == null ? NotFound() : Ok(adjustment);
    }

    public sealed class UpdateAdjustmentReasonRequest { public string Reason { get; set; } = string.Empty; }

    // Only the reason is editable: changing quantities must create a new audited adjustment.
    [HttpPut("adjustments/{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> UpdateAdjustmentReason(int id, [FromBody] UpdateAdjustmentReasonRequest dto)
    {
        var error = await CheckStockPermission("Edit");
        if (error != null) return error;
        if (string.IsNullOrWhiteSpace(dto.Reason)) return BadRequest(new { Success = false, Message = "Reason is required." });
        var adjustment = await _db.StockAdjustments.FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (adjustment == null) return NotFound();
        adjustment.Reason = dto.Reason.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { Success = true, Message = "Adjustment reason updated; stock unchanged." });
    }

    [HttpDelete("adjustments/{id:int}")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> DeleteAdjustment(int id)
    {
        var error = await CheckStockPermission("Delete");
        if (error != null) return error;
        var adjustment = await _db.StockAdjustments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (adjustment == null) return NotFound();
        return Conflict(new { Success = false, Message = "Applied adjustments cannot be deleted without reversing inventory and transaction history. Create a compensating adjustment instead." });
    }

    // =====================================================
    // COMPLETE MEDICINE + INVENTORY + BATCH + SUPPLIER VIEW
    // GET /api/inventory/stock-details
    // =====================================================

    [HttpGet("stock-details")]
    public async Task<IActionResult> GetStockDetails(
        [FromQuery] string? search = null,
        [FromQuery] string? stockStatus = null,
        [FromQuery] string? expiryStatus = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25)
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var today = DateTime.UtcNow.Date;
        var nearExpiryDate = today.AddDays(90);

        var query = _db.Inventories
            .AsNoTracking()
            .Where(x =>
                x.PharmacyId == PharmacyId &&
                x.BranchId == BranchId &&
                x.Medicine.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x =>
                x.Medicine.Name.Contains(keyword) ||
                x.Medicine.MedicineCode.Contains(keyword) ||
                x.Medicine.GenericName.Contains(keyword) ||
                x.Medicine.BrandName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(stockStatus))
        {
            switch (stockStatus.Trim().ToLowerInvariant())
            {
                case "outofstock":
                case "out of stock":
                    query = query.Where(x => x.AvailableStock <= 0);
                    break;
                case "lowstock":
                case "low stock":
                    query = query.Where(x =>
                        x.AvailableStock > 0 &&
                        x.AvailableStock <= x.ReorderLevel);
                    break;
                case "instock":
                case "in stock":
                    query = query.Where(x =>
                        x.AvailableStock > x.ReorderLevel);
                    break;
                default:
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "stockStatus must be InStock, LowStock or OutOfStock."
                    });
            }
        }

        if (!string.IsNullOrWhiteSpace(expiryStatus))
        {
            switch (expiryStatus.Trim().ToLowerInvariant())
            {
                case "expired":
                    query = query.Where(x => _db.MedicineBatches.Any(batch =>
                        batch.InventoryId == x.Id &&
                        batch.RemainingQuantity > 0 &&
                        batch.ExpiryDate < today));
                    break;
                case "nearexpiry":
                case "near expiry":
                    query = query.Where(x => _db.MedicineBatches.Any(batch =>
                        batch.InventoryId == x.Id &&
                        batch.RemainingQuantity > 0 &&
                        batch.ExpiryDate >= today &&
                        batch.ExpiryDate <= nearExpiryDate));
                    break;
                case "valid":
                    query = query.Where(x => _db.MedicineBatches.Any(batch =>
                        batch.InventoryId == x.Id &&
                        batch.RemainingQuantity > 0 &&
                        batch.ExpiryDate > nearExpiryDate));
                    break;
                default:
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "expiryStatus must be Valid, NearExpiry or Expired."
                    });
            }
        }

        var totalRecords = await query.CountAsync();

        var records = await query
            .OrderBy(x => x.Medicine.Name)
            .ThenBy(x => x.MedicineId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MedicineInventoryDetailsDto
            {
                MedicineId = x.MedicineId,
                MedicineCode = x.Medicine.MedicineCode,
                MedicineName = x.Medicine.Name,
                GenericName = x.Medicine.GenericName,
                BrandName = x.Medicine.BrandName,
                Category = x.Medicine.Category,
                DosageForm = x.Medicine.DosageForm,
                Strength = x.Medicine.Strength,
                Manufacturer = x.Medicine.Manufacturer,
                PackSize = x.Medicine.PackSize,
                Unit = x.Medicine.Unit,
                PrescriptionRequired = x.Medicine.PrescriptionRequired,
                InventoryId = x.Id,
                CurrentStock = x.CurrentStock,
                AvailableStock = x.AvailableStock,
                ReservedStock = x.ReservedStock,
                MinimumStock = x.MinimumStock,
                MaximumStock = x.MaximumStock,
                ReorderLevel = x.ReorderLevel,
                StorageLocation = x.StorageLocation,
                StockStatus = x.AvailableStock <= 0
                    ? "OutOfStock"
                    : x.AvailableStock <= x.ReorderLevel
                        ? "LowStock"
                        : "InStock",
                Batches = _db.MedicineBatches
                    .Where(batch => batch.InventoryId == x.Id)
                    .OrderBy(batch => batch.ExpiryDate)
                    .ThenBy(batch => batch.Id)
                    .Select(batch => new MedicineBatchDetailsDto
                    {
                        BatchId = batch.Id,
                        BatchNumber = batch.BatchNumber,
                        SupplierId = batch.SupplierId,
                        SupplierCode = batch.SupplierId.HasValue
                            ? _db.Suppliers
                                .Where(supplier =>
                                    supplier.Id == batch.SupplierId.Value &&
                                    supplier.PharmacyId == PharmacyId &&
                                    supplier.BranchId == BranchId)
                                .Select(supplier => supplier.Code)
                                .FirstOrDefault() ?? string.Empty
                            : string.Empty,
                        SupplierName = batch.SupplierId.HasValue
                            ? _db.Suppliers
                                .Where(supplier =>
                                    supplier.Id == batch.SupplierId.Value &&
                                    supplier.PharmacyId == PharmacyId &&
                                    supplier.BranchId == BranchId)
                                .Select(supplier => supplier.Name)
                                .FirstOrDefault() ?? string.Empty
                            : string.Empty,
                        ManufactureDate = batch.ManufactureDate,
                        PurchaseDate = batch.PurchaseDate,
                        ExpiryDate = batch.ExpiryDate,
                        PurchasePrice = batch.PurchasePrice,
                        SellingPrice = batch.SellingPrice,
                        GST = batch.GST,
                        QuantityReceived = batch.QuantityReceived,
                        AvailablePieces = batch.RemainingQuantity,
                        IsQuarantined = batch.IsQuarantined,
                        ExpiryStatus = batch.ExpiryDate < today
                            ? "Expired"
                            : batch.ExpiryDate <= nearExpiryDate
                                ? "NearExpiry"
                                : "Valid"
                    })
                    .ToList()
            })
            .ToListAsync();

        var summary = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalMedicines = group.Count(),
                TotalStock = group.Sum(x => x.CurrentStock),
                AvailableStock = group.Sum(x => x.AvailableStock),
                LowStock = group.Count(x =>
                    x.AvailableStock > 0 &&
                    x.AvailableStock <= x.ReorderLevel),
                OutOfStock = group.Count(x => x.AvailableStock <= 0)
            })
            .FirstOrDefaultAsync();

        var batchSummary = await _db.MedicineBatches
            .AsNoTracking()
            .Where(batch =>
                batch.Inventory.PharmacyId == PharmacyId &&
                batch.Inventory.BranchId == BranchId &&
                batch.Inventory.Medicine.IsActive &&
                batch.RemainingQuantity > 0)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                NearExpiryBatches = group.Count(batch =>
                    batch.ExpiryDate >= today &&
                    batch.ExpiryDate <= nearExpiryDate),
                ExpiredBatches = group.Count(batch =>
                    batch.ExpiryDate < today)
            })
            .FirstOrDefaultAsync();

        return Ok(new
        {
            Success = true,
            Summary = new
            {
                TotalMedicines = summary?.TotalMedicines ?? 0,
                TotalStock = summary?.TotalStock ?? 0,
                AvailableStock = summary?.AvailableStock ?? 0,
                LowStock = summary?.LowStock ?? 0,
                OutOfStock = summary?.OutOfStock ?? 0,
                NearExpiryBatches = batchSummary?.NearExpiryBatches ?? 0,
                ExpiredBatches = batchSummary?.ExpiredBatches ?? 0
            },
            Pagination = new
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize)
            },
            Data = records
        });
    }

    // =====================================================
    // GET MEDICINE STOCK
    // GET /api/inventory/{medicineId}
    // =====================================================

    [HttpGet("{medicineId:int}")]
    public async Task<IActionResult> GetMedicineStock(
        int medicineId)
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var inv =
            await _db.Inventories
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId &&
                    x.MedicineId == medicineId)
                .Select(x => new
                {
                    x.Id,
                    x.MedicineId,
                    x.Medicine.Name,
                    x.CurrentStock,
                    x.ReservedStock,
                    x.AvailableStock,
                    x.MinimumStock,
                    x.MaximumStock,
                    x.ReorderLevel,
                    x.StorageLocation,
                    x.LastUpdated
                })
                .FirstOrDefaultAsync();

        return inv is null
            ? NotFound()
            : Ok(inv);
    }

    // =====================================================
    // GET MEDICINE BATCHES
    // GET /api/inventory/{medicineId}/batches
    // =====================================================

    [HttpGet("{medicineId:int}/batches")]
    public async Task<IActionResult> Batches(
        int medicineId)
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var items =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId == PharmacyId &&
                    x.Inventory.BranchId == BranchId &&
                    x.Inventory.MedicineId == medicineId)
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
                    x.SupplierId,
                    x.IsQuarantined
                })
                .ToListAsync();

        return Ok(items);
    }

    // =====================================================
    // GET BATCH DETAILS
    // GET /api/inventory/batches/{batchId}
    // =====================================================

    [HttpGet("batches/{batchId:int}")]
    public async Task<IActionResult> Batch(
        int batchId)
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var batch =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Id == batchId &&
                    x.Inventory.PharmacyId == PharmacyId &&
                    x.Inventory.BranchId == BranchId)
                .Select(x => new
                {
                    x.Id,
                    x.Inventory.MedicineId,
                    x.Inventory.Medicine.Name,
                    x.BatchNumber,
                    x.ManufactureDate,
                    x.ExpiryDate,
                    x.QuantityReceived,
                    x.RemainingQuantity,
                    x.PurchasePrice,
                    x.SellingPrice,
                    x.GST,
                    x.SupplierId,
                    x.IsQuarantined
                })
                .FirstOrDefaultAsync();

        return batch is null
            ? NotFound()
            : Ok(batch);
    }

    // =====================================================
    // EXPIRED BATCHES
    // GET /api/inventory/expired
    // =====================================================

    [HttpGet("expired")]
    public async Task<IActionResult> Expired()
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var items =
            await ExpiryQuery(
                    DateTime.UtcNow.Date)
                .ToListAsync();

        return Ok(items);
    }

    // =====================================================
    // NEAR EXPIRY DETAILS
    // GET /api/inventory/near-expiry-details
    // =====================================================

    [HttpGet("near-expiry-details")]
    public async Task<IActionResult> NearExpiry(
        [FromQuery] int days = 30)
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        days =
            Math.Clamp(
                days,
                1,
                365);

        var items =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId == PharmacyId &&
                    x.Inventory.BranchId == BranchId &&
                    x.RemainingQuantity > 0 &&
                    x.ExpiryDate >= DateTime.UtcNow.Date &&
                    x.ExpiryDate <=
                        DateTime.UtcNow.Date.AddDays(days))
                .OrderBy(x =>
                    x.ExpiryDate)
                .Select(
                    ExpiryProjection())
                .ToListAsync();

        return Ok(items);
    }

    // =====================================================
    // STOCK TRANSACTIONS
    // GET /api/inventory/transactions
    // =====================================================

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions(
        [FromQuery] int? medicineId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var query =
            _db.StockTransactions
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (medicineId.HasValue)
        {
            query =
                query.Where(x =>
                    x.MedicineId ==
                    medicineId.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var transactionType =
                type.Trim();

            query =
                query.Where(x =>
                    x.TransactionType ==
                    transactionType);
        }

        if (from.HasValue)
        {
            query =
                query.Where(x =>
                    x.TransactionDate >=
                    from.Value);
        }

        if (to.HasValue)
        {
            var endDate =
                to.Value.Date.AddDays(1);

            query =
                query.Where(x =>
                    x.TransactionDate <
                    endDate);
        }

        var items =
            await query
                .OrderByDescending(x =>
                    x.TransactionDate)
                .Take(1000)
                .Select(x => new
                {
                    x.Id,

                    x.MedicineId,

                    MedicineName =
                        _db.Medicines
                            .Where(m =>
                                m.Id ==
                                x.MedicineId)
                            .Select(m =>
                                m.Name)
                            .FirstOrDefault(),

                    x.MedicineBatchId,

                    x.TransactionType,

                    x.Quantity,

                    x.BalanceStock,

                    x.Remarks,

                    x.ReferenceType,

                    x.ReferenceId,

                    x.CreatedByUserId,

                    CreatedByName =
                        _db.Users
                            .Where(u =>
                                u.Id ==
                                x.CreatedByUserId)
                            .Select(u =>
                                u.Name)
                            .FirstOrDefault(),

                    CreatedByRole =
                        _db.Users
                            .Where(u =>
                                u.Id ==
                                x.CreatedByUserId)
                            .Select(u =>
                                u.Role)
                            .FirstOrDefault(),

                    x.TransactionDate
                })
                .ToListAsync();

        return Ok(items);
    }

    // =====================================================
    // STOCK ADJUSTMENT
    // POST /api/inventory/adjustments
    //
    // NOTE:
    // This route is different from:
    // POST /api/inventory/adjust
    //
    // so it does not cause a Swagger route conflict.
    // =====================================================

    [HttpPost("adjustments")]
    public async Task<IActionResult> Adjust(
        [FromBody] StockAdjustmentDto dto)
    {
        var permissionError = await CheckStockPermission("Edit");
        if (permissionError != null) return permissionError;

        if (dto.Quantity <= 0)
        {
            return BadRequest(
                "Quantity must be positive.");
        }

        if (dto.Type != "Increase" &&
            dto.Type != "Decrease")
        {
            return BadRequest(
                "Type must be Increase or Decrease and quantity must be positive.");
        }

        if (!dto.BatchId.HasValue)
        {
            return BadRequest(
                "BatchId is required to keep batch and aggregate stock synchronized.");
        }

        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();

        try
        {
            var inventory =
                await _db.Inventories
                    .FirstOrDefaultAsync(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId &&
                        x.MedicineId ==
                        dto.MedicineId);

            if (inventory is null)
            {
                return NotFound();
            }

            var delta =
                dto.Type == "Increase"
                    ? dto.Quantity
                    : -dto.Quantity;

            if (inventory.CurrentStock +
                delta < 0)
            {
                return BadRequest(
                    "Insufficient stock.");
            }

            var batch =
                await _db.MedicineBatches
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        dto.BatchId.Value &&
                        x.InventoryId ==
                        inventory.Id);

            if (batch is null)
            {
                return BadRequest(
                    "Invalid batch quantity.");
            }

            if (batch.RemainingQuantity +
                delta < 0)
            {
                return BadRequest(
                    "Invalid batch quantity.");
            }

            batch.RemainingQuantity +=
                delta;

            inventory.CurrentStock +=
                delta;

            inventory.AvailableStock +=
                delta;

            inventory.LastUpdated =
                DateTime.UtcNow;

            var adjustment =
                new StockAdjustment
                {
                    PharmacyId =
                        PharmacyId,

                    BranchId =
                        BranchId,

                    MedicineId =
                        dto.MedicineId,

                    MedicineBatchId =
                        dto.BatchId,

                    AdjustmentType =
                        dto.Type,

                    Quantity =
                        dto.Quantity,

                    Reason =
                        dto.Reason,

                    CreatedByUserId =
                        UserId
                };

            _db.StockAdjustments.Add(
                adjustment);

            await _db.SaveChangesAsync();

            _db.StockTransactions.Add(
                new StockTransaction
                {
                    PharmacyId =
                        PharmacyId,

                    BranchId =
                        BranchId,

                    MedicineId =
                        dto.MedicineId,

                    MedicineBatchId =
                        dto.BatchId,

                    TransactionType =
                        "Adjustment" +
                        dto.Type,

                    Quantity =
                        dto.Quantity,

                    BalanceStock =
                        inventory.CurrentStock,

                    Remarks =
                        dto.Reason,

                    ReferenceType =
                        "StockAdjustment",

                    ReferenceId =
                        adjustment.Id,

                    CreatedByUserId =
                        UserId,

                    TransactionDate =
                        DateTime.UtcNow
                });

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                adjustment.Id,
                inventory.CurrentStock
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================================
    // QUARANTINE / RELEASE BATCH
    // PATCH /api/inventory/batches/{batchId}/quarantine
    // =====================================================

    [HttpPatch("batches/{batchId:int}/quarantine")]
    public async Task<IActionResult> Quarantine(
        int batchId,
        [FromBody] StatusDto dto)
    {
        var permissionError = await CheckStockPermission("Edit");
        if (permissionError != null) return permissionError;

        var batch =
            await _db.MedicineBatches
                .FirstOrDefaultAsync(x =>
                    x.Id == batchId &&
                    x.Inventory.PharmacyId ==
                    PharmacyId &&
                    x.Inventory.BranchId ==
                    BranchId);

        if (batch is null)
        {
            return NotFound();
        }

        batch.IsQuarantined =
            dto.IsActive;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    // =====================================================
    // INVENTORY SUMMARY
    // GET /api/inventory/summary
    // =====================================================

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var inventory =
            _db.Inventories
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        var totalMedicines =
            await inventory.CountAsync();

        var totalUnits =
            await inventory
                .Select(x =>
                    (int?)x.CurrentStock)
                .SumAsync()
            ?? 0;

        var lowStock =
            await inventory
                .CountAsync(x =>
                    x.CurrentStock <=
                    x.ReorderLevel);

        var outOfStock =
            await inventory
                .CountAsync(x =>
                    x.CurrentStock <= 0);

        return Ok(new
        {
            TotalMedicines =
                totalMedicines,

            TotalUnits =
                totalUnits,

            LowStock =
                lowStock,

            OutOfStock =
                outOfStock
        });
    }

    // =====================================================
    // INVENTORY VALUATION
    // GET /api/inventory/valuation
    // =====================================================

    [HttpGet("valuation")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> Valuation()
    {
        var permissionError = await CheckStockPermission("View");
        if (permissionError != null) return permissionError;

        var result =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId ==
                    PharmacyId &&
                    x.Inventory.BranchId ==
                    BranchId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    PurchaseValue =
                        g.Sum(x =>
                            x.RemainingQuantity *
                            x.PurchasePrice),

                    SellingValue =
                        g.Sum(x =>
                            x.RemainingQuantity *
                            x.SellingPrice)
                })
                .FirstOrDefaultAsync();

        return Ok(result);
    }

    // =====================================================
    // EXPIRY QUERY
    // =====================================================

    private IQueryable<object> ExpiryQuery(
        DateTime date)
    {
        return _db.MedicineBatches
            .AsNoTracking()
            .Where(x =>
                x.Inventory.PharmacyId ==
                PharmacyId &&
                x.Inventory.BranchId ==
                BranchId &&
                x.RemainingQuantity > 0 &&
                x.ExpiryDate <= date)
            .OrderBy(x =>
                x.ExpiryDate)
            .Select(
                ExpiryProjection());
    }

    // =====================================================
    // EXPIRY PROJECTION
    // =====================================================

    private static System.Linq.Expressions.Expression<
        Func<MedicineBatch, object>>
        ExpiryProjection()
    {
        return x => new
        {
            x.Id,

            x.Inventory.MedicineId,

            MedicineName =
                x.Inventory.Medicine.Name,

            x.BatchNumber,

            x.ExpiryDate,

            x.RemainingQuantity,

            Quantity = x.RemainingQuantity,

            DaysLeft = EF.Functions.DateDiffDay(DateTime.UtcNow.Date, x.ExpiryDate),

            Status = x.ExpiryDate <= DateTime.UtcNow.Date ? "Expired" : "NearExpiry",

            x.SupplierId,

            x.IsQuarantined
        };
    }
}
