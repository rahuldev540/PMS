namespace AuthDemo.DTOs;

public class MedicineInventoryDetailsDto
{
    public int MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string GenericName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string PackSize { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool PrescriptionRequired { get; set; }
    public int InventoryId { get; set; }
    public int CurrentStock { get; set; }
    public int AvailableStock { get; set; }
    public int ReservedStock { get; set; }
    public int MinimumStock { get; set; }
    public int MaximumStock { get; set; }
    public int ReorderLevel { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string StockStatus { get; set; } = string.Empty;
    public List<MedicineBatchDetailsDto> Batches { get; set; } = new();
}

public class MedicineBatchDetailsDto
{
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime ManufactureDate { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal GST { get; set; }
    public int QuantityReceived { get; set; }
    public int AvailablePieces { get; set; }
    public bool IsQuarantined { get; set; }
    public string ExpiryStatus { get; set; } = string.Empty;
}
