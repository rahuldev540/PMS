
using AuthDemo.DTOs;
using Microsoft.AspNetCore.Http;

namespace AuthDemo.Services.Interfaces;

public interface IMedicineImportService
{
    // Upload and validate CSV/XLSX
    Task<object> ValidateAsync(
        IFormFile file,
        int pharmacyId,
        int branchId,
        int userId);

    // Commit validated medicine rows
    Task<object> CommitAsync(
        int importId,
        CommitMedicineImportDto dto,
        int pharmacyId,
        int branchId,
        int userId);

    // Get import status
    Task<object?> GetStatusAsync(
        int importId,
        int pharmacyId,
        int branchId);

    // Download validation errors
    Task<byte[]> GetErrorsAsync(
        int importId,
        int pharmacyId,
        int branchId);

    // View staged rows
    Task<List<MedicineImportRowDto>> GetRowsAsync(
        int importId,
        int pharmacyId,
        int branchId);

    // Manually add a staged row
    Task<MedicineImportRowDto> CreateRowAsync(
        int importId,
        CreateMedicineImportRowDto dto,
        int pharmacyId,
        int branchId);

    // Edit a staged row
    Task<MedicineImportRowDto> UpdateRowAsync(
        int importId,
        int rowId,
        UpdateMedicineImportRowDto dto,
        int pharmacyId,
        int branchId);

    // Delete a staged row
    Task DeleteRowAsync(
        int importId,
        int rowId,
        int pharmacyId,
        int branchId);
}
