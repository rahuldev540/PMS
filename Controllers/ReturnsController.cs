using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/returns")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class ReturnsController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public ReturnsController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // RETURN PERMISSION CHECK
    //
    // There is no separate Returns module in the frontend.
    //
    // Therefore:
    //
    // PharmacyAdmin
    // -> PharmacyPermissionRole
    // -> Inventory
    //
    // Pharmacist
    // -> UserPermission
    // -> Inventory
    //
    // Patient PAYMENT refund remains:
    // -> Sales / Edit
    // in BillingOperationsController.
    // =====================================================

    private async Task<IActionResult?> CheckReturnPermission(
        string action)
    {
        // =================================================
        // PHARMACY ADMIN
        // =================================================

        if (User.IsInRole("PharmacyAdmin"))
        {
            var allowed =
                await _permissionChecker.HasPermissionAsync(
                    UserId,
                    "Returns",
                    action);

            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            $"You do not have {action} permission for Returns."
                    });
            }

            return null;
        }

        // =================================================
        // PHARMACIST
        // =================================================

        if (User.IsInRole("Pharmacist"))
        {
            var permission =
                await _db.UserPermissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UserId == UserId &&
                        x.Module == "Returns" );

            if (permission == null)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            "You do not have access to Returns."
                    });
            }

            var allowed =
                action.Trim()
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
                            $"You do not have {action} permission for Returns."
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
                    "You do not have permission to access returns."
            });
    }

    // =====================================================
    // CREATE RETURN
    //
    // POST /api/pharmacy/returns
    //
    // Inventory -> Create
    //
    // ReturnType:
    // Patient
    // Supplier
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateReturnDto dto)
    {
        var permissionError =
            await CheckReturnPermission(
                "Create");

        if (permissionError != null)
            return permissionError;

        // =================================================
        // BASIC VALIDATION
        // =================================================

        if (dto.Quantity <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Return quantity must be greater than zero."
            });
        }

        if (dto.MedicineId <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid MedicineId is required."
            });
        }

        if (dto.RefundAmount < 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Refund amount cannot be negative."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Reason))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Return reason is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Disposition))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Disposition is required."
            });
        }

        var returnType =
            dto.ReturnType?.Trim();

        if (!string.Equals(
                returnType,
                "Patient",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                returnType,
                "Supplier",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "ReturnType must be Patient or Supplier."
            });
        }

        returnType =
            string.Equals(
                returnType,
                "Patient",
                StringComparison.OrdinalIgnoreCase)
                ? "Patient"
                : "Supplier";

        var disposition =
            dto.Disposition.Trim();

        // =================================================
        // MEDICINE VALIDATION
        // =================================================

        var medicine =
            await _db.Medicines
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.MedicineId &&
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (medicine == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Medicine was not found in this pharmacy."
            });
        }

        // =================================================
        // BATCH VALIDATION
        // =================================================

        MedicineBatch? batch =
            null;

        if (dto.BatchId.HasValue)
        {
            batch =
                await _db.MedicineBatches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.BatchId.Value &&
                        x.Inventory.PharmacyId ==
                        PharmacyId &&
                        x.Inventory.BranchId ==
                        BranchId &&
                        x.Inventory.MedicineId ==
                        dto.MedicineId);

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

        // =================================================
        // PATIENT RETURN VALIDATION
        // =================================================

        if (returnType == "Patient")
        {
            if (!dto.BillId.HasValue ||
                dto.BillId.Value <= 0)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "BillId is required for a patient return."
                });
            }

            var bill =
                await _db.PharmacyBills
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                        dto.BillId.Value &&
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId)
                    .Select(x => new
                    {
                        x.Id,
                        x.PaymentStatus,

                        BillItem =
                            x.BillItems
                                .Where(i =>
                                    i.MedicineId ==
                                    dto.MedicineId)
                                .Select(i => new
                                {
                                    i.Quantity,
                                    i.Total
                                })
                                .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

            if (bill == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Pharmacy bill was not found."
                });
            }

            if (bill.BillItem == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "The selected medicine does not exist in this bill."
                });
            }

            // =================================================
            // PREVENT RETURN QUANTITY GREATER THAN SOLD QUANTITY
            // =================================================

            var alreadyReturned =
                await _db.PharmacyReturns
                    .AsNoTracking()
                    .Where(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId &&
                        x.ReturnType ==
                        "Patient" &&
                        x.PharmacyBillId ==
                        dto.BillId.Value &&
                        x.MedicineId ==
                        dto.MedicineId &&
                        x.Status !=
                        "Cancelled")
                    .SumAsync(x =>
                        (int?)x.Quantity)
                ?? 0;

            if (alreadyReturned +
                dto.Quantity >
                bill.BillItem.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,

                    Message =
                        "Return quantity exceeds the quantity sold on this bill.",

                    SoldQuantity =
                        bill.BillItem.Quantity,

                    AlreadyReturned =
                        alreadyReturned,

                    RequestedReturn =
                        dto.Quantity
                });
            }

            if (string.Equals(
                    disposition,
                    "Restock",
                    StringComparison.OrdinalIgnoreCase) &&
                !dto.BatchId.HasValue)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "BatchId is required when patient return disposition is Restock."
                });
            }
        }

        // =================================================
        // SUPPLIER RETURN VALIDATION
        // =================================================

        if (returnType == "Supplier")
        {
            if (!dto.SupplierId.HasValue ||
                dto.SupplierId.Value <= 0)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "SupplierId is required for a supplier return."
                });
            }

            var supplierExists =
                await _db.Suppliers
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id ==
                        dto.SupplierId.Value &&
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId &&
                        x.IsActive);

            if (!supplierExists)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Supplier was not found or is inactive."
                });
            }

            if (!dto.BatchId.HasValue)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "BatchId is required for a supplier return."
                });
            }

            var inventory =
                await _db.Inventories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId &&
                        x.MedicineId ==
                        dto.MedicineId);

            if (inventory == null ||
                inventory.CurrentStock <
                dto.Quantity ||
                inventory.AvailableStock <
                dto.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Insufficient stock for supplier return."
                });
            }

            if (batch == null ||
                batch.RemainingQuantity <
                dto.Quantity)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Insufficient batch stock for supplier return."
                });
            }
        }

        // =================================================
        // CREATE RETURN
        // =================================================

        var entity =
            new PharmacyReturn
            {
                PharmacyId =
                    PharmacyId,

                BranchId =
                    BranchId,

                ReturnType =
                    returnType,

                MedicineId =
                    dto.MedicineId,

                MedicineBatchId =
                    dto.BatchId,

                PharmacyBillId =
                    returnType == "Patient"
                        ? dto.BillId
                        : null,

                SupplierId =
                    returnType == "Supplier"
                        ? dto.SupplierId
                        : null,

                Quantity =
                    dto.Quantity,

                RefundAmount =
                    dto.RefundAmount,

                Reason =
                    dto.Reason.Trim(),

                Disposition =
                    disposition,

                CreatedByUserId =
                    UserId
            };

        _db.PharmacyReturns.Add(
            entity);

        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(Get),
            new
            {
                id = entity.Id
            },
            new
            {
                Success = true,

                Message =
                    "Return created successfully.",

                Return = new
                {
                    entity.Id,
                    entity.ReturnType,
                    entity.MedicineId,
                    entity.MedicineBatchId,
                    entity.PharmacyBillId,
                    entity.SupplierId,
                    entity.Quantity,
                    entity.RefundAmount,
                    entity.Reason,
                    entity.Disposition,
                    entity.Status,
                    entity.CreatedAt
                }
            });
    }

    // =====================================================
    // LIST RETURNS
    //
    // GET /api/pharmacy/returns
    //
    // Inventory -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? type,
        [FromQuery] string? status)
    {
        var permissionError =
            await CheckReturnPermission(
                "View");

        if (permissionError != null)
            return permissionError;

        var query =
            _db.PharmacyReturns
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId);

        if (!string.IsNullOrWhiteSpace(
                type))
        {
            var returnType =
                type.Trim();

            query =
                query.Where(x =>
                    x.ReturnType ==
                    returnType);
        }

        if (!string.IsNullOrWhiteSpace(
                status))
        {
            var statusValue =
                status.Trim();

            query =
                query.Where(x =>
                    x.Status ==
                    statusValue);
        }

        var items =
            await query
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Select(x => new
                {
                    x.Id,

                    x.ReturnType,

                    x.MedicineId,

                    Medicine =
                        _db.Medicines
                            .Where(m =>
                                m.Id ==
                                x.MedicineId)
                            .Select(m =>
                                m.Name)
                            .FirstOrDefault(),

                    x.MedicineBatchId,

                    BatchNumber =
                        x.MedicineBatchId.HasValue
                            ? _db.MedicineBatches
                                .Where(b =>
                                    b.Id ==
                                    x.MedicineBatchId.Value)
                                .Select(b =>
                                    b.BatchNumber)
                                .FirstOrDefault()
                            : null,

                    x.PharmacyBillId,

                    x.SupplierId,

                    Supplier =
                        x.SupplierId.HasValue
                            ? _db.Suppliers
                                .Where(s =>
                                    s.Id ==
                                    x.SupplierId.Value)
                                .Select(s =>
                                    s.Name)
                                .FirstOrDefault()
                            : null,

                    x.Quantity,

                    x.RefundAmount,

                    x.Reason,

                    x.Disposition,

                    x.Status,

                    x.CreatedAt,

                    x.CreatedByUserId
                })
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Total = items.Count,
            Items = items
        });
    }

    // =====================================================
    // GET RETURN
    //
    // GET /api/pharmacy/returns/{id}
    //
    // Inventory -> View
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id)
    {
        var permissionError =
            await CheckReturnPermission(
                "View");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid return id is required."
            });
        }

        var item =
            await _db.PharmacyReturns
                .AsNoTracking()
                .Where(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId)
                .Select(x => new
                {
                    x.Id,

                    x.ReturnType,

                    x.MedicineId,

                    Medicine =
                        _db.Medicines
                            .Where(m =>
                                m.Id ==
                                x.MedicineId)
                            .Select(m =>
                                m.Name)
                            .FirstOrDefault(),

                    x.MedicineBatchId,

                    BatchNumber =
                        x.MedicineBatchId.HasValue
                            ? _db.MedicineBatches
                                .Where(b =>
                                    b.Id ==
                                    x.MedicineBatchId.Value)
                                .Select(b =>
                                    b.BatchNumber)
                                .FirstOrDefault()
                            : null,

                    x.PharmacyBillId,

                    x.SupplierId,

                    Supplier =
                        x.SupplierId.HasValue
                            ? _db.Suppliers
                                .Where(s =>
                                    s.Id ==
                                    x.SupplierId.Value)
                                .Select(s =>
                                    s.Name)
                                .FirstOrDefault()
                            : null,

                    x.Quantity,

                    x.RefundAmount,

                    x.Reason,

                    x.Disposition,

                    x.Status,

                    x.CreatedAt,

                    x.CreatedByUserId
                })
                .FirstOrDefaultAsync();

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Return was not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Return = item
        });
    }

    // =====================================================
    // APPROVE RETURN
    //
    // POST /api/pharmacy/returns/{id}/approve
    //
    // Inventory -> Edit
    // =====================================================

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(
        int id)
    {
        var permissionError =
            await CheckReturnPermission(
                "Edit");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid return id is required."
            });
        }

        await using var transaction =
            await _db.Database
                .BeginTransactionAsync();

        try
        {
            var item =
                await _db.PharmacyReturns
                    .FirstOrDefaultAsync(x =>
                        x.Id == id &&
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId);

            if (item == null)
            {
                return NotFound(new
                {
                    Success = false,
                    Message =
                        "Return was not found."
                });
            }

            if (item.Status != "Pending")
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Only pending returns can be approved."
                });
            }

            var inventory =
                await _db.Inventories
                    .FirstOrDefaultAsync(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId &&
                        x.MedicineId ==
                        item.MedicineId);

            if (inventory == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Inventory was not found."
                });
            }

            // =================================================
            // PATIENT RETURN
            // =================================================

            if (item.ReturnType ==
                "Patient")
            {
                // ---------------------------------------------
                // RESTOCK
                // ---------------------------------------------

                if (string.Equals(
                        item.Disposition,
                        "Restock",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (!item.MedicineBatchId.HasValue)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message =
                                "BatchId is required for restocking."
                        });
                    }

                    var batch =
                        await _db.MedicineBatches
                            .FirstOrDefaultAsync(x =>
                                x.Id ==
                                item.MedicineBatchId.Value &&
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

                    inventory.CurrentStock +=
                        item.Quantity;

                    inventory.AvailableStock +=
                        item.Quantity;

                    inventory.LastUpdated =
                        DateTime.UtcNow;

                    batch.RemainingQuantity +=
                        item.Quantity;

                    _db.StockTransactions.Add(
                        CreateStockTransaction(
                            item,
                            inventory.CurrentStock,
                            "PatientReturnRestock"));
                }

                // ---------------------------------------------
                // NON-RESTOCK PATIENT RETURN
                //
                // No inventory is added.
                // Example:
                // Dispose / Damaged / Quarantine
                // ---------------------------------------------

                else
                {
                    // No stock addition.
                    // The physical returned item is not
                    // considered available inventory.
                }
            }

            // =================================================
            // SUPPLIER RETURN
            // =================================================

            else if (item.ReturnType ==
                     "Supplier")
            {
                if (!item.MedicineBatchId.HasValue)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "BatchId is required for a supplier return."
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
                            "Insufficient inventory stock."
                    });
                }

                var batch =
                    await _db.MedicineBatches
                        .FirstOrDefaultAsync(x =>
                            x.Id ==
                            item.MedicineBatchId.Value &&
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

                if (batch.RemainingQuantity <
                    item.Quantity)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message =
                            "Insufficient batch stock."
                    });
                }

                inventory.CurrentStock -=
                    item.Quantity;

                inventory.AvailableStock -=
                    item.Quantity;

                inventory.LastUpdated =
                    DateTime.UtcNow;

                batch.RemainingQuantity -=
                    item.Quantity;

                _db.StockTransactions.Add(
                    CreateStockTransaction(
                        item,
                        inventory.CurrentStock,
                        "SupplierReturn"));
            }
            else
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Invalid return type."
                });
            }

            item.Status =
                "Approved";

            await _db.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                Success = true,

                Message =
                    "Return approved successfully.",

                Return = new
                {
                    item.Id,
                    item.ReturnType,
                    item.Status,
                    item.Quantity,
                    item.Disposition
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
    // CANCEL RETURN
    //
    // POST /api/pharmacy/returns/{id}/cancel
    //
    // Inventory -> Delete
    //
    // Pharmacy Admin only
    // =====================================================

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> Cancel(
        int id)
    {
        var permissionError =
            await CheckReturnPermission(
                "Delete");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid return id is required."
            });
        }

        var item =
            await _db.PharmacyReturns
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId);

        if (item == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Return was not found."
            });
        }

        if (item.Status != "Pending")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Only pending returns can be cancelled."
            });
        }

        item.Status =
            "Cancelled";

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Return cancelled successfully."
        });
    }

    // =====================================================
    // CREATE STOCK TRANSACTION
    // =====================================================

    private StockTransaction CreateStockTransaction(
        PharmacyReturn pharmacyReturn,
        int balance,
        string type)
    {
        return new StockTransaction
        {
            PharmacyId =
                PharmacyId,

            BranchId =
                BranchId,

            MedicineId =
                pharmacyReturn.MedicineId,

            MedicineBatchId =
                pharmacyReturn.MedicineBatchId,

            TransactionType =
                type,

            Quantity =
                pharmacyReturn.Quantity,

            BalanceStock =
                balance,

            Remarks =
                pharmacyReturn.Reason,

            ReferenceType =
                "PharmacyReturn",

            ReferenceId =
                pharmacyReturn.Id,

            CreatedByUserId =
                UserId,

            TransactionDate =
                DateTime.UtcNow
        };
    }
}