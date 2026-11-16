using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "PharmacyAdmin")]
public class InventoryController : PharmacyApiControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly PharmacyPermissionChecker _permissionChecker;
    private readonly AppDbContext _db;

    public InventoryController(
        IInventoryService inventoryService,
        PharmacyPermissionChecker permissionChecker,
        AppDbContext db)
    {
        _inventoryService = inventoryService;
        _permissionChecker = permissionChecker;
        _db = db;
    }

    // =====================================================
    // CHECK INVENTORY PERMISSION
    // =====================================================

    private async Task<IActionResult?> CheckInventoryPermission(
        string action)
    {
        var allowed =
            await _permissionChecker.HasPermissionAsync(
                UserId,
                "Stock",
                action);

        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        $"You do not have {action} permission for Stock."
                });
        }

        return null;
    }

    [HttpGet("records/{id:int}")]
    public async Task<IActionResult> GetInventoryRecord(int id)
    {
        var error = await CheckInventoryPermission("View");
        if (error != null) return error;
        var record = await _db.Inventories.AsNoTracking().Where(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId)
            .Select(x => new { x.Id, x.MedicineId, x.BranchId, x.CurrentStock, x.ReservedStock, x.AvailableStock, x.MinimumStock, x.MaximumStock, x.ReorderLevel, x.StorageLocation }).FirstOrDefaultAsync();
        return record == null ? NotFound() : Ok(record);
    }

    [HttpDelete("records/{id:int}")]
    public async Task<IActionResult> DeleteEmptyInventory(int id)
    {
        var error = await CheckInventoryPermission("Delete");
        if (error != null) return error;
        var inventory = await _db.Inventories.FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (inventory == null) return NotFound();
        if (inventory.CurrentStock != 0 || inventory.AvailableStock != 0 || inventory.ReservedStock != 0 ||
            await _db.MedicineBatches.AnyAsync(x => x.InventoryId == id) ||
            await _db.StockTransactions.AnyAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.MedicineId == inventory.MedicineId) ||
            await _db.StockAdjustments.AnyAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.MedicineId == inventory.MedicineId))
            return Conflict(new { Success = false, Message = "Inventory has stock, batches or movement history; permanent deletion blocked." });
        _db.Inventories.Remove(inventory);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { Success = false, Message = "Inventory has dependent records." }); }
        return Ok(new { Success = true, Message = "Unused inventory permanently deleted." });
    }

    public sealed class CreateEmptyBatchRequest
    {
        public int InventoryId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public DateTime ManufactureDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal GST { get; set; }
    }

    // Create an empty batch; stock must enter through the existing audited stock-receipt workflow.
    [HttpPost("batches")]
    public async Task<IActionResult> CreateEmptyBatch([FromBody] CreateEmptyBatchRequest dto)
    {
        var error = await CheckInventoryPermission("Create");
        if (error != null) return error;
        if (string.IsNullOrWhiteSpace(dto.BatchNumber) || dto.ManufactureDate >= dto.ExpiryDate || dto.ExpiryDate <= DateTime.UtcNow ||
            dto.PurchasePrice < 0 || dto.SellingPrice < 0 || dto.GST < 0)
            return BadRequest(new { Success = false, Message = "Invalid batch number, dates or prices." });
        var inventory = await _db.Inventories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.InventoryId && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (inventory == null) return NotFound(new { Success = false, Message = "Inventory not found." });
        if (await _db.MedicineBatches.AnyAsync(x => x.InventoryId == dto.InventoryId && x.BatchNumber == dto.BatchNumber.Trim()))
            return Conflict(new { Success = false, Message = "Batch number already exists for this inventory." });
        var batch = new MedicineBatch { InventoryId = dto.InventoryId, BatchNumber = dto.BatchNumber.Trim(), ManufactureDate = dto.ManufactureDate,
            ExpiryDate = dto.ExpiryDate, PurchasePrice = dto.PurchasePrice, SellingPrice = dto.SellingPrice, GST = dto.GST,
            QuantityReceived = 0, RemainingQuantity = 0 };
        _db.MedicineBatches.Add(batch);
        await _db.SaveChangesAsync();
        return Ok(new { Success = true, batch.Id, Message = "Empty batch created. Use audited stock receipt to add quantity." });
    }

    [HttpDelete("batches/{batchId:int}")]
    public async Task<IActionResult> DeleteEmptyBatch(int batchId)
    {
        var error = await CheckInventoryPermission("Delete");
        if (error != null) return error;
        var batch = await _db.MedicineBatches.Include(x => x.Inventory).FirstOrDefaultAsync(x => x.Id == batchId && x.Inventory.PharmacyId == PharmacyId && x.Inventory.BranchId == BranchId);
        if (batch == null) return NotFound();
        if (batch.QuantityReceived != 0 || batch.RemainingQuantity != 0 || batch.PurchaseOrderId.HasValue || batch.GoodsReceiptId.HasValue ||
            await _db.StockTransactions.AnyAsync(x => x.MedicineBatchId == batchId) ||
            await _db.StockAdjustments.AnyAsync(x => x.MedicineBatchId == batchId) ||
            await _db.StockTransfers.AnyAsync(x => x.MedicineBatchId == batchId))
            return Conflict(new { Success = false, Message = "Batch has received quantity or movement history; permanent deletion blocked." });
        _db.MedicineBatches.Remove(batch);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { Success = false, Message = "Batch has dependent records." }); }
        return Ok(new { Success = true, Message = "Unused batch permanently deleted." });
    }

    // =====================================================
    // ADD STOCK
    // POST /api/inventory/add-stock
    //
    // Stock -> Create
    // =====================================================

    [HttpPost("add-stock")]
    public async Task<IActionResult> AddStock(
        [FromBody] AddStockDto dto)
    {
        var permissionError =
            await CheckInventoryPermission(
                "Create");

        if (permissionError != null)
            return permissionError;

        var result =
            await _inventoryService.AddStockAsync(
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

    // =====================================================
    // GET INVENTORY
    // GET /api/inventory
    //
    // Stock -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> GetInventory([FromQuery] int? branchId = null)
    {
        var permissionError =
            await CheckInventoryPermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var targetBranchId = BranchId;
        if (branchId.HasValue)
        {
            if (!User.IsInRole("PharmacyAdmin"))
                return StatusCode(403, new { Success = false, Message = "Only a Pharmacy Admin can select another branch." });
            var mapped = await _db.PharmacyBranchIdentities.AsNoTracking()
                .Where(i => i.PharmacyId == PharmacyId && i.BranchId == branchId.Value)
                .Select(i => (int?)i.ExistingBranchId).SingleOrDefaultAsync();
            if (!mapped.HasValue || !await _db.Branches.AnyAsync(b => b.Id == mapped.Value && b.PharmacyId == PharmacyId && b.IsActive))
                return BadRequest(new { Success = false, Message = "Invalid pharmacy branch." });
            targetBranchId = mapped.Value;
        }

        var result =
            await _inventoryService.GetInventoryAsync(
                PharmacyId,
                targetBranchId);

        return Ok(result);
    }

    // Opt-in paged inventory; legacy GET /api/inventory stays compatible.
    [HttpGet("paged")]
    public async Task<IActionResult> GetInventoryPaged(
        [FromQuery] int? branchId = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var permissionError = await CheckInventoryPermission("View");
        if (permissionError != null) return permissionError;
        if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "pageNumber must be >= 1 and pageSize must be 1..100." });
        var targetBranchId = BranchId;
        if (branchId.HasValue)
        {
            var mapping = await _db.PharmacyBranchIdentities.AsNoTracking()
                .Where(x => x.PharmacyId == PharmacyId && x.BranchId == branchId.Value)
                .Select(x => (int?)x.ExistingBranchId).FirstOrDefaultAsync();
            if (!mapping.HasValue) return BadRequest(new { message = "Invalid pharmacy branch." });
            if (!User.IsInRole("PharmacyAdmin") && mapping.Value != BranchId)
                return StatusCode(403, new { message = "You can view only your assigned branch." });
            targetBranchId = mapping.Value;
        }
        if (!await _db.Branches.AnyAsync(x => x.Id == targetBranchId && x.PharmacyId == PharmacyId && x.IsActive))
            return BadRequest(new { message = "Inactive or invalid branch." });
        var totalCount = await _db.Inventories.AsNoTracking().CountAsync(x =>
            x.PharmacyId == PharmacyId && x.BranchId == targetBranchId && x.Medicine.IsActive);
        var items = await _inventoryService.GetInventoryPageAsync(PharmacyId, targetBranchId, pageNumber, pageSize);
        return Ok(new { pharmacyId = PharmacyId, branchId, pageNumber, pageSize, totalCount, items });
    }

    // =====================================================
    // LOW STOCK
    // GET /api/inventory/low-stock
    //
    // Stock -> View
    // =====================================================

    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock()
    {
        var permissionError =
            await CheckInventoryPermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var result =
            await _inventoryService.GetLowStockAsync(
                PharmacyId,
                BranchId);

        return Ok(result);
    }

    // =====================================================
    // OUT OF STOCK
    // GET /api/inventory/out-of-stock
    //
    // Stock -> View
    // =====================================================

    [HttpGet("out-of-stock")]
    public async Task<IActionResult> GetOutOfStock()
    {
        var permissionError =
            await CheckInventoryPermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var result =
            await _inventoryService.GetOutOfStockAsync(
                PharmacyId,
                BranchId);

        return Ok(result);
    }

    // =====================================================
    // NEAR EXPIRY
    // GET /api/inventory/near-expiry
    //
    // Stock -> View
    // =====================================================

    [HttpGet("near-expiry")]
    public async Task<IActionResult> GetNearExpiry()
    {
        var permissionError =
            await CheckInventoryPermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var result =
            await _inventoryService.GetNearExpiryAsync(
                PharmacyId,
                BranchId);

        return Ok(result);
    }

    // =====================================================
    // UPDATE INVENTORY LEVELS
    //
    // PUT /api/inventory/{medicineId}/levels
    //
    // Stock -> Edit
    //
    // Updates:
    // MinimumStock
    // MaximumStock
    // ReorderLevel
    // StorageLocation
    // =====================================================

    [HttpPut("{medicineId:int}/levels")]
    public async Task<IActionResult> UpdateLevels(
        int medicineId,
        [FromBody] InventoryLevelsDto dto)
    {
        var permissionError =
            await CheckInventoryPermission(
                "Edit");

        if (permissionError != null)
            return permissionError;

        if (medicineId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid medicine id is required."
            });
        }

        if (dto.MinimumStock < 0 ||
            dto.MaximumStock < 0 ||
            dto.ReorderLevel < 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Inventory levels cannot be negative."
            });
        }

        if (dto.MaximumStock > 0 &&
            dto.MinimumStock >
            dto.MaximumStock)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Minimum stock cannot be greater than maximum stock."
            });
        }

        if (dto.MaximumStock > 0 &&
            dto.ReorderLevel >
            dto.MaximumStock)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Reorder level cannot be greater than maximum stock."
            });
        }

        var inventory =
            await _db.Inventories
                .FirstOrDefaultAsync(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId &&
                    x.MedicineId == medicineId);

        if (inventory == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Inventory record was not found."
            });
        }

        inventory.MinimumStock =
            dto.MinimumStock;

        inventory.MaximumStock =
            dto.MaximumStock;

        inventory.ReorderLevel =
            dto.ReorderLevel;

        inventory.StorageLocation =
            dto.StorageLocation?.Trim();

        inventory.LastUpdated =
            DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "Inventory levels updated successfully.",

            Inventory = new
            {
                inventory.MedicineId,
                inventory.MinimumStock,
                inventory.MaximumStock,
                inventory.ReorderLevel,
                inventory.StorageLocation,
                inventory.LastUpdated
            }
        });
    }

    // =====================================================
    // STOCK ADJUSTMENT
    //
    // POST /api/inventory/adjust
    //
    // Stock -> Edit
    //
    // Supported Type:
    // Increase
    // Decrease
    // Add
    // Remove
    // =====================================================

    [HttpPost("adjust")]
    public async Task<IActionResult> AdjustStock(
        [FromBody] StockAdjustmentDto dto)
    {
        var permissionError =
            await CheckInventoryPermission(
                "Edit");

        if (permissionError != null)
            return permissionError;

        if (dto.MedicineId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid MedicineId is required."
            });
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Adjustment quantity must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Type))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Adjustment type is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Reason))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Adjustment reason is required."
            });
        }

        var adjustmentType =
            dto.Type.Trim()
                .ToLowerInvariant();

        var increase =
            adjustmentType == "increase" ||
            adjustmentType == "add";

        var decrease =
            adjustmentType == "decrease" ||
            adjustmentType == "remove";

        if (!increase &&
            !decrease)
        {
            return BadRequest(new
            {
                Success = false,

                Message =
                    "Type must be Increase or Decrease."
            });
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

            if (inventory == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message =
                        "Inventory record was not found."
                });
            }

            MedicineBatch? batch = null;

            if (dto.BatchId.HasValue)
            {
                batch =
                    await _db.MedicineBatches
                        .FirstOrDefaultAsync(x =>
                            x.Id ==
                            dto.BatchId.Value &&
                            x.InventoryId ==
                            inventory.Id);

                if (batch == null)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Medicine batch was not found."
                    });
                }
            }

            if (decrease)
            {
                if (inventory.CurrentStock <
                    dto.Quantity ||
                    inventory.AvailableStock <
                    dto.Quantity)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Insufficient inventory stock."
                    });
                }

                if (batch != null &&
                    batch.RemainingQuantity <
                    dto.Quantity)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Insufficient batch stock."
                    });
                }

                inventory.CurrentStock -=
                    dto.Quantity;

                inventory.AvailableStock =
                    Math.Max(
                        0,
                        inventory.CurrentStock -
                        inventory.ReservedStock);

                if (batch != null)
                {
                    batch.RemainingQuantity -=
                        dto.Quantity;
                }
            }
            else
            {
                inventory.CurrentStock +=
                    dto.Quantity;

                inventory.AvailableStock =
                    Math.Max(
                        0,
                        inventory.CurrentStock -
                        inventory.ReservedStock);

                if (batch != null)
                {
                    batch.RemainingQuantity +=
                        dto.Quantity;
                }
            }

            inventory.LastUpdated =
                DateTime.UtcNow;

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
                        increase
                            ? "StockAdjustmentIncrease"
                            : "StockAdjustmentDecrease",

                    Quantity =
                        dto.Quantity,

                    BalanceStock =
                        inventory.CurrentStock,

                    ReferenceType =
                        "StockAdjustment",

                    ReferenceId =
                        inventory.Id,

                    CreatedByUserId =
                        UserId,

                    Remarks =
                        dto.Reason.Trim(),

                    TransactionDate =
                        DateTime.UtcNow
                });

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                Success = true,

                Message =
                    "Stock adjusted successfully.",

                Inventory = new
                {
                    inventory.MedicineId,

                    inventory.CurrentStock,

                    inventory.AvailableStock,

                    inventory.ReservedStock,

                    inventory.LastUpdated
                }
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================================
    // UPDATE EXISTING BATCH DETAILS
    //
    // PUT /api/inventory/batches/{batchId}
    // Stock -> Edit
    //
    // Quantity is NEVER changed here.
    // Use add-stock / adjust / dispose for quantity changes.
    // =====================================================

    [HttpPut("batches/{batchId:int}")]
    public async Task<IActionResult> UpdateBatch(
        int batchId,
        [FromBody] UpdateMedicineBatchDto dto)
    {
        var permissionError =
            await CheckInventoryPermission("Edit");

        if (permissionError != null)
            return permissionError;

        if (batchId <= 0)
            return BadRequest(new { Success = false, Message = "A valid batch id is required." });

        if (dto.PurchasePrice < 0 || dto.SellingPrice < 0)
            return BadRequest(new { Success = false, Message = "Purchase price and selling price cannot be negative." });

        if (dto.GST < 0 || dto.GST > 100)
            return BadRequest(new { Success = false, Message = "GST must be between 0 and 100." });

        if (dto.ExpiryDate.Date <= DateTime.UtcNow.Date)
            return BadRequest(new { Success = false, Message = "Expiry date must be a future date." });

        var batch = await _db.MedicineBatches
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x =>
                x.Id == batchId &&
                x.Inventory.PharmacyId == PharmacyId &&
                x.Inventory.BranchId == BranchId);

        if (batch == null)
            return NotFound(new { Success = false, Message = "Medicine batch was not found." });

        batch.PurchasePrice = dto.PurchasePrice;
        batch.SellingPrice = dto.SellingPrice;
        batch.GST = dto.GST;
        batch.ExpiryDate = dto.ExpiryDate;

        if (dto.ManufactureDate.HasValue)
            batch.ManufactureDate = dto.ManufactureDate.Value;

        if (dto.PurchaseDate.HasValue)
            batch.PurchaseDate = dto.PurchaseDate;

        if (dto.SupplierId.HasValue)
        {
            var supplierExists = await _db.Suppliers.AnyAsync(x =>
                x.Id == dto.SupplierId.Value &&
                x.PharmacyId == PharmacyId &&
                x.BranchId == BranchId &&
                x.IsActive);

            if (!supplierExists)
                return BadRequest(new { Success = false, Message = "Supplier was not found for this pharmacy and branch." });

            batch.SupplierId = dto.SupplierId;
        }

        if (dto.Notes != null)
            batch.Notes = dto.Notes.Trim();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Medicine batch updated successfully.",
            Batch = new
            {
                batch.Id,
                batch.BatchNumber,
                batch.QuantityReceived,
                batch.RemainingQuantity,
                batch.PurchasePrice,
                batch.SellingPrice,
                batch.GST,
                batch.ManufactureDate,
                batch.ExpiryDate,
                batch.PurchaseDate,
                batch.SupplierId,
                batch.Notes
            }
        });
    }

    // =====================================================
    // DISPOSE / WRITE-OFF BATCH
    //
    // POST /api/inventory/batches/{batchId}/dispose
    //
    // Stock -> Delete
    //
    // This does NOT delete history.
    // Stock is reduced and a transaction is recorded.
    // =====================================================

    [HttpPost("batches/{batchId:int}/dispose")]
    public async Task<IActionResult> DisposeBatch(
        int batchId,
        [FromBody] DisposeBatchDto dto)
    {
        var permissionError =
            await CheckInventoryPermission(
                "Delete");

        if (permissionError != null)
            return permissionError;

        if (batchId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid batch id is required."
            });
        }

        if (dto.Quantity <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Disposal quantity must be greater than zero."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Reason))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Disposal reason is required."
            });
        }

        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();

        try
        {
            var batch =
                await _db.MedicineBatches
                    .Include(x =>
                        x.Inventory)
                    .FirstOrDefaultAsync(x =>
                        x.Id == batchId &&
                        x.Inventory.PharmacyId ==
                        PharmacyId &&
                        x.Inventory.BranchId ==
                        BranchId);

            if (batch == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message =
                        "Medicine batch was not found."
                });
            }

            if (batch.RemainingQuantity <
                dto.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Disposal quantity exceeds remaining batch stock."
                });
            }

            var inventory =
                batch.Inventory;

            if (inventory.CurrentStock <
                dto.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Inventory stock is lower than the requested disposal quantity."
                });
            }

            if (inventory.AvailableStock <
                dto.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Cannot dispose reserved stock. Available stock is lower than the requested disposal quantity."
                });
            }

            batch.RemainingQuantity -=
                dto.Quantity;

            inventory.CurrentStock -=
                dto.Quantity;

            // Available stock is always derived from physical stock
            // minus reserved stock.
            inventory.AvailableStock =
                Math.Max(
                    0,
                    inventory.CurrentStock -
                    inventory.ReservedStock);

            inventory.LastUpdated =
                DateTime.UtcNow;

            _db.StockTransactions.Add(
                new StockTransaction
                {
                    PharmacyId =
                        PharmacyId,

                    BranchId =
                        BranchId,

                    MedicineId =
                        inventory.MedicineId,

                    MedicineBatchId =
                        batch.Id,

                    TransactionType =
                        "Dispose",

                    Quantity =
                        dto.Quantity,

                    BalanceStock =
                        inventory.CurrentStock,

                    ReferenceType =
                        "MedicineBatch",

                    ReferenceId =
                        batch.Id,

                    CreatedByUserId =
                        UserId,

                    Remarks =
                        dto.Reason.Trim(),

                    TransactionDate =
                        DateTime.UtcNow
                });

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                Success = true,

                Message =
                    "Batch stock disposed successfully.",

                Batch = new
                {
                    batch.Id,
                    batch.BatchNumber,
                    batch.RemainingQuantity
                },

                Inventory = new
                {
                    inventory.MedicineId,
                    inventory.CurrentStock,
                    inventory.AvailableStock
                }
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

