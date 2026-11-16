using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Roles = "PharmacyAdmin")]
public class PurchaseOrderController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public PurchaseOrderController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // CHECK PURCHASES PERMISSION
    // =====================================================

    private async Task<IActionResult?> CheckPurchasePermission(
        string action)
    {
        var allowed =
            await _permissionChecker.HasPermissionAsync(
                UserId,
                "Purchases",
                action);

        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        $"You do not have {action} permission for Purchases."
                });
        }

        return null;
    }

    // =====================================================
    // CREATE PURCHASE ORDER
    // POST /api/purchase-orders
    //
    // Purchases -> Create
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseOrderDto dto)
    {
        var permissionError =
            await CheckPurchasePermission("Create");

        if (permissionError != null)
            return permissionError;

        if (string.IsNullOrWhiteSpace(dto.SupplierName) ||
            string.IsNullOrWhiteSpace(dto.SupplierContactPhone) ||
            string.IsNullOrWhiteSpace(dto.MedicineName) ||
            string.IsNullOrWhiteSpace(dto.BatchNumber) ||
            dto.OrderQuantity <= 0 || dto.UnitPrice < 0)
            return BadRequest(new { Success = false, Message = "Supplier, phone, medicine, batch, positive quantity and nonnegative unit price are required." });

        var supplierId = await _db.Suppliers
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId &&
                        x.IsActive && x.Name == dto.SupplierName.Trim())
            .Select(x => x.Id).FirstOrDefaultAsync();
        if (supplierId <= 0)
            return BadRequest(new { Success = false, Message = "Supplier not found in this branch. Create the supplier first." });

        var medicineId = await _db.Medicines
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId &&
                        x.IsActive && x.Name == dto.MedicineName.Trim())
            .Select(x => x.Id).FirstOrDefaultAsync();
        if (medicineId <= 0)
            return BadRequest(new { Success = false, Message = "Medicine not found in this branch." });

        var validOrderStatuses = new[] { "Draft", "Pending", "Approved", "Sent" };
        if (!validOrderStatuses.Contains(dto.OrderStatus))
            return BadRequest(new { Success = false, Message = "Invalid initial order status." });
        var validPaymentStatuses = new[] { "Unpaid", "Partial", "Paid" };
        if (!validPaymentStatuses.Contains(dto.PaymentStatus))
            return BadRequest(new { Success = false, Message = "Invalid payment status." });

        var po =
            new PurchaseOrder
            {
                PharmacyId =
                    PharmacyId,

                BranchId =
                    BranchId,

                SupplierId =
                    supplierId,

                OrderNumber =
                    $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",

                ExpectedDate = dto.ExpectedDate,

                Notes =
                    string.Empty,

                Status = string.IsNullOrWhiteSpace(dto.OrderStatus) ? "Pending" : dto.OrderStatus.Trim(),
                PaymentStatus = string.IsNullOrWhiteSpace(dto.PaymentStatus) ? "Unpaid" : dto.PaymentStatus.Trim(),
                SupplierContactPhone = dto.SupplierContactPhone?.Trim() ?? string.Empty,
                BatchNumber = dto.BatchNumber?.Trim() ?? string.Empty,

                CreatedByUserId =
                    UserId,

                Items =
                    new List<PurchaseOrderItem>
                    {
                        new PurchaseOrderItem
                        {
                            MedicineId = medicineId,
                            OrderedQuantity = dto.OrderQuantity,
                            UnitCost = dto.UnitPrice
                        }
                    }
            };

        _db.PurchaseOrders.Add(po);

        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(Get),
            new
            {
                id = po.Id
            },
            new
            {
                Success = true,

                Message =
                    "Purchase order created successfully.",

                po.Id,

                po.OrderNumber,
                dto.SupplierName,
                dto.SupplierContactPhone,
                dto.MedicineName,
                dto.BatchNumber,
                dto.OrderQuantity,
                dto.UnitPrice,
                PaymentStatus = po.PaymentStatus,
                OrderStatus = po.Status
            });
    }

    // =====================================================
    // LIST PURCHASE ORDERS
    // GET /api/purchase-orders
    //
    // Purchases -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status)
    {
        var permissionError =
            await CheckPurchasePermission("View");

        if (permissionError != null)
            return permissionError;

        var result =
            await Query()
                .Where(x =>
                    status == null ||
                    x.Status == status)
                .OrderByDescending(x =>
                    x.OrderDate)
                .Select(x => new
                {
                    x.Id,
                    x.OrderNumber,

                    Supplier = x.Supplier.Name,
                    SupplierName = x.Supplier.Name,
                    x.SupplierContactPhone,
                    x.BatchNumber,
                    OrderStatus = x.Status,
                    MedicineName = x.Items.Select(i => i.Medicine.Name).FirstOrDefault(),
                    OrderQuantity = x.Items.Sum(i => i.OrderedQuantity),
                    UnitPrice = x.Items.Select(i => i.UnitCost).FirstOrDefault(),

                    x.OrderDate,

                    x.ExpectedDate,

                    x.Status,

                    ItemCount =
                        x.Items.Count,

                    MedicineDetails = x.Items
                        .Select(i => new
                        {
                            i.MedicineId,
                            Medicine = i.Medicine.Name,
                            Quantity = i.OrderedQuantity,
                            i.ReceivedQuantity,
                            i.UnitCost
                        })
                        .ToList(),

                    TotalAmount =
                        x.Items.Sum(i => i.OrderedQuantity * i.UnitCost),

                    Total =
                        x.Items.Sum(i => i.OrderedQuantity * i.UnitCost),

                    PaymentStatus = x.PaymentStatus
                })
                .ToListAsync();

        var allOrders = Query();

        return Ok(new
        {
            Success = true,
            Summary = new
            {
                TotalPOs = await allOrders.CountAsync(),
                Pending = await allOrders.CountAsync(x => x.Status == "Draft" || x.Status == "Pending"),
                Ordered = await allOrders.CountAsync(x => x.Status == "Approved" || x.Status == "Sent"),
                PartialReceived = await allOrders.CountAsync(x => x.Items.Any(i => i.ReceivedQuantity > 0 && i.ReceivedQuantity < i.OrderedQuantity)),
                FullyReceived = await allOrders.CountAsync(x => x.Items.Any() && x.Items.All(i => i.ReceivedQuantity >= i.OrderedQuantity)),
                PurchaseAmount = await allOrders.SelectMany(x => x.Items).SumAsync(i => (decimal?)(i.OrderedQuantity * i.UnitCost)) ?? 0m
            },
            Items = result
        });
    }

    // =====================================================
    // PENDING PURCHASE ORDERS
    // GET /api/purchase-orders/pending
    //
    // Purchases -> View
    // =====================================================

    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
    {
        var permissionError =
            await CheckPurchasePermission("View");

        if (permissionError != null)
            return permissionError;

        var result =
            await Query()
                .Where(x =>
                    x.Status != "Received" &&
                    x.Status != "Cancelled")
                .OrderByDescending(x =>
                    x.OrderDate)
                .Select(x => new
                {
                    x.Id,
                    x.OrderNumber,

                    Supplier = x.Supplier.Name,
                    SupplierName = x.Supplier.Name,
                    x.SupplierContactPhone,
                    x.BatchNumber,
                    OrderStatus = x.Status,
                    MedicineName = x.Items.Select(i => i.Medicine.Name).FirstOrDefault(),
                    OrderQuantity = x.Items.Sum(i => i.OrderedQuantity),
                    UnitPrice = x.Items.Select(i => i.UnitCost).FirstOrDefault(),

                    x.OrderDate,

                    x.ExpectedDate,

                    x.Status,

                    ItemCount =
                        x.Items.Count,

                    MedicineDetails = x.Items
                        .Select(i => new
                        {
                            i.MedicineId,
                            Medicine = i.Medicine.Name,
                            Quantity = i.OrderedQuantity,
                            i.ReceivedQuantity,
                            i.UnitCost
                        })
                        .ToList(),

                    TotalAmount =
                        x.Items.Sum(i => i.OrderedQuantity * i.UnitCost),

                    Total =
                        x.Items.Sum(i => i.OrderedQuantity * i.UnitCost),

                    PaymentStatus = x.PaymentStatus
                })
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Items = result
        });
    }

    // =====================================================
    // GET PURCHASE ORDER
    // GET /api/purchase-orders/{id}
    //
    // Purchases -> View
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id)
    {
        var permissionError =
            await CheckPurchasePermission("View");

        if (permissionError != null)
            return permissionError;

        var po =
            await Query()
                .Where(x =>
                    x.Id == id)
                .Select(x => new
                {
                    x.Id,

                    x.OrderNumber,

                    x.SupplierId,

                    Supplier = x.Supplier.Name,
                    SupplierName = x.Supplier.Name,
                    x.SupplierContactPhone,
                    x.BatchNumber,
                    OrderStatus = x.Status,
                    MedicineName = x.Items.Select(i => i.Medicine.Name).FirstOrDefault(),
                    OrderQuantity = x.Items.Sum(i => i.OrderedQuantity),
                    UnitPrice = x.Items.Select(i => i.UnitCost).FirstOrDefault(),

                    x.OrderDate,

                    x.ExpectedDate,

                    x.Status,

                    x.Notes,

                    Items =
                        x.Items
                            .Select(i => new
                            {
                                i.Id,

                                i.MedicineId,

                                Medicine =
                                    i.Medicine.Name,

                                i.OrderedQuantity,

                                i.ReceivedQuantity,

                                i.UnitCost
                            })
                            .ToList()
                })
                .FirstOrDefaultAsync();

        if (po == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Purchase order not found."
            });
        }

        return Ok(new
        {
            Success = true,
            PurchaseOrder = po
        });
    }

    // =====================================================
    // UPDATE PURCHASE ORDER STATUS
    // PATCH /api/purchase-orders/{id}/status
    //
    // Purchases -> Edit
    // =====================================================

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> Status(
        int id,
        [FromBody] PurchaseOrderStatusDto dto)
    {
        var permissionError =
            await CheckPurchasePermission("Edit");

        if (permissionError != null)
            return permissionError;

        var allowed =
            new[]
            {
                "Draft",
                "Approved",
                "Sent",
                "Cancelled",
                "Closed"
            };

        if (!allowed.Contains(dto.Status))
        {
            return BadRequest(new
            {
                Success = false,
                Message = "Invalid status."
            });
        }

        var po =
            await _db.PurchaseOrders
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId);

        if (po == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Purchase order not found."
            });
        }

        // Always load order lines before validating a status change. The initialized
        // collection can be empty even when rows exist in the database.
        await _db.Entry(po).Collection(x => x.Items).LoadAsync();
        if (po.Items.Any(x => x.ReceivedQuantity > 0) &&
            (dto.Status == "Cancelled" || dto.Status == "Draft" || dto.Status == "Approved" || dto.Status == "Sent"))
            return BadRequest(new { Success = false, Message = "Cannot reset or cancel an order after stock receipt." });
        if (po.Status == "Cancelled" || po.Status == "Received" || po.Status == "Closed")
            return BadRequest(new { Success = false, Message = "Terminal order status cannot be changed." });
        if (dto.Status == "Closed" && po.Items.Any(x => x.ReceivedQuantity < x.OrderedQuantity))
            return BadRequest(new { Success = false, Message = "Cannot close an order with outstanding quantities." });
        po.Status = dto.Status;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Purchase order status updated successfully."
        });
    }

    // =====================================================
    // RECEIVE PURCHASE ORDER
    // POST /api/purchase-orders/{id}/receive
    //
    // Purchases -> Edit
    //
    // Receiving modifies an existing purchase order,
    // inventory and batches, so Edit is the correct
    // permission from the frontend matrix.
    // =====================================================

    [HttpPost("{id:int}/receive")]
    public async Task<IActionResult> Receive(
        int id,
        [FromBody] ReceivePurchaseOrderDto dto)
    {
        var permissionError =
            await CheckPurchasePermission("Edit");

        if (permissionError != null)
            return permissionError;

        await using var tx =
            await _db.Database
                .BeginTransactionAsync();

        var po =
            await _db.PurchaseOrders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId);

        if (po == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Purchase order not found."
            });
        }

        if (po.Status == "Cancelled" || po.Status == "Closed" || po.Status == "Received")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Cancelled order cannot be received."
            });
        }

        if (dto.Items == null ||
            dto.Items.Count == 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "At least one received item is required."
            });
        }

        var receipt =
            new GoodsReceipt
            {
                PharmacyId =
                    PharmacyId,

                BranchId =
                    BranchId,

                SupplierId =
                    po.SupplierId,

                PurchaseOrderId =
                    po.Id,

                ReceiptNumber =
                    $"GRN-{DateTime.UtcNow:yyyyMMddHHmmss}",

                SupplierInvoiceNumber =
                    dto.SupplierInvoiceNumber,

                ReceivedByUserId =
                    UserId
            };

        _db.GoodsReceipts.Add(receipt);

        await _db.SaveChangesAsync();

        foreach (var line in dto.Items)
        {
            var poItem =
                po.Items.FirstOrDefault(x =>
                    x.Id ==
                    line.PurchaseOrderItemId);

            if (poItem == null ||
                line.Quantity <= 0 ||
                poItem.ReceivedQuantity +
                line.Quantity >
                poItem.OrderedQuantity)
            {
                await tx.RollbackAsync();

                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Invalid received quantity."
                });
            }

            var medicine =
                await _db.Medicines
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        poItem.MedicineId &&
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId);

            if (medicine == null)
            {
                await tx.RollbackAsync();

                return BadRequest(new
                {
                    Success = false,
                    Message =
                        $"Medicine ID {poItem.MedicineId} was not found."
                });
            }

            medicine.PurchasePrice =
                line.PurchasePrice;

            medicine.SellingPrice =
                line.SellingPrice;

            medicine.GST =
                line.GST;

            medicine.ExpiryDate =
                line.ExpiryDate;

            var inv =
                await _db.Inventories
                    .FirstOrDefaultAsync(x =>
                        x.PharmacyId ==
                        PharmacyId &&
                        x.BranchId ==
                        BranchId &&
                        x.MedicineId ==
                        poItem.MedicineId);

            if (inv == null)
            {
                inv =
                    new Inventory
                    {
                        PharmacyId =
                            PharmacyId,

                        BranchId =
                            BranchId,

                        MedicineId =
                            poItem.MedicineId
                    };

                _db.Inventories.Add(inv);
            }

            inv.CurrentStock +=
                line.Quantity;

            inv.AvailableStock +=
                line.Quantity;

            inv.MinimumStock =
                line.MinimumStock;

            inv.MaximumStock =
                line.MaximumStock;

            inv.ReorderLevel =
                line.ReorderLevel;

            inv.StorageLocation =
                line.StorageLocation;

            inv.LastUpdated =
                DateTime.UtcNow;

            await _db.SaveChangesAsync();

            var batch =
                new MedicineBatch
                {
                    InventoryId =
                        inv.Id,

                    SupplierId =
                        po.SupplierId,

                    PurchaseOrderId =
                        po.Id,

                    GoodsReceiptId =
                        receipt.Id,

                    BatchNumber =
                        line.BatchNumber,

                    ManufactureDate =
                        line.ManufactureDate,

                    ExpiryDate =
                        line.ExpiryDate,

                    PurchasePrice =
                        line.PurchasePrice,

                    SellingPrice =
                        line.SellingPrice,

                    GST =
                        line.GST,

                    QuantityReceived =
                        line.Quantity,

                    RemainingQuantity =
                        line.Quantity
                };

            _db.MedicineBatches.Add(batch);

            poItem.ReceivedQuantity +=
                line.Quantity;

            await _db.SaveChangesAsync();

            _db.StockTransactions.Add(
                new StockTransaction
                {
                    PharmacyId =
                        PharmacyId,

                    BranchId =
                        BranchId,

                    MedicineId =
                        poItem.MedicineId,

                    MedicineBatchId =
                        batch.Id,

                    TransactionType =
                        "Purchase",

                    Quantity =
                        line.Quantity,

                    BalanceStock =
                        inv.CurrentStock,

                    ReferenceType =
                        "GoodsReceipt",

                    ReferenceId =
                        receipt.Id,

                    CreatedByUserId =
                        UserId,

                    Remarks =
                        receipt.ReceiptNumber
                });
        }

        po.Status =
            po.Items.All(x =>
                x.ReceivedQuantity >=
                x.OrderedQuantity)
                ? "Received"
                : "PartiallyReceived";

        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "Purchase order received successfully.",

            Receipt = new
            {
                receipt.Id,
                receipt.ReceiptNumber
            },

            po.Status
        });
    }

    // Additional purchase-order management endpoints (pharmacy/branch scoped).
    public sealed class UpdatePurchaseOrderRequest
    {
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierContactPhone { get; set; } = string.Empty;
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public int OrderQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class UpdatePurchasePaymentRequest
    {
        public string PaymentStatus { get; set; } = string.Empty;
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePurchaseOrderRequest dto)
    {
        var permissionError = await CheckPurchasePermission("Edit");
        if (permissionError != null) return permissionError;
        var po = await _db.PurchaseOrders.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (po == null) return NotFound(new { Success = false, Message = "Purchase order not found." });
        if (po.Status != "Draft" && po.Status != "Pending")
            return BadRequest(new { Success = false, Message = "Only Draft or Pending orders can be edited." });
        if (po.Items.Count != 1 || po.Items.Any(x => x.ReceivedQuantity > 0))
            return BadRequest(new { Success = false, Message = "Orders with received stock or multiple lines cannot be edited here." });
        if (string.IsNullOrWhiteSpace(dto.SupplierName) || string.IsNullOrWhiteSpace(dto.SupplierContactPhone) ||
            string.IsNullOrWhiteSpace(dto.MedicineName) || string.IsNullOrWhiteSpace(dto.BatchNumber) ||
            dto.OrderQuantity <= 0 || dto.UnitPrice < 0)
            return BadRequest(new { Success = false, Message = "Valid supplier, phone, medicine, batch, quantity and price are required." });
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.IsActive && x.Name == dto.SupplierName.Trim());
        var medicine = await _db.Medicines.FirstOrDefaultAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.IsActive && x.Name == dto.MedicineName.Trim());
        if (supplier == null || medicine == null)
            return BadRequest(new { Success = false, Message = "Active supplier and medicine must exist in the current branch." });
        po.SupplierId = supplier.Id;
        po.SupplierContactPhone = dto.SupplierContactPhone.Trim();
        po.BatchNumber = dto.BatchNumber.Trim();
        po.ExpectedDate = dto.ExpectedDate;
        po.Notes = dto.Notes?.Trim() ?? string.Empty;
        var item = po.Items.Single();
        item.MedicineId = medicine.Id;
        item.OrderedQuantity = dto.OrderQuantity;
        item.UnitCost = dto.UnitPrice;
        await _db.SaveChangesAsync();
        return Ok(new { Success = true, Message = "Purchase order updated.", po.Id, po.OrderNumber });
    }

    // Permanent deletion is allowed only before any receipt or dependent history exists.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var permissionError = await CheckPurchasePermission("Delete");
        if (permissionError != null) return permissionError;
        var po = await _db.PurchaseOrders.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (po == null) return NotFound(new { Success = false, Message = "Purchase order not found." });
        if (po.Items.Any(x => x.ReceivedQuantity > 0) ||
            await _db.GoodsReceipts.AnyAsync(x => x.PurchaseOrderId == id) ||
            await _db.MedicineBatches.AnyAsync(x => x.PurchaseOrderId == id) ||
            (po.Status != "Draft" && po.Status != "Pending" && po.Status != "Cancelled"))
            return Conflict(new { Success = false, Message = "Purchase order has receipt/stock history or is not an eligible unreceived order; permanent deletion blocked." });
        try
        {
            _db.PurchaseOrderItems.RemoveRange(po.Items);
            _db.PurchaseOrders.Remove(po);
            await _db.SaveChangesAsync();
            return Ok(new { Success = true, Message = "Purchase order permanently deleted." });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { Success = false, Message = "Purchase order is referenced by other records; deletion blocked." });
        }
    }

    [HttpPatch("{id:int}/payment")]
    public async Task<IActionResult> UpdatePayment(int id, [FromBody] UpdatePurchasePaymentRequest dto)
    {
        var permissionError = await CheckPurchasePermission("Edit");
        if (permissionError != null) return permissionError;
        if (dto.PaymentStatus != "Unpaid" && dto.PaymentStatus != "Partial" && dto.PaymentStatus != "Paid")
            return BadRequest(new { Success = false, Message = "PaymentStatus must be Unpaid, Partial or Paid." });
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId);
        if (po == null) return NotFound(new { Success = false, Message = "Purchase order not found." });
        if (po.Status == "Cancelled") return BadRequest(new { Success = false, Message = "Cancelled orders cannot be paid." });
        po.PaymentStatus = dto.PaymentStatus;
        await _db.SaveChangesAsync();
        return Ok(new { Success = true, po.Id, po.PaymentStatus });
    }

    [HttpGet("{id:int}/receipts")]
    public async Task<IActionResult> Receipts(int id)
    {
        var permissionError = await CheckPurchasePermission("View");
        if (permissionError != null) return permissionError;
        var exists = await Query().AnyAsync(x => x.Id == id);
        if (!exists) return NotFound(new { Success = false, Message = "Purchase order not found." });
        var receipts = await _db.GoodsReceipts.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId && x.PurchaseOrderId == id)
            .OrderByDescending(x => x.ReceivedDate)
            .Select(x => new { x.Id, x.ReceiptNumber, x.SupplierInvoiceNumber, x.ReceivedDate, x.ReceivedByUserId })
            .ToListAsync();
        return Ok(new { Success = true, PurchaseOrderId = id, Receipts = receipts });
    }

    [HttpGet("/api/goods-receipts/{id:int}")]
    public async Task<IActionResult> GetGoodsReceipt(int id)
    {
        var permissionError = await CheckPurchasePermission("View");
        if (permissionError != null) return permissionError;
        var receipt = await _db.GoodsReceipts.AsNoTracking()
            .Where(x => x.Id == id && x.PharmacyId == PharmacyId && x.BranchId == BranchId)
            .Select(x => new { x.Id, x.PurchaseOrderId, x.SupplierId, x.ReceiptNumber, x.SupplierInvoiceNumber, x.ReceivedDate, x.ReceivedByUserId })
            .FirstOrDefaultAsync();
        return receipt == null ? NotFound(new { Success = false, Message = "Receipt not found." }) : Ok(new { Success = true, Receipt = receipt });
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? supplierId)
    {
        var permissionError = await CheckPurchasePermission("View");
        if (permissionError != null) return permissionError;
        if (from.HasValue && to.HasValue && from > to) return BadRequest(new { Success = false, Message = "from must be before to." });
        var q = Query().Where(x => (!from.HasValue || x.OrderDate >= from.Value) && (!to.HasValue || x.OrderDate <= to.Value) && (!supplierId.HasValue || x.SupplierId == supplierId.Value));
        var orders = await q.OrderByDescending(x => x.OrderDate).Select(x => new {
            x.Id, x.OrderNumber, x.OrderDate, x.ExpectedDate, x.SupplierId, SupplierName = x.Supplier.Name,
            x.Status, x.PaymentStatus, Total = x.Items.Sum(i => i.OrderedQuantity * i.UnitCost),
            OrderedQuantity = x.Items.Sum(i => i.OrderedQuantity), ReceivedQuantity = x.Items.Sum(i => i.ReceivedQuantity)
        }).ToListAsync();
        return Ok(new { Success = true, Orders = orders });
    }

    // =====================================================
    // PURCHASE ORDER QUERY
    // =====================================================

    private IQueryable<PurchaseOrder> Query()
    {
        return _db.PurchaseOrders
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Items)
            .ThenInclude(x => x.Medicine)
            .Where(x =>
                x.PharmacyId == PharmacyId &&
                x.BranchId == BranchId);
    }
}