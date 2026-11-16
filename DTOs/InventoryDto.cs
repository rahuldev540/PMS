namespace AuthDemo.DTOs;

public class InventoryDto
{
    public int InventoryId { get; set; }

    public int MedicineId { get; set; }

    public string MedicineName { get; set; } = string.Empty;

    public string MedicineCode { get; set; } = string.Empty;

    public int CurrentStock { get; set; }

    public int ReservedStock { get; set; }

    public int AvailableStock { get; set; }

    public int MinimumStock { get; set; }

    public int MaximumStock { get; set; }

    public int ReorderLevel { get; set; }

    public string StorageLocation { get; set; } = string.Empty;

    // All batches belonging to this medicine inventory record.
    public List<InventoryBatchDto> Batches { get; set; } = new();

    // Expiry alert fields (populated by near-expiry endpoints)
    public int? BatchId { get; set; }

    public string? BatchNumber { get; set; }

    public int? Quantity { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public int? DaysLeft { get; set; }

    public string? Status { get; set; }
}

public class InventoryBatchDto
{
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int QuantityReceived { get; set; }
    public int RemainingQuantity { get; set; }
    public DateTime ManufactureDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal GST { get; set; }
    public bool IsQuarantined { get; set; }
    public bool IsRecalled { get; set; }
}
