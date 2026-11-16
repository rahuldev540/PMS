namespace AuthDemo.DTOs;

public class PayPharmacyBillDto
{
    public int BillId { get; set; }

    public string PaymentMode { get; set; } = string.Empty;

    public string? TransactionId { get; set; }

    public decimal PaidAmount { get; set; }
}