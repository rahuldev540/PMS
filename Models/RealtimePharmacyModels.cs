namespace AuthDemo.Models;

public class MedicineRecall
{
    public int Id { get; set; }

    // Standalone pharmacy ownership
    public int PharmacyId { get; set; }

    // Recall belongs to a specific pharmacy branch
    public int BranchId { get; set; }

    // Legacy nullable database field retained for schema compatibility

public int MedicineId { get; set; }

    public int? MedicineBatchId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PharmacyShift
{
    public int Id { get; set; }

    // Standalone pharmacy ownership
    public int PharmacyId { get; set; }

    // Shift belongs to a specific pharmacy branch
    public int BranchId { get; set; }

    // Legacy nullable database field retained for schema compatibility

public int UserId { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAt { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal? ExpectedCash { get; set; }

    public string Status { get; set; } = "Open";

    public string Notes { get; set; } = string.Empty;
}

public class SupplierInvoice
{
    public int Id { get; set; }

    // Standalone pharmacy ownership
    public int PharmacyId { get; set; }

    // Invoice belongs to a specific pharmacy branch
    public int BranchId { get; set; }

    // Legacy nullable database field retained for schema compatibility

public int SupplierId { get; set; }

    public int? PurchaseOrderId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public DateTime? DueDate { get; set; }

    public string Status { get; set; } = "Unpaid";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
