using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AuthDemo.DTOs;

// =====================================================
// MEDICINE IMPORT FILE UPLOAD
// POST /api/medicine-import/upload
// =====================================================

public class MedicineImportUploadDto
{
    [Required(ErrorMessage = "A CSV or XLSX file is required.")]
    public IFormFile File { get; set; } = null!;

    // Pharmacy Admin selects any branch in their pharmacy; Pharmacist uses assigned branch.
    public int? BranchId { get; set; }
}

// =====================================================
// MEDICINE IMPORT ROW DATA
//
// One Excel/CSV row can contain:
// 1. Medicine details
// 2. Supplier details
// 3. Batch details
// 4. Initial inventory / stock details
// =====================================================

public class MedicineImportDataDto
{
    // =================================================
    // MEDICINE DETAILS
    // =================================================

    public string MedicineCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    public string Name { get; set; } = string.Empty;

    public string GenericName { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string DosageForm { get; set; } = string.Empty;

    public string Strength { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string PackSize { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public bool PrescriptionRequired { get; set; }

    public string Description { get; set; } = string.Empty;


    // =================================================
    // SUPPLIER DETAILS
    //
    // SupplierName should be treated as the minimum
    // supplier information during import.
    //
    // Other properties are optional so old Excel files
    // containing only SupplierCode/SupplierName still work.
    // =================================================

    public string SupplierCode { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string SupplierContactPerson { get; set; } = string.Empty;

    public string SupplierPhone { get; set; } = string.Empty;

    public string SupplierEmail { get; set; } = string.Empty;

    public string SupplierAddress { get; set; } = string.Empty;

    public string SupplierCity { get; set; } = string.Empty;

    public string SupplierState { get; set; } = string.Empty;

    public string SupplierTaxNumber { get; set; } = string.Empty;

    public string SupplierNotes { get; set; } = string.Empty;


    // =================================================
    // BATCH DETAILS
    // =================================================

    public string BatchNumber { get; set; } = string.Empty;

    public DateTime ManufactureDate { get; set; }

    public DateTime ExpiryDate { get; set; }


    // =================================================
    // STOCK / INVENTORY DETAILS
    //
    // Quantity represents stock coming from this
    // particular import row.
    //
    // Example:
    // Dolo Quantity = 100
    //
    // New medicine:
    // CurrentStock = 100
    // AvailableStock = 100
    //
    // Existing medicine behavior is controlled by
    // DuplicateBatchAction during commit.
    // =================================================

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "Quantity cannot be negative.")]
    public int Quantity { get; set; }


    // =================================================
    // PRICING
    // =================================================

    [Range(
        0,
        double.MaxValue,
        ErrorMessage = "PurchasePrice cannot be negative.")]
    public decimal PurchasePrice { get; set; }

    [Range(
        0,
        double.MaxValue,
        ErrorMessage = "SellingPrice cannot be negative.")]
    public decimal SellingPrice { get; set; }

    [Range(
        0,
        100,
        ErrorMessage = "GST must be between 0 and 100.")]
    public decimal GST { get; set; }


    // =================================================
    // INVENTORY CONFIGURATION
    // =================================================

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "MinimumStock cannot be negative.")]
    public int MinimumStock { get; set; }

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "MaximumStock cannot be negative.")]
    public int MaximumStock { get; set; }

    [Range(
        0,
        int.MaxValue,
        ErrorMessage = "ReorderLevel cannot be negative.")]
    public int ReorderLevel { get; set; }

    public string StorageLocation { get; set; } = string.Empty;
}


// =====================================================
// ROW RETURNED TO FRONTEND
//
// Used when previewing staged bulk-import data.
// =====================================================

public class MedicineImportRowDto
{
    public int Id { get; set; }

    public int RowNumber { get; set; }

    public string MedicineCode { get; set; } = string.Empty;

    public MedicineImportDataDto Data { get; set; } = new();

    // Example:
    // Create
    // Update
    // Skip
    public string Action { get; set; } = string.Empty;

    // Example:
    // Valid
    // Invalid
    // Imported
    // Failed
    public string Status { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;
}


// =====================================================
// MANUALLY ADD STAGED ROW
//
// POST
// /api/medicine-import/{importId}/rows
// =====================================================

public class CreateMedicineImportRowDto
{
    [Required(ErrorMessage = "Medicine row data is required.")]
    public MedicineImportDataDto Data { get; set; } = new();
}


// =====================================================
// EDIT STAGED IMPORT ROW
//
// PUT
// /api/medicine-import/{importId}/rows/{rowId}
// =====================================================

public class UpdateMedicineImportRowDto
{
    [Required(ErrorMessage = "Medicine row data is required.")]
    public MedicineImportDataDto Data { get; set; } = new();
}


// =====================================================
// CONFIRM AND COMMIT IMPORT
//
// Used after preview/validation.
// =====================================================

public class CommitMedicineImportDto
{
    // -------------------------------------------------
    // MEDICINE
    // -------------------------------------------------

    // If medicine already exists:
    // true  -> update medicine master details
    // false -> keep existing medicine details
    public bool UpdateExistingMedicines { get; set; } = true;


    // -------------------------------------------------
    // SUPPLIERS
    // -------------------------------------------------

    // true:
    // Supplier data from Excel should be processed.
    //
    // Existing supplier -> reuse/update
    // New supplier      -> create
    //
    // false:
    // Ignore supplier data during commit.
    public bool ImportSuppliers { get; set; } = true;


    // -------------------------------------------------
    // STOCK
    // -------------------------------------------------

    // true:
    // Quantity/Batch/Inventory data should be imported.
    //
    // false:
    // Only medicine/supplier data is processed.
    public bool ImportStock { get; set; } = true;


    // -------------------------------------------------
    // INVALID ROW HANDLING
    // -------------------------------------------------

    // true:
    // Ignore invalid rows and continue importing
    // valid rows.
    //
    // false:
    // Invalid rows should stop/fail commit depending
    // on service implementation.
    public bool SkipInvalidRows { get; set; }


    // -------------------------------------------------
    // DUPLICATE BATCH HANDLING
    // -------------------------------------------------
    //
    // Supported:
    //
    // Error
    //     Existing batch -> return error.
    //
    // Skip
    //     Existing batch -> ignore row stock.
    //
    // AddQuantity
    //     Existing batch -> add quantity to existing
    //     batch and inventory.
    //
    // Example:
    //
    // Current Dolo stock = 100
    // Excel quantity      = 50
    //
    // AddQuantity:
    //
    // Inventory = 150
    //
    // IMPORTANT:
    // MedicineImportService must implement this logic.
    // DTO alone does not modify stock.
    // -------------------------------------------------

    public string DuplicateBatchAction { get; set; } = "Error";
}