namespace AuthDemo.Models;

// =====================================================
// BULK MEDICINE IMPORT
// Stores one uploaded CSV/XLSX import operation
// =====================================================

public class MedicineImport
{
    public int Id { get; set; }

    // =====================================================
    // HOSPITAL AND BRANCH
    // =====================================================

    public int PharmacyId { get; set; }

    // Optional CMS/legacy link. Standalone PMS does not require it.

    public int BranchId { get; set; }

    // =====================================================
    // UPLOADED FILE
    // =====================================================

    public string FileName { get; set; } = string.Empty;

    // Uploaded
    // Validating
    // Validated
    // ValidationFailed
    // Processing
    // Completed
    // PartiallyCompleted
    // Failed
    // Cancelled
    public string Status { get; set; } = "Uploaded";

    // =====================================================
    // VALIDATION TOTALS
    // =====================================================

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    // =====================================================
    // IMPORT RESULTS
    // =====================================================

    public int CreatedMedicines { get; set; }

    public int UpdatedMedicines { get; set; }

    public int CreatedSuppliers { get; set; }

    public int CreatedBatches { get; set; }

    public int StockUnitsAdded { get; set; }

    // =====================================================
    // USER AND DATES
    // =====================================================

    public int UploadedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // =====================================================
    // IMPORT ROWS
    // =====================================================

    public ICollection<MedicineImportRow> Rows { get; set; }
        = new List<MedicineImportRow>();
}

// =====================================================
// BULK MEDICINE IMPORT ROW
// Stores validation/import result for every uploaded row
// =====================================================

public class MedicineImportRow
{
    public int Id { get; set; }

    // =====================================================
    // IMPORT RELATIONSHIP
    // =====================================================

    public int MedicineImportId { get; set; }

    public MedicineImport MedicineImport { get; set; }
        = null!;

    // =====================================================
    // SOURCE ROW
    // =====================================================

    public int RowNumber { get; set; }

    public string MedicineCode { get; set; }
        = string.Empty;

    // Original row serialized as JSON.
    public string RawData { get; set; }
        = string.Empty;

    // Pending
    // Create
    // Update
    // Invalid
    // Skip
    public string Action { get; set; }
        = "Pending";

    // Pending
    // Valid
    // Invalid
    // Imported
    // Skipped
    // Failed
    public string Status { get; set; }
        = "Pending";

    public string ErrorMessage { get; set; }
        = string.Empty;

    // =====================================================
    // CREATED OR UPDATED RECORD IDS
    // =====================================================

    public int? MedicineId { get; set; }

    public int? SupplierId { get; set; }

    public int? InventoryId { get; set; }

    public int? MedicineBatchId { get; set; }

    // =====================================================
    // PROCESSING DATE
    // =====================================================

    public DateTime? ProcessedAt { get; set; }
}