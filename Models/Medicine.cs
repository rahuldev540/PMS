namespace AuthDemo.Models;

public class Medicine
{
    public int Id { get; set; }

    // ==============================
    // Pharmacy & Branch Ownership
    // ==============================

    // Standalone pharmacy ownership.
    public int PharmacyId { get; set; }

    // Medicine belongs to a specific pharmacy branch.
    public int BranchId { get; set; }


    // ==============================
    // Medicine Details
    // ==============================

    public string MedicineCode { get; set; } = string.Empty;

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

    // Real-world pharmacy controls (additive; existing APIs remain compatible)
    public string? Barcode { get; set; }

    public bool IsControlledMedicine { get; set; }

    public bool IsRecalled { get; set; }

    public string? RecallReason { get; set; }

    // ==============================
    // Inventory / Pricing
    // ==============================

    public int StockQuantity { get; set; } = 0;

    public decimal PurchasePrice { get; set; }

    public decimal SellingPrice { get; set; }

    // GST Percentage
    public decimal GST { get; set; }

    public DateTime? ExpiryDate { get; set; }

    // ==============================
    // Status
    // ==============================

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ==============================
    // Navigation Properties
    // ==============================

    public ICollection<PrescriptionItem> PrescriptionItems { get; set; }
        = new List<PrescriptionItem>();
}
