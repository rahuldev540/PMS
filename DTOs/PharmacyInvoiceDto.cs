namespace AuthDemo.DTOs;

public class PharmacyInvoiceDto
{
    public int BillId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime BillDate { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string DoctorName { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }

    public decimal GST { get; set; }

    public decimal Discount { get; set; }

    public decimal GrandTotal { get; set; }

    public List<PharmacyInvoiceItemDto> Medicines { get; set; }
        = new();
}