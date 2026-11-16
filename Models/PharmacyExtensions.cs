namespace AuthDemo.Models;

public class Supplier
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDate { get; set; }
    public string Status { get; set; } = "Draft";
    public string Notes { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Unpaid";
    public string SupplierContactPhone { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;
    public int OrderedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class GoodsReceipt
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int SupplierId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public int ReceivedByUserId { get; set; }
}

public class PharmacyPayment
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int PharmacyBillId { get; set; }
    public PharmacyBill PharmacyBill { get; set; } = null!;
    public string PaymentMode { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string Status { get; set; } = "Paid";
    public int ReceivedByUserId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
}

public class StockAdjustment
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }

public int MedicineId { get; set; }
    public int? MedicineBatchId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PharmacyReturn
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }

public string ReturnType { get; set; } = "Patient";
    public int MedicineId { get; set; }
    public int? MedicineBatchId { get; set; }
    public int? PharmacyBillId { get; set; }
    public int? SupplierId { get; set; }
    public int Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Disposition { get; set; } = "Dispose";
    public string Status { get; set; } = "Pending";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StockTransfer
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }

public int MedicineId { get; set; }
    public int? MedicineBatchId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "Requested";
    public string Notes { get; set; } = string.Empty;
    public int RequestedByUserId { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class PharmacyAlert
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }

public string AlertType { get; set; } = string.Empty;
    public int? MedicineId { get; set; }
    public int? MedicineBatchId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
