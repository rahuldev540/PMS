using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;

public interface IInventoryService
{
    Task<List<InventoryDto>> GetInventoryPageAsync(int pharmacyId, int branchId, int? pageNumber, int? pageSize);

    // =====================================================
    // ADD STOCK
    //
    // Standalone pharmacy scope:
    // PharmacyId + BranchId
    //
    // Creates/updates:
    // - Inventory
    // - MedicineBatch
    // - StockTransaction
    //
    // HospitalId is NOT required.
    // =====================================================

    Task<string> AddStockAsync(
        AddStockDto dto,
        int pharmacyId,
        int branchId,
        int userId);


    // =====================================================
    // GET INVENTORY
    //
    // Returns inventory for the selected
    // PharmacyId + BranchId.
    // =====================================================

    Task<List<InventoryDto>> GetInventoryAsync(
        int pharmacyId,
        int branchId);


    // =====================================================
    // GET LOW STOCK
    //
    // Returns medicines where available stock
    // has reached or fallen below reorder level.
    // =====================================================

    Task<List<InventoryDto>> GetLowStockAsync(
        int pharmacyId,
        int branchId);


    // =====================================================
    // GET OUT OF STOCK
    //
    // Returns medicines having no available stock.
    // =====================================================

    Task<List<InventoryDto>> GetOutOfStockAsync(
        int pharmacyId,
        int branchId);


    // =====================================================
    // GET NEAR EXPIRY
    //
    // Returns medicine batches approaching expiry
    // for the selected PharmacyId + BranchId.
    // =====================================================

    Task<List<InventoryDto>> GetNearExpiryAsync(
        int pharmacyId,
        int branchId);
}