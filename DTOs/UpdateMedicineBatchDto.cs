using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class UpdateMedicineBatchDto
{
    [Range(0, double.MaxValue,
        ErrorMessage = "Purchase price cannot be negative.")]
    public decimal PurchasePrice { get; set; }

    [Range(0, double.MaxValue,
        ErrorMessage = "Selling price cannot be negative.")]
    public decimal SellingPrice { get; set; }

    [Range(0, 100,
        ErrorMessage = "GST must be between 0 and 100.")]
    public decimal GST { get; set; }

    [Required(ErrorMessage = "Expiry date is required.")]
    public DateTime ExpiryDate { get; set; }

    public DateTime? ManufactureDate { get; set; }

    public DateTime? PurchaseDate { get; set; }

    public int? SupplierId { get; set; }

    public string? Notes { get; set; }
}