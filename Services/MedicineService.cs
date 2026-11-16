using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class MedicineService : IMedicineService
{
    private readonly AppDbContext _context;

    public MedicineService(AppDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // CREATE MEDICINE
    //
    // Creates ONLY medicine master/catalogue information.
    //
    // Stock, batch, expiry, prices and inventory settings
    // must be handled through Inventory APIs.
    // =====================================================

    public async Task<string> CreateMedicineAsync(
        CreateMedicineDto dto,
        int pharmacyId,
        int branchId)
    {
        if (pharmacyId <= 0)
            throw new ArgumentException("Valid PharmacyId is required.");

        if (branchId <= 0)
            throw new ArgumentException("Valid BranchId is required.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Medicine name is required.");

        if (string.IsNullOrWhiteSpace(dto.Category))
            throw new ArgumentException("Medicine category is required.");

        if (string.IsNullOrWhiteSpace(dto.DosageForm))
            throw new ArgumentException("Dosage form is required.");

        var medicineCode = string.IsNullOrWhiteSpace(dto.MedicineCode)
            ? $"MED-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : dto.MedicineCode.Trim();

        var exists = await _context.Medicines.AnyAsync(x =>
            x.MedicineCode == medicineCode &&
            x.PharmacyId == pharmacyId &&
            x.BranchId == branchId);

        if (exists)
            throw new InvalidOperationException(
                "Medicine code already exists.");

        var medicine = new Medicine
        {
            PharmacyId = pharmacyId,
            BranchId = branchId,

            MedicineCode = medicineCode,

            Name = dto.Name.Trim(),

            GenericName =
                dto.GenericName?.Trim() ?? string.Empty,

            BrandName =
                dto.BrandName?.Trim() ?? string.Empty,

            Category =
                dto.Category.Trim(),

            DosageForm =
                dto.DosageForm.Trim(),

            Strength =
                dto.Strength?.Trim() ?? string.Empty,

            Manufacturer =
                dto.Manufacturer?.Trim() ?? string.Empty,

            PackSize =
                string.IsNullOrWhiteSpace(dto.PackSize)
                    ? dto.Unit?.Trim() ?? string.Empty
                    : dto.PackSize.Trim(),

            Unit =
                string.IsNullOrWhiteSpace(dto.Unit)
                    ? dto.PackSize?.Trim() ?? string.Empty
                    : dto.Unit.Trim(),

            PrescriptionRequired =
                dto.PrescriptionRequired,

            Description =
                dto.Description?.Trim() ?? string.Empty,

            IsActive =
                !string.Equals(
                    dto.CatalogueStatus?.Trim(),
                    "Inactive",
                    StringComparison.OrdinalIgnoreCase),

            CreatedAt = DateTime.UtcNow
        };

        _context.Medicines.Add(medicine);

        await _context.SaveChangesAsync();

        // IMPORTANT:
        //
        // Inventory is NOT created here.
        //
        // After creating the medicine, use:
        //
        // POST /api/inventory/add-stock
        //
        // to create inventory / batch / stock transaction.

        return "Medicine created successfully.";
    }

    // =====================================================
    // GET ALL MEDICINES
    //
    // Optimized: three bounded, branch-scoped queries instead
    // of repeated correlated subqueries per medicine.
    // =====================================================

    public async Task<List<MedicineDto>> GetAllMedicinesAsync(
        int pharmacyId,
        int branchId)
        => await GetMedicinesPageAsync(pharmacyId, branchId, null, null);

    // Optional, database-side pagination; legacy callers retain full-list behavior.
    public async Task<List<MedicineDto>> GetMedicinesPageAsync(
        int pharmacyId, int branchId, int? pageNumber, int? pageSize)
    {
        var query = _context.Medicines
            .AsNoTracking()
            .Where(m => m.PharmacyId == pharmacyId &&
                        m.BranchId == branchId &&
                        m.IsActive)
            .OrderBy(m => m.Name)
            .ThenBy(m => m.Id);

        var medicines = pageNumber.HasValue && pageSize.HasValue
            ? await query.Skip((pageNumber.Value - 1) * pageSize.Value).Take(pageSize.Value).ToListAsync()
            : await query.ToListAsync();

        return await BuildMedicineDtosAsync(medicines, pharmacyId, branchId);
    }

    // =====================================================
    // GET MEDICINE BY ID
    // =====================================================

    public async Task<MedicineDto?> GetMedicineByIdAsync(
        int id,
        int pharmacyId,
        int branchId)
    {
        var medicine = await _context.Medicines
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id &&
                                      m.PharmacyId == pharmacyId &&
                                      m.BranchId == branchId &&
                                      m.IsActive);

        if (medicine == null)
            return null;

        var results = await BuildMedicineDtosAsync(
            new List<Medicine> { medicine }, pharmacyId, branchId);

        return results[0];
    }

    private async Task<List<MedicineDto>> BuildMedicineDtosAsync(
        List<Medicine> medicines,
        int pharmacyId,
        int branchId)
    {
        if (medicines.Count == 0)
            return new List<MedicineDto>();

        var medicineIds = medicines.Select(m => m.Id).ToArray();

        // SQL Server can translate this to a set-based IN/OPENJSON query.
        // Do not query the database separately for each medicine.
        var inventories = await _context.Inventories
            .AsNoTracking()
            .Where(i => i.PharmacyId == pharmacyId &&
                        i.BranchId == branchId &&
                        medicineIds.Contains(i.MedicineId))
            .OrderBy(i => i.Id)
            .ToListAsync();

        // Preserve the previous FirstOrDefault inventory semantics.
        var inventoryByMedicine = inventories
            .GroupBy(i => i.MedicineId)
            .ToDictionary(g => g.Key, g => g.First());

        var inventoryIds = inventories.Select(i => i.Id).ToArray();

        // Select the earliest-expiring batch with remaining quantity
        // for each medicine, matching the previous catalogue behavior.
        var batches = inventoryIds.Length == 0
            ? new List<MedicineBatch>()
            : await _context.MedicineBatches
                .AsNoTracking()
                .Where(b => inventoryIds.Contains(b.InventoryId) &&
                            b.RemainingQuantity > 0)
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.Id)
                .ToListAsync();

        var medicineByInventory = inventories.ToDictionary(i => i.Id, i => i.MedicineId);

        var batchByMedicine = batches
            .GroupBy(b => medicineByInventory[b.InventoryId])
            .ToDictionary(g => g.Key, g => g.First());

        var results = new List<MedicineDto>(medicines.Count);

        foreach (var m in medicines)
        {
            inventoryByMedicine.TryGetValue(m.Id, out var inventory);
            batchByMedicine.TryGetValue(m.Id, out var batch);

            results.Add(new MedicineDto
            {
                Id = m.Id,
                MedicineCode = m.MedicineCode,
                Name = m.Name,
                GenericName = m.GenericName,
                BrandName = m.BrandName,
                Category = m.Category,
                DosageForm = m.DosageForm,
                Strength = m.Strength,
                Manufacturer = m.Manufacturer,
                PackSize = m.PackSize,
                Unit = string.IsNullOrEmpty(m.Unit) ? m.PackSize : m.Unit,
                PrescriptionRequired = m.PrescriptionRequired,
                Description = m.Description,

                MinimumStockThreshold = inventory?.MinimumStock ?? 0,
                ReorderStockLevel = inventory?.ReorderLevel ?? 0,
                MaximumStock = inventory?.MaximumStock ?? 0,
                StorageLocation = inventory?.StorageLocation ?? string.Empty,
                Stock = inventory?.AvailableStock ?? 0,

                SellingPrice = batch?.SellingPrice ?? 0m,
                PurchasePrice = batch?.PurchasePrice ?? 0m,
                GST = batch?.GST ?? 0m,
                ExpiryDate = batch?.ExpiryDate,

                Status = m.IsActive ? "Active" : "Inactive"
            });
        }

        return results;
    }

    // =====================================================
    // UPDATE MEDICINE
    //
    // Updates ONLY medicine master/catalogue fields.
    //
    // Does NOT change:
    // - quantity
    // - inventory
    // - prices
    // - expiry
    // - batch
    // - storage location
    // =====================================================

    public async Task<string> UpdateMedicineAsync(
        int id,
        UpdateMedicineDto dto,
        int pharmacyId,
        int branchId)
    {
        var medicine =
            await _context.Medicines
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId == pharmacyId &&
                    x.BranchId == branchId &&
                    x.IsActive);

        if (medicine == null)
            throw new KeyNotFoundException("Medicine not found or already deleted.");

        var normalizedCode =
            string.IsNullOrWhiteSpace(dto.MedicineCode)
                ? medicine.MedicineCode
                : dto.MedicineCode.Trim();

        var duplicateCode =
            await _context.Medicines.AnyAsync(x =>
                x.Id != id &&
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.MedicineCode == normalizedCode);

        if (duplicateCode)
            throw new InvalidOperationException(
                "Medicine code already exists.");

        medicine.MedicineCode = normalizedCode;

        medicine.Name =
            dto.Name.Trim();

        medicine.GenericName =
            dto.GenericName?.Trim() ?? string.Empty;

        medicine.BrandName =
            dto.BrandName?.Trim() ?? string.Empty;

        medicine.Category =
            dto.Category.Trim();

        medicine.DosageForm =
            dto.DosageForm.Trim();

        medicine.Strength =
            dto.Strength?.Trim() ?? string.Empty;

        medicine.Manufacturer =
            dto.Manufacturer?.Trim() ?? string.Empty;

        medicine.PackSize =
            string.IsNullOrWhiteSpace(dto.PackSize)
                ? dto.Unit?.Trim() ?? string.Empty
                : dto.PackSize.Trim();

        medicine.Unit =
            string.IsNullOrWhiteSpace(dto.Unit)
                ? medicine.PackSize
                : dto.Unit.Trim();

        medicine.PrescriptionRequired =
            dto.PrescriptionRequired;

        medicine.Description =
            dto.Description?.Trim() ?? string.Empty;

        medicine.IsActive =
            !string.Equals(
                dto.CatalogueStatus?.Trim(),
                "Inactive",
                StringComparison.OrdinalIgnoreCase);

        // IMPORTANT:
        //
        // Do NOT update Inventory here.
        // Do NOT update MedicineBatch here.
        // Do NOT update CurrentStock here.
        // Do NOT update AvailableStock here.

        await _context.SaveChangesAsync();

        return "Medicine updated successfully.";
    }

    // =====================================================
    // DELETE / DEACTIVATE MEDICINE
    //
    // Permanently delete only unused medicine masters; never erase clinical/stock history.
    // =====================================================
    public async Task<string> DeleteMedicineAsync(int id, int pharmacyId, int branchId)
    {
        var medicine = await _context.Medicines.FirstOrDefaultAsync(x =>
            x.Id == id && x.PharmacyId == pharmacyId && x.BranchId == branchId);
        if (medicine == null) throw new KeyNotFoundException("Medicine not found.");
        if (await _context.Inventories.AnyAsync(x => x.MedicineId == id) ||
            await _context.PrescriptionItems.AnyAsync(x => x.MedicineId == id) ||
            await _context.PurchaseOrderItems.AnyAsync(x => x.MedicineId == id) ||
            await _context.StockTransactions.AnyAsync(x => x.MedicineId == id) ||
            await _context.StockTransfers.AnyAsync(x => x.MedicineId == id) ||
            await _context.StockAdjustments.AnyAsync(x => x.MedicineId == id))
            throw new InvalidOperationException("Medicine has inventory or transaction history; permanent deletion blocked (409). Remove only unused medicines.");
        _context.Medicines.Remove(medicine);
        await _context.SaveChangesAsync();
        return "Medicine permanently deleted.";
    }

    // =====================================================
    // SEARCH MEDICINE
    // =====================================================

    public async Task<List<MedicineSearchDto>> SearchMedicineAsync(
        string keyword,
        int pharmacyId,
        int branchId)
    {
        keyword = keyword?.Trim() ?? string.Empty;

        return await _context.Medicines
            .AsNoTracking()
            .Where(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.IsActive &&
                (
                    x.Name.Contains(keyword) ||
                    x.GenericName.Contains(keyword) ||
                    x.BrandName.Contains(keyword) ||
                    x.MedicineCode.Contains(keyword)
                ))
            .Select(x => new MedicineSearchDto
            {
                Id = x.Id,

                MedicineCode = x.MedicineCode,

                Name = x.Name,

                GenericName = x.GenericName,

                Strength = x.Strength,

                AvailableStock =
                    _context.Inventories
                        .Where(i =>
                            i.MedicineId == x.Id &&
                            i.PharmacyId == pharmacyId &&
                            i.BranchId == branchId)
                        .Select(i => i.AvailableStock)
                        .FirstOrDefault()
            })
            .OrderBy(x => x.Name)
            .Take(20)
            .ToListAsync();
    }

    // =====================================================
    // GET AVAILABLE MEDICINES
    //
    // Only medicines with AvailableStock > 0 are returned.
    // =====================================================

    public async Task<List<MedicineSearchDto>>
        GetAvailableMedicinesAsync(
            int pharmacyId,
            int branchId)
    {
        return await _context.Medicines
            .AsNoTracking()
            .Where(m =>
                m.PharmacyId == pharmacyId &&
                m.BranchId == branchId &&
                m.IsActive)
            .Join(
                _context.Inventories
                    .AsNoTracking()
                    .Where(i =>
                        i.PharmacyId == pharmacyId &&
                        i.BranchId == branchId &&
                        i.AvailableStock > 0),

                medicine => medicine.Id,

                inventory => inventory.MedicineId,

                (medicine, inventory) =>
                    new MedicineSearchDto
                    {
                        Id = medicine.Id,

                        MedicineCode =
                            medicine.MedicineCode,

                        Name =
                            medicine.Name,

                        GenericName =
                            medicine.GenericName,

                        Strength =
                            medicine.Strength,

                        AvailableStock =
                            inventory.AvailableStock
                    })
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}