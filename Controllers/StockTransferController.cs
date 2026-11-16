using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/inventory/transfers")]
[Authorize(Roles = "PharmacyAdmin")]
public class StockTransferController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public StockTransferController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // CHECK STOCK TRANSFERS PERMISSION
    // =====================================================

    private async Task<IActionResult?> CheckTransferPermission(
        string action)
    {
        var allowed =
            await _permissionChecker.HasPermissionAsync(
                UserId,
                "Stock Transfers",
                action);

        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        $"You do not have {action} permission for Stock Transfers."
                });
        }

        return null;
    }

    // Frontend uses pharmacy-scoped public branch IDs. Database foreign keys remain internal.
    // Retain the legacy location-name request fields for existing clients.
    private async Task<Branch?> ResolveTransferBranchAsync(int? publicBranchId, string? location)
    {
        if (publicBranchId.HasValue)
        {
            if (publicBranchId.Value <= 0) return null;

            var internalId = await _db.PharmacyBranchIdentities.AsNoTracking()
                .Where(x => x.PharmacyId == PharmacyId && x.BranchId == publicBranchId.Value)
                .Select(x => (int?)x.ExistingBranchId)
                .SingleOrDefaultAsync();
            if (!internalId.HasValue) return null;

            return await _db.Branches.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == internalId.Value &&
                    x.PharmacyId == PharmacyId && x.IsActive);
        }

        if (string.IsNullOrWhiteSpace(location)) return null;
        var requestedName = location.Trim();
        var branches = await _db.Branches.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.IsActive)
            .ToListAsync();

        var exact = branches.Where(x => string.Equals(x.Name.Trim(), requestedName,
            StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1) return exact[0];
        if (exact.Count > 1) return null;

        // Accept a UI label with/without the standard suffix, but only if UNIQUE.
        // Never use fuzzy/partial names, which could silently target the wrong branch.
        var normalized = NormalizeTransferBranchName(requestedName);
        var normalizedMatches = branches.Where(x => string.Equals(
            NormalizeTransferBranchName(x.Name), normalized,
            StringComparison.OrdinalIgnoreCase)).ToList();
        return normalizedMatches.Count == 1 ? normalizedMatches[0] : null;
    }

    private static string NormalizeTransferBranchName(string value)
    {
        var trimmed = value.Trim();
        foreach (var suffix in new[] { " - Main Branch", " - Sub Branch" })
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return trimmed[..^suffix.Length].Trim();
        return trimmed;
    }

    private async Task<Dictionary<int, int>> GetPublicBranchIdsAsync() =>
        await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId)
            .ToDictionaryAsync(x => x.ExistingBranchId, x => x.BranchId);

    private async Task<bool> TransferBranchesBelongToPharmacyAsync(StockTransfer transfer) =>
        await _db.Branches.AsNoTracking().CountAsync(x =>
            x.PharmacyId == PharmacyId &&
            (x.Id == transfer.FromBranchId || x.Id == transfer.ToBranchId)) == 2;

    public sealed class UpdateTransferDraftRequest
    {
        public int Quantity { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateDraft(int id, [FromBody] UpdateTransferDraftRequest request)
    {
        var permissionError = await CheckTransferPermission("Edit");
        if (permissionError != null) return permissionError;
        if (request.Quantity <= 0) return BadRequest(new { Success = false, Message = "Quantity must be positive." });
        var transfer = await _db.StockTransfers.FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId);
        if (transfer == null) return NotFound();
        if (!await TransferBranchesBelongToPharmacyAsync(transfer))
            return Conflict(new { Success = false, Message = "Transfer references an invalid pharmacy branch." });
        if (transfer.Status != "Requested") return Conflict(new { Success = false, Message = "Only requested transfers can be edited." });
        transfer.Quantity = request.Quantity;
        transfer.Notes = request.Notes?.Trim() ?? string.Empty;
        await _db.SaveChangesAsync();
        return Ok(new { Success = true, Message = "Transfer request updated." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id)
    {
        var permissionError = await CheckTransferPermission("Delete");
        if (permissionError != null) return permissionError;
        var transfer = await _db.StockTransfers.FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId);
        if (transfer == null) return NotFound();
        if (!await TransferBranchesBelongToPharmacyAsync(transfer))
            return Conflict(new { Success = false, Message = "Transfer references an invalid pharmacy branch." });
        if (transfer.Status != "Requested") return Conflict(new { Success = false, Message = "Dispatched/received transfers cannot be permanently deleted." });
        _db.StockTransfers.Remove(transfer);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { Success = false, Message = "Transfer has dependent records." }); }
        return Ok(new { Success = true, Message = "Unprocessed transfer permanently deleted." });
    }

    // =====================================================
    // CREATE STOCK TRANSFER
    //
    // POST /api/inventory/transfers
    //
    // Stock Transfers -> Create
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStockTransferDto dto)
    {
        var permissionError =
            await CheckTransferPermission("Create");

        if (permissionError != null)
            return permissionError;

        if ((!dto.FromBranchId.HasValue && string.IsNullOrWhiteSpace(dto.FromLocation)) ||
            (!dto.ToBranchId.HasValue && string.IsNullOrWhiteSpace(dto.ToLocation)) ||
            string.IsNullOrWhiteSpace(dto.MedicineName) ||
            string.IsNullOrWhiteSpace(dto.BatchNumber) ||
            dto.TransferQuantity <= 0)
            return BadRequest(new { Success = false, Message = "Source, destination, medicine, batch and positive quantity are required." });

        var sourceBranch = await ResolveTransferBranchAsync(dto.FromBranchId, dto.FromLocation);
        if (sourceBranch == null)
            return BadRequest(new { Success = false, Message = "Source branch not found, inactive or ambiguous within your pharmacy." });

        var destinationBranch = await ResolveTransferBranchAsync(dto.ToBranchId, dto.ToLocation);
        if (destinationBranch == null)
            return BadRequest(new { Success = false, Message = "Destination branch not found, inactive or ambiguous within your pharmacy." });

        var fromBranchId = sourceBranch.Id;  // Internal database foreign key
        var toBranchId = destinationBranch.Id; // Internal database foreign key
        if (fromBranchId == toBranchId)
            return BadRequest(new { Success = false, Message = "Source and destination branches cannot be the same." });

        // The PharmacyAdmin controls every branch of their own pharmacy.
        // Medicines and batches must still be in the selected SOURCE branch.
        var medicineId = await _db.Medicines
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == fromBranchId &&
                x.IsActive && x.Name == dto.MedicineName.Trim())
            .Select(x => x.Id).FirstOrDefaultAsync();
        if (medicineId <= 0)
            return BadRequest(new { Success = false, Message = "A valid medicine in the source branch is required." });

        var quantity = dto.TransferQuantity;
        int? batchId = null;

        // =================================================
        // VALIDATE DESTINATION BRANCH
        // =================================================

        var destinationExists =
            await _db.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == toBranchId &&
                    x.PharmacyId == PharmacyId &&
                    x.IsActive);

        if (!destinationExists)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Destination branch was not found or is inactive."
            });
        }

        // =================================================
        // VALIDATE SOURCE INVENTORY
        // =================================================

        var sourceInventory =
            await _db.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == fromBranchId &&
                    x.MedicineId == medicineId);

        if (sourceInventory == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Medicine inventory was not found in the source branch."
            });
        }

        if (sourceInventory.AvailableStock <
            quantity)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Insufficient available stock for this transfer."
            });
        }

        // =================================================
        // VALIDATE BATCH
        // =================================================

        if (!batchId.HasValue && !string.IsNullOrWhiteSpace(dto.BatchNumber))
            batchId = await _db.MedicineBatches.Where(x => x.InventoryId == sourceInventory.Id && x.BatchNumber == dto.BatchNumber.Trim()).Select(x => (int?)x.Id).FirstOrDefaultAsync();

        if (!batchId.HasValue)
            return BadRequest(new { Success = false, Message = "Batch not found in the source branch inventory." });

        if (batchId.HasValue)
        {
            var batch =
                await _db.MedicineBatches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == batchId.Value &&
                        x.InventoryId ==
                        sourceInventory.Id);

            if (batch == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Selected batch does not belong to this medicine inventory."
                });
            }

            if (batch.IsQuarantined)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Quarantined batch cannot be transferred."
                });
            }

            if (batch.ExpiryDate <=
                DateTime.UtcNow.Date)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Expired batch cannot be transferred."
                });
            }

            if (batch.RemainingQuantity <
                quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Insufficient stock in the selected batch."
                });
            }
        }

        // Resolve public IDs before saving so the response can never fail
        // because a source/destination branch identity mapping is missing.
        var publicFromBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(
            _db, PharmacyId, fromBranchId);
        var publicToBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(
            _db, PharmacyId, toBranchId);

        // =================================================
        // CREATE TRANSFER
        // =================================================

        var item =
            new StockTransfer
            {
                PharmacyId =
                    PharmacyId,

                FromBranchId =
                    fromBranchId,

                ToBranchId =
                    toBranchId,

                MedicineId =
                    medicineId,

                MedicineBatchId =
                    batchId,

                Quantity =
                    quantity,

                Notes =
                    string.Empty,

                RequestedByUserId = UserId,
                RequestedBy = string.IsNullOrWhiteSpace(dto.RequestedBy) ? User.Identity?.Name ?? string.Empty : dto.RequestedBy.Trim(),
                ApprovedBy = dto.ApprovedBy?.Trim() ?? string.Empty
            };

        _db.StockTransfers.Add(item);

        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(Get),
            new
            {
                id = item.Id
            },
            new
            {
                Success = true,

                Message =
                    "Stock transfer created successfully.",

                Transfer = new
                {
                    item.Id,
                    item.PharmacyId,
                    FromBranchId = publicFromBranchId,
                    ToBranchId = publicToBranchId,
                    item.MedicineId,
                    item.MedicineBatchId,
                    item.Quantity,
                    TransferQuantity = item.Quantity,
                    FromLocation = sourceBranch.Name,
                    ToLocation = destinationBranch.Name,
                    dto.MedicineName,
                    dto.BatchNumber,
                    item.RequestedBy,
                    item.ApprovedBy,
                    item.Status,
                    item.RequestedAt,
                    item.Notes
                }
            });
    }

    // =====================================================
    // LIST STOCK TRANSFERS
    //
    // GET /api/inventory/transfers
    //
    // Stock Transfers -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status)
    {
        var permissionError =
            await CheckTransferPermission("View");

        if (permissionError != null)
            return permissionError;

        var query =
            _db.StockTransfers
                .AsNoTracking()
                .Where(x => x.PharmacyId == PharmacyId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusValue =
                status.Trim();

            query =
                query.Where(x =>
                    x.Status == statusValue);
        }

        var items =
            await query
                .OrderByDescending(x =>
                    x.RequestedAt)
                .Select(x => new
                {
                    x.Id,
                    TransferId = x.Id,
                    x.FromBranchId,
                    FromLocation = _db.Branches.Where(b => b.Id == x.FromBranchId).Select(b => b.Name).FirstOrDefault(),
                    x.ToBranchId,
                    ToLocation = _db.Branches.Where(b => b.Id == x.ToBranchId).Select(b => b.Name).FirstOrDefault(),
                    x.MedicineId,
                    Medicine = _db.Medicines.Where(m => m.Id == x.MedicineId).Select(m => m.Name).FirstOrDefault(),
                    MedicineName = _db.Medicines.Where(m => m.Id == x.MedicineId).Select(m => m.Name).FirstOrDefault(),
                    x.MedicineBatchId,
                    BatchNumber = x.MedicineBatchId.HasValue
                        ? _db.MedicineBatches.Where(b => b.Id == x.MedicineBatchId.Value).Select(b => b.BatchNumber).FirstOrDefault()
                        : null,
                    x.Quantity,
                    TransferQuantity = x.Quantity,
                    x.Status,
                    x.Notes,
                    x.RequestedByUserId,
                    x.RequestedBy,
                    x.ApprovedBy,
                    TransferDate = x.RequestedAt,
                    x.RequestedAt,
                    x.CompletedAt
                })
                .ToListAsync();

        // Projection above uses database FKs; convert only at the API boundary.
        var publicIds = await GetPublicBranchIdsAsync();
        var visibleItems = items.Select(x => new
        {
            x.Id, x.TransferId,
            FromBranchId = publicIds[x.FromBranchId], x.FromLocation,
            ToBranchId = publicIds[x.ToBranchId], x.ToLocation,
            x.MedicineId, x.Medicine, x.MedicineName, x.MedicineBatchId,
            x.BatchNumber, x.Quantity, x.TransferQuantity, x.Status, x.Notes,
            x.RequestedByUserId, x.RequestedBy, x.ApprovedBy,
            x.TransferDate, x.RequestedAt, x.CompletedAt
        }).ToList();

        var allTransfers = _db.StockTransfers.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId);

        return Ok(new
        {
            Success = true,
            Total = visibleItems.Count,
            Summary = new
            {
                TotalTransfers = await allTransfers.CountAsync(),
                PendingTransfers = await allTransfers.CountAsync(x => x.Status == "Requested" || x.Status == "Approved"),
                Dispatched = await allTransfers.CountAsync(x => x.Status == "Dispatched"),
                Received = await allTransfers.CountAsync(x => x.Status == "Received"),
                Cancelled = await allTransfers.CountAsync(x => x.Status == "Cancelled")
            },
            Items = visibleItems
        });
    }

    // =====================================================
    // GET STOCK TRANSFER
    //
    // GET /api/inventory/transfers/{id}
    //
    // Stock Transfers -> View
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id)
    {
        var permissionError =
            await CheckTransferPermission("View");

        if (permissionError != null)
            return permissionError;

        var item =
            await _db.StockTransfers
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Stock transfer not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Transfer = new
            {
                item.Id, item.PharmacyId,
                FromBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(_db, PharmacyId, item.FromBranchId),
                ToBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(_db, PharmacyId, item.ToBranchId),
                FromLocation = await _db.Branches.Where(x => x.Id == item.FromBranchId).Select(x => x.Name).FirstOrDefaultAsync(),
                ToLocation = await _db.Branches.Where(x => x.Id == item.ToBranchId).Select(x => x.Name).FirstOrDefaultAsync(),
                item.MedicineId,
                MedicineName = await _db.Medicines.Where(x => x.Id == item.MedicineId).Select(x => x.Name).FirstOrDefaultAsync(),
                item.MedicineBatchId,
                BatchNumber = item.MedicineBatchId.HasValue
                    ? await _db.MedicineBatches.Where(x => x.Id == item.MedicineBatchId.Value).Select(x => x.BatchNumber).FirstOrDefaultAsync()
                    : null,
                item.Quantity,
                TransferQuantity = item.Quantity,
                item.RequestedBy, item.ApprovedBy, item.Status, item.RequestedAt, item.CompletedAt
            }
        });
    }

    // =====================================================
    // APPROVE / CANCEL TRANSFER
    //
    // POST /api/inventory/transfers/{id}/status
    //
    // Approved  -> Stock Transfers / Edit
    // Cancelled -> Stock Transfers / Delete
    // =====================================================

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> Status(
        int id,
        [FromBody] TransferStatusDto dto)
    {
        var normalizedStatus =
            dto.Status?.Trim();

        if (normalizedStatus != "Approved" &&
            normalizedStatus != "Cancelled")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Status must be Approved or Cancelled."
            });
        }

        var permissionAction =
            normalizedStatus == "Cancelled"
                ? "Delete"
                : "Edit";

        var permissionError =
            await CheckTransferPermission(
                permissionAction);

        if (permissionError != null)
            return permissionError;

        var item =
            await _db.StockTransfers
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == PharmacyId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Stock transfer not found."
            });
        }

        if (!await TransferBranchesBelongToPharmacyAsync(item))
            return Conflict(new { Success = false, Message = "Transfer references an invalid pharmacy branch." });

        if (item.Status != "Requested")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Only requested transfers can be approved or cancelled."
            });
        }

        item.Status =
            normalizedStatus;
        if (normalizedStatus == "Approved")
            item.ApprovedBy = User.Identity?.Name ?? string.Empty;

        if (normalizedStatus ==
            "Cancelled")
        {
            item.CompletedAt =
                DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                normalizedStatus ==
                "Cancelled"
                    ? "Stock transfer cancelled successfully."
                    : "Stock transfer approved successfully.",

            item.Id,
            item.Status
        });
    }

    // =====================================================
    // DISPATCH STOCK TRANSFER
    //
    // POST /api/inventory/transfers/{id}/dispatch
    //
    // Stock Transfers -> Edit
    // =====================================================

    [HttpPost("{id:int}/dispatch")]
    public async Task<IActionResult> Dispatch(
        int id)
    {
        var permissionError =
            await CheckTransferPermission("Edit");

        if (permissionError != null)
            return permissionError;

        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();

        try
        {
            var item =
                await _db.StockTransfers
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.PharmacyId == PharmacyId);

            if (item == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message =
                        "Stock transfer not found."
                });
            }

            if (!await TransferBranchesBelongToPharmacyAsync(item))
                return Conflict(new { Success = false, Message = "Transfer references an invalid pharmacy branch." });

            if (item.Status != "Approved")
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Transfer must be approved before dispatch."
                });
            }

            var inventory =
                await _db.Inventories
                    .FirstOrDefaultAsync(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId == item.FromBranchId &&
                        x.MedicineId ==
                        item.MedicineId);

            if (inventory == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Source inventory was not found."
                });
            }

            if (inventory.CurrentStock <
                    item.Quantity ||
                inventory.AvailableStock <
                    item.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Insufficient stock."
                });
            }

            MedicineBatch? sourceBatch =
                null;

            if (item.MedicineBatchId.HasValue)
            {
                sourceBatch =
                    await _db.MedicineBatches
                        .FirstOrDefaultAsync(x =>
                            x.Id ==
                            item.MedicineBatchId.Value &&
                            x.InventoryId ==
                            inventory.Id);

                if (sourceBatch == null)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Source batch was not found."
                    });
                }

                if (sourceBatch.IsQuarantined)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Quarantined batch cannot be dispatched."
                    });
                }

                if (sourceBatch.ExpiryDate <=
                    DateTime.UtcNow.Date)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Expired batch cannot be dispatched."
                    });
                }

                if (sourceBatch.RemainingQuantity <
                    item.Quantity)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Insufficient batch stock."
                    });
                }

                sourceBatch.RemainingQuantity -=
                    item.Quantity;
            }

            inventory.CurrentStock -=
                item.Quantity;

            inventory.AvailableStock -=
                item.Quantity;

            inventory.LastUpdated =
                DateTime.UtcNow;

            item.Status =
                "Dispatched";

            _db.StockTransactions.Add(
                new StockTransaction
                {
                    PharmacyId =
                        PharmacyId,

                    BranchId = item.FromBranchId,

                    MedicineId =
                        item.MedicineId,

                    MedicineBatchId =
                        item.MedicineBatchId,

                    TransactionType =
                        "BranchTransferOut",

                    Quantity =
                        item.Quantity,

                    BalanceStock =
                        inventory.CurrentStock,

                    ReferenceType =
                        "StockTransfer",

                    ReferenceId =
                        item.Id,

                    CreatedByUserId =
                        UserId,

                    Remarks =
                        item.Notes,

                    TransactionDate =
                        DateTime.UtcNow
                });

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                Success = true,

                Message =
                    "Stock transfer dispatched successfully.",

                Transfer = new
                {
                    item.Id,
                    item.Status,

                    RemainingStock =
                        inventory.CurrentStock
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
    // RECEIVE STOCK TRANSFER
    //
    // POST /api/inventory/transfers/{id}/receive
    //
    // Stock Transfers -> Edit
    // =====================================================

    [HttpPost("{id:int}/receive")]
    public async Task<IActionResult> Receive(
        int id)
    {
        var permissionError =
            await CheckTransferPermission("Edit");

        if (permissionError != null)
            return permissionError;

        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();

        try
        {
            var item =
                await _db.StockTransfers
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.PharmacyId == PharmacyId);

            if (item == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message =
                        "Stock transfer not found."
                });
            }

            if (!await TransferBranchesBelongToPharmacyAsync(item))
                return Conflict(new { Success = false, Message = "Transfer references an invalid pharmacy branch." });

            if (item.Status != "Dispatched")
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Only dispatched transfers can be received."
                });
            }

            // =================================================
            // DESTINATION INVENTORY
            // =================================================

            var inventory =
                await _db.Inventories
                    .FirstOrDefaultAsync(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId == item.ToBranchId &&
                        x.MedicineId ==
                        item.MedicineId);

            if (inventory == null)
            {
                inventory =
                    new Inventory
                    {
                        PharmacyId =
                            PharmacyId,

                        BranchId = item.ToBranchId,

                        MedicineId =
                            item.MedicineId,

                        CurrentStock =
                            0,

                        AvailableStock =
                            0,

                        ReservedStock =
                            0,

                        LastUpdated =
                            DateTime.UtcNow
                    };

                _db.Inventories.Add(
                    inventory);

                await _db.SaveChangesAsync();
            }

            inventory.CurrentStock +=
                item.Quantity;

            inventory.AvailableStock +=
                item.Quantity;

            inventory.LastUpdated =
                DateTime.UtcNow;

            int? targetBatchId =
                null;

            // =================================================
            // COPY / UPDATE BATCH
            // =================================================

            if (item.MedicineBatchId.HasValue)
            {
                var sourceBatch =
                    await _db.MedicineBatches
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.Id ==
                            item.MedicineBatchId.Value);

                if (sourceBatch == null)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Source medicine batch was not found."
                    });
                }

                var targetBatch =
                    await _db.MedicineBatches
                        .FirstOrDefaultAsync(x =>
                            x.InventoryId ==
                            inventory.Id &&
                            x.BatchNumber ==
                            sourceBatch.BatchNumber);

                if (targetBatch == null)
                {
                    targetBatch =
                        new MedicineBatch
                        {
                            InventoryId =
                                inventory.Id,

                            SupplierId =
                                sourceBatch.SupplierId,

                            BatchNumber =
                                sourceBatch.BatchNumber,

                            ManufactureDate =
                                sourceBatch.ManufactureDate,

                            ExpiryDate =
                                sourceBatch.ExpiryDate,

                            PurchasePrice =
                                sourceBatch.PurchasePrice,

                            SellingPrice =
                                sourceBatch.SellingPrice,

                            GST =
                                sourceBatch.GST,

                            QuantityReceived =
                                item.Quantity,

                            RemainingQuantity =
                                item.Quantity,

                            IsQuarantined =
                                false
                        };

                    _db.MedicineBatches.Add(
                        targetBatch);

                    await _db.SaveChangesAsync();
                }
                else
                {
                    targetBatch.QuantityReceived +=
                        item.Quantity;

                    targetBatch.RemainingQuantity +=
                        item.Quantity;
                }

                targetBatchId =
                    targetBatch.Id;
            }

            item.Status =
                "Received";

            item.CompletedAt =
                DateTime.UtcNow;

            _db.StockTransactions.Add(
                new StockTransaction
                {
                    PharmacyId =
                        PharmacyId,

                    BranchId = item.ToBranchId,

                    MedicineId =
                        item.MedicineId,

                    MedicineBatchId =
                        targetBatchId,

                    TransactionType =
                        "BranchTransferIn",

                    Quantity =
                        item.Quantity,

                    BalanceStock =
                        inventory.CurrentStock,

                    ReferenceType =
                        "StockTransfer",

                    ReferenceId =
                        item.Id,

                    CreatedByUserId =
                        UserId,

                    Remarks =
                        item.Notes,

                    TransactionDate =
                        DateTime.UtcNow
                });

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                Success = true,

                Message =
                    "Stock transfer received successfully.",

                Transfer = new
                {
                    item.Id,
                    item.Status,
                    item.CompletedAt
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