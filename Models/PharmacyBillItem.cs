namespace AuthDemo.Models;

public class PharmacyBillItem
{
    public int Id { get; set; }

    // Scope is inherited through PharmacyBill:
    // Pharmacy -> Branch -> PharmacyBill -> PharmacyBillItem.
    public int PharmacyBillId { get; set; }

    public PharmacyBill PharmacyBill { get; set; } = null!;

    public int MedicineId { get; set; }

    public string MedicineName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal Rate { get; set; }

    public decimal GST { get; set; }

    public decimal Total { get; set; }
}
