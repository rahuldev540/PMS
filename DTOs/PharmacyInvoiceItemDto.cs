namespace AuthDemo.DTOs;

public class PharmacyInvoiceItemDto
{
    public string MedicineName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal Rate { get; set; }

    public decimal GST { get; set; }

    public decimal Total { get; set; }
}