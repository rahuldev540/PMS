namespace AuthDemo.DTOs;

public class MedicineDto
{
    public int Id { get; set; }

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

    public int MinimumStockThreshold { get; set; }

    public int ReorderStockLevel { get; set; }

    public decimal SellingPrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal GST { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public int Stock { get; set; }
    public int MaximumStock { get; set; }
    public string StorageLocation { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
