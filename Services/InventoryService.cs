using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // ADD STOCK
    //
    // Standalone scope:
    // PharmacyId + BranchId
    //
    // NEW BATCH:
    //   -> Increase Inventory
    //   -> Create MedicineBatch
    //   -> Create StockTransaction
    //
    // EXISTING SAME BATCH:
    //   -> Increase Inventory
    //   -> Increase batch QuantityReceived
    //   -> Increase batch RemainingQuantity
    //   -> Create NEW StockTransaction
    // =====================================================

    public async Task<string> AddStockAsync(
        AddStockDto dto,
        int pharmacyId,
        int branchId,
        int userId)
    {
        // =================================================
        // VALIDATION
        // =================================================

        if (pharmacyId <= 0)
            throw new ArgumentException(
                "A valid pharmacy id is required.");

        if (branchId <= 0)
            throw new ArgumentException(
                "A valid branch id is required.");

        if (dto.MedicineId <= 0)
            throw new ArgumentException(
                "A valid medicine id is required.");

        if (dto.Quantity <= 0)
            throw new ArgumentException(
                "Stock quantity must be greater than zero.");

        if (string.IsNullOrWhiteSpace(dto.BatchNumber))
            throw new ArgumentException(
                "Batch number is required.");

        if (dto.ExpiryDate.Date <= DateTime.UtcNow.Date)
            throw new ArgumentException(
                "Expiry date must be a future date.");

        if (dto.PurchasePrice < 0)
            throw new ArgumentException(
                "Purchase price cannot be negative.");

        if (dto.SellingPrice < 0)
            throw new ArgumentException(
                "Selling price cannot be negative.");

        if (dto.GST < 0 || dto.GST > 100)
            throw new ArgumentException(
                "GST must be between 0 and 100.");

        // =================================================
        // CHECK MEDICINE
        // =================================================

        var medicine = await _context.Medicines
            .FirstOrDefaultAsync(x =>
                x.Id == dto.MedicineId &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.IsActive);

        if (medicine == null)
        {
            throw new ArgumentException(
                "Medicine not found for the selected pharmacy and branch.");
        }

        // =================================================
        // RESOLVE SUPPLIER
        // =================================================

        if (!dto.SupplierId.HasValue &&
            !string.IsNullOrWhiteSpace(dto.Supplier))
        {
            var supplierName = dto.Supplier.Trim();

            dto.SupplierId = await _context.Suppliers
                .Where(x =>
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.IsActive &&
                    x.Name == supplierName)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        }

        if (dto.SupplierId.HasValue)
        {
            var supplierExists =
                await _context.Suppliers.AnyAsync(x =>
                    x.Id == dto.SupplierId.Value &&
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.IsActive);

            if (!supplierExists)
            {
                throw new ArgumentException(
                    "Supplier not found for the selected pharmacy and branch.");
            }
        }

        var normalizedBatchNumber =
            dto.BatchNumber.Trim();

        await using var dbTransaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // =================================================
            // GET / CREATE INVENTORY
            // =================================================

            var inventory =
                await _context.Inventories
                    .FirstOrDefaultAsync(x =>
                        x.MedicineId == dto.MedicineId &&
                        x.PharmacyId == pharmacyId &&
                        x.BranchId == branchId);

            if (inventory == null)
            {
                inventory = new Inventory
                {
                    PharmacyId = pharmacyId,

                    BranchId = branchId,

                    MedicineId = dto.MedicineId,

                    CurrentStock = 0,

                    ReservedStock = 0,

                    AvailableStock = 0,

                    MinimumStock =
                        dto.MinimumStock,

                    MaximumStock =
                        dto.MaximumStock,

                    ReorderLevel =
                        dto.ReorderLevel,

                    StorageLocation =
                        dto.StorageLocation?.Trim()
                        ?? string.Empty,

                    LastUpdated =
                        DateTime.UtcNow
                };

                _context.Inventories.Add(inventory);

                // Generate InventoryId before batch creation.
                await _context.SaveChangesAsync();
            }
            else
            {
                // =============================================
                // INVENTORY SETTINGS
                // =============================================

                inventory.MinimumStock =
                    dto.MinimumStock;

                inventory.MaximumStock =
                    dto.MaximumStock;

                inventory.ReorderLevel =
                    dto.ReorderLevel;

                // Don't accidentally erase an existing rack
                // when the Add Stock request doesn't provide one.
                if (!string.IsNullOrWhiteSpace(
                        dto.StorageLocation))
                {
                    inventory.StorageLocation =
                        dto.StorageLocation.Trim();
                }
            }

            // =================================================
            // FIND EXISTING BATCH
            // =================================================

            var existingBatch =
                await _context.MedicineBatches
                    .FirstOrDefaultAsync(x =>
                        x.InventoryId == inventory.Id &&
                        x.BatchNumber ==
                            normalizedBatchNumber);

            // =================================================
            // SAME BATCH RECEIVED AGAIN
            // =================================================

            if (existingBatch != null)
            {
                // Same batch number should represent the same
                // manufacturer's batch, so expiry must match.

                if (existingBatch.ExpiryDate.Date !=
                    dto.ExpiryDate.Date)
                {
                    throw new InvalidOperationException(
                        $"Batch '{normalizedBatchNumber}' already exists " +
                        $"with expiry date " +
                        $"{existingBatch.ExpiryDate:yyyy-MM-dd}. " +
                        $"The same batch number cannot have a different expiry date.");
                }

                // ---------------------------------------------
                // INCREASE EXISTING BATCH QUANTITY
                // ---------------------------------------------

                existingBatch.QuantityReceived +=
                    dto.Quantity;

                existingBatch.RemainingQuantity +=
                    dto.Quantity;

                // ---------------------------------------------
                // UPDATE CURRENT COMMERCIAL DETAILS
                //
                // If supplier sends same batch later at a
                // changed purchase/selling price, keep latest
                // values on the active batch.
                // ---------------------------------------------

                existingBatch.PurchasePrice =
                    dto.PurchasePrice;

                existingBatch.SellingPrice =
                    dto.SellingPrice;

                existingBatch.GST =
                    dto.GST;

                if (dto.SupplierId.HasValue)
                {
                    existingBatch.SupplierId =
                        dto.SupplierId;
                }

                if (dto.ManufactureDate.HasValue)
                {
                    existingBatch.ManufactureDate =
                        dto.ManufactureDate.Value;
                }

                if (dto.PurchaseDate.HasValue)
                {
                    existingBatch.PurchaseDate =
                        dto.PurchaseDate;
                }

                if (!string.IsNullOrWhiteSpace(dto.Notes))
                {
                    existingBatch.Notes =
                        dto.Notes.Trim();
                }
            }

            // =================================================
            // NEW BATCH
            // =================================================

            else
            {
                var batch = new MedicineBatch
                {
                    InventoryId =
                        inventory.Id,

                    BatchNumber =
                        normalizedBatchNumber,

                    SupplierId =
                        dto.SupplierId,

                    ManufactureDate =
                        dto.ManufactureDate ??
                        dto.PurchaseDate ??
                        DateTime.UtcNow.Date,

                    PurchaseDate =
                        dto.PurchaseDate,

                    Notes =
                        dto.Notes?.Trim()
                        ?? string.Empty,

                    ExpiryDate =
                        dto.ExpiryDate,

                    PurchasePrice =
                        dto.PurchasePrice,

                    SellingPrice =
                        dto.SellingPrice,

                    GST =
                        dto.GST,

                    QuantityReceived =
                        dto.Quantity,

                    RemainingQuantity =
                        dto.Quantity,

                    CreatedAt =
                        DateTime.UtcNow
                };

                _context.MedicineBatches.Add(batch);
            }

            // =================================================
            // UPDATE TOTAL INVENTORY
            // =================================================

            inventory.CurrentStock +=
                dto.Quantity;

            // Available stock must always be:
            //
            // CurrentStock - ReservedStock

            inventory.AvailableStock =
                Math.Max(
                    0,
                    inventory.CurrentStock -
                    inventory.ReservedStock);

            inventory.LastUpdated =
                DateTime.UtcNow;

            // =================================================
            // STOCK TRANSACTION
            //
            // IMPORTANT:
            // A new transaction is created EVERY TIME stock
            // arrives, including when it is the same batch.
            // =================================================

            var stockTransaction =
                new StockTransaction
                {
                    PharmacyId =
                        pharmacyId,

                    BranchId =
                        branchId,

                    MedicineId =
                        dto.MedicineId,

                    TransactionType =
                        "Purchase",

                    Quantity =
                        dto.Quantity,

                    BalanceStock =
                        inventory.CurrentStock,

                    Remarks =
                        $"Stock Added - Batch {normalizedBatchNumber}",

                    ReferenceType =
                        "ManualStockEntry",

                    CreatedByUserId =
                        userId,

                    TransactionDate =
                        DateTime.UtcNow
                };

            _context.StockTransactions.Add(
                stockTransaction);

            // =================================================
            // SAVE
            // =================================================

            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            if (existingBatch != null)
            {
                return
                    $"Stock added successfully to existing batch " +
                    $"{normalizedBatchNumber}.";
            }

            return
                $"Stock added successfully. New batch " +
                $"{normalizedBatchNumber} created.";
        }
        catch
        {
            await dbTransaction.RollbackAsync();

            throw;
        }
    }

    // =====================================================
    // GET INVENTORY
    // =====================================================

    public async Task<List<InventoryDto>> GetInventoryAsync(
        int pharmacyId,
        int branchId)
        => await GetInventoryPageAsync(pharmacyId, branchId, null, null);

    // Batch data is fetched only for inventory rows on the requested page.
    public async Task<List<InventoryDto>> GetInventoryPageAsync(
        int pharmacyId, int branchId, int? pageNumber, int? pageSize)
    {
        var query = _context.Inventories
            .AsNoTracking()
            .Where(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.Medicine.IsActive)
            .OrderBy(x => x.Medicine.Name)
            .Select(x => new InventoryDto
            {
                InventoryId = x.Id,

                MedicineId = x.MedicineId,

                MedicineCode =
                    x.Medicine.MedicineCode,

                MedicineName =
                    x.Medicine.Name,

                CurrentStock =
                    x.CurrentStock,

                ReservedStock =
                    x.ReservedStock,

                AvailableStock =
                    x.AvailableStock,

                MinimumStock =
                    x.MinimumStock,

                MaximumStock =
                    x.MaximumStock,

                ReorderLevel =
                    x.ReorderLevel,

                StorageLocation =
                    x.StorageLocation
            })
            ;

        var inventory = pageNumber.HasValue && pageSize.HasValue
            ? await query.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync()
            : await query.ToListAsync();

        if (inventory.Count == 0) return inventory;

        // Match MedicineBatches.InventoryId to Inventories.Id, not MedicineId.
        var inventoryIds = inventory.Select(x => x.InventoryId).ToList();
        var batches = await _context.MedicineBatches.AsNoTracking()
            .Where(x => inventoryIds.Contains(x.InventoryId))
            .OrderBy(x => x.ExpiryDate)
            .Select(x => new { x.InventoryId, Batch = new InventoryBatchDto
            {
                BatchId = x.Id,
                BatchNumber = x.BatchNumber,
                QuantityReceived = x.QuantityReceived,
                RemainingQuantity = x.RemainingQuantity,
                ManufactureDate = x.ManufactureDate,
                ExpiryDate = x.ExpiryDate,
                PurchasePrice = x.PurchasePrice,
                SellingPrice = x.SellingPrice,
                GST = x.GST,
                IsQuarantined = x.IsQuarantined,
                IsRecalled = x.IsRecalled
            } })
            .ToListAsync();
        var grouped = batches.GroupBy(x => x.InventoryId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Batch).ToList());
        foreach (var item in inventory)
        {
            if (!grouped.TryGetValue(item.InventoryId, out var associated)) continue;
            item.Batches = associated;
            // Retain backwards-compatible single-batch fields using earliest expiry.
            var first = associated.FirstOrDefault(x => x.RemainingQuantity > 0 && !x.IsQuarantined && !x.IsRecalled)
                        ?? associated.First();
            item.BatchId = first.BatchId;
            item.BatchNumber = first.BatchNumber;
            item.Quantity = first.RemainingQuantity;
            item.ExpiryDate = first.ExpiryDate;
            item.DaysLeft = (first.ExpiryDate.Date - DateTime.UtcNow.Date).Days;
        }
        return inventory;
    }

    // =====================================================
    // LOW STOCK
    // =====================================================

    public async Task<List<InventoryDto>> GetLowStockAsync(
        int pharmacyId,
        int branchId)
    {
        return await _context.Inventories
            .AsNoTracking()
            .Include(x => x.Medicine)
            .Where(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.Medicine.IsActive &&
                x.AvailableStock <= x.ReorderLevel)
            .OrderBy(x => x.AvailableStock)
            .ThenBy(x => x.Medicine.Name)
            .Select(x => new InventoryDto
            {
                InventoryId = x.Id,

                MedicineId = x.MedicineId,

                MedicineCode =
                    x.Medicine.MedicineCode,

                MedicineName =
                    x.Medicine.Name,

                CurrentStock =
                    x.CurrentStock,

                ReservedStock =
                    x.ReservedStock,

                AvailableStock =
                    x.AvailableStock,

                MinimumStock =
                    x.MinimumStock,

                MaximumStock =
                    x.MaximumStock,

                ReorderLevel =
                    x.ReorderLevel,

                StorageLocation =
                    x.StorageLocation
            })
            .ToListAsync();
    }

    // =====================================================
    // OUT OF STOCK
    // =====================================================

    public async Task<List<InventoryDto>> GetOutOfStockAsync(
        int pharmacyId,
        int branchId)
    {
        return await _context.Inventories
            .AsNoTracking()
            .Include(x => x.Medicine)
            .Where(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.Medicine.IsActive &&
                x.AvailableStock <= 0)
            .OrderBy(x => x.Medicine.Name)
            .Select(x => new InventoryDto
            {
                InventoryId = x.Id,

                MedicineId = x.MedicineId,

                MedicineCode =
                    x.Medicine.MedicineCode,

                MedicineName =
                    x.Medicine.Name,

                CurrentStock =
                    x.CurrentStock,

                ReservedStock =
                    x.ReservedStock,

                AvailableStock =
                    x.AvailableStock,

                MinimumStock =
                    x.MinimumStock,

                MaximumStock =
                    x.MaximumStock,

                ReorderLevel =
                    x.ReorderLevel,

                StorageLocation =
                    x.StorageLocation
            })
            .ToListAsync();
    }

    // =====================================================
    // NEAR EXPIRY
    //
    // Returns batches expiring within 30 days.
    // Only batches with remaining quantity are returned.
    // =====================================================

    public async Task<List<InventoryDto>> GetNearExpiryAsync(
        int pharmacyId,
        int branchId)
    {
        var today =
            DateTime.UtcNow.Date;

        var nearExpiryDate =
            today.AddDays(30);

        return await _context.MedicineBatches
            .AsNoTracking()
            .Where(x =>
                x.Inventory.PharmacyId == pharmacyId &&
                x.Inventory.BranchId == branchId &&
                x.Inventory.Medicine.IsActive &&
                x.ExpiryDate >= today &&
                x.ExpiryDate <= nearExpiryDate &&
                x.RemainingQuantity > 0)
            .OrderBy(x => x.ExpiryDate)
            .ThenBy(x =>
                x.Inventory.Medicine.Name)
            .Select(x => new InventoryDto
            {
                InventoryId =
                    x.InventoryId,

                MedicineId =
                    x.Inventory.MedicineId,

                MedicineCode =
                    x.Inventory.Medicine.MedicineCode,

                MedicineName =
                    x.Inventory.Medicine.Name,

                CurrentStock =
                    x.Inventory.CurrentStock,

                ReservedStock =
                    x.Inventory.ReservedStock,

                AvailableStock =
                    x.Inventory.AvailableStock,

                MinimumStock =
                    x.Inventory.MinimumStock,

                MaximumStock =
                    x.Inventory.MaximumStock,

                ReorderLevel =
                    x.Inventory.ReorderLevel,

                StorageLocation =
                    x.Inventory.StorageLocation,

                BatchId =
                    x.Id,

                BatchNumber =
                    x.BatchNumber,

                Quantity =
                    x.RemainingQuantity,

                ExpiryDate =
                    x.ExpiryDate,

                DaysLeft =
                    EF.Functions.DateDiffDay(
                        today,
                        x.ExpiryDate),

                Status =
                    "NearExpiry"
            })
            .ToListAsync();
    }
}