namespace AuthDemo.Models;

public class MedicineBatch
{
    public int Id { get; set; }

    // Batch ownership is inherited through Inventory:
    // Pharmacy -> Branch -> Inventory -> MedicineBatch.
    public int InventoryId { get; set; }

    public Inventory Inventory { get; set; } = null!;

    public int? SupplierId { get; set; }

    public int? PurchaseOrderId { get; set; }

    public int? GoodsReceiptId { get; set; }

    public bool IsQuarantined { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public DateTime ManufactureDate { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }

    public decimal PurchasePrice { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal GST { get; set; }

    public int QuantityReceived { get; set; }

    public int RemainingQuantity { get; set; }

    public bool IsRecalled { get; set; }

    public string? RecallReason { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
