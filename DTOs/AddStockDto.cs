using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class AddStockDto
{
    [Range(1, int.MaxValue)] public int MedicineId { get; set; }
    [Required] public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ManufactureDate { get; set; }
    [Required] public DateTime ExpiryDate { get; set; }
    [Range(0, double.MaxValue)] public decimal PurchasePrice { get; set; }
    public decimal CostPrice { get => PurchasePrice; set => PurchasePrice = value; }
    [Range(0, double.MaxValue)] public decimal SellingPrice { get; set; }
    [Range(0, 100)] public decimal GST { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
    public int StockQuantity { get => Quantity; set => Quantity = value; }
    [Range(0, int.MaxValue)] public int MinimumStock { get; set; }
    [Range(0, int.MaxValue)] public int MaximumStock { get; set; }
    [Range(0, int.MaxValue)] public int ReorderLevel { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}
