using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;

public interface IMedicineService
{
    Task<List<MedicineDto>> GetMedicinesPageAsync(int pharmacyId, int branchId, int? pageNumber, int? pageSize);

    // =====================================================
    // CREATE MEDICINE
    //
    // Creates medicine master/catalogue only.
    // Stock and batch information are handled separately.
    //
    // Standalone scope:
    // PharmacyId + BranchId
    // =====================================================

    Task<string> CreateMedicineAsync(
        CreateMedicineDto dto,
        int pharmacyId,
        int branchId);


    // =====================================================
    // GET ALL MEDICINES
    // =====================================================

    Task<List<MedicineDto>> GetAllMedicinesAsync(
        int pharmacyId,
        int branchId);


    // =====================================================
    // GET MEDICINE BY ID
    // =====================================================

    Task<MedicineDto?> GetMedicineByIdAsync(
        int id,
        int pharmacyId,
        int branchId);


    // =====================================================
    // UPDATE MEDICINE MASTER
    //
    // Updates catalogue/master information only.
    //
    // Does NOT update:
    // - Stock quantity
    // - Batch
    // - Purchase price
    // - Selling price
    // - GST
    // - Expiry
    // - Rack / storage location
    // - Inventory levels
    // =====================================================

    Task<string> UpdateMedicineAsync(
        int id,
        UpdateMedicineDto dto,
        int pharmacyId,
        int branchId);


    // =====================================================
    // DELETE / DEACTIVATE MEDICINE
    //
    // Soft delete.
    // Existing stock/history/billing records are preserved.
    // =====================================================

    Task<string> DeleteMedicineAsync(
        int id,
        int pharmacyId,
        int branchId);


    // =====================================================
    // SEARCH MEDICINE
    // =====================================================

    Task<List<MedicineSearchDto>> SearchMedicineAsync(
        string keyword,
        int pharmacyId,
        int branchId);


    // =====================================================
    // GET AVAILABLE MEDICINES
    //
    // Returns active medicines where:
    //
    // AvailableStock > 0
    //
    // for the logged-in:
    // PharmacyId + BranchId
    // =====================================================

    Task<List<MedicineSearchDto>> GetAvailableMedicinesAsync(
        int pharmacyId,
        int branchId);
}