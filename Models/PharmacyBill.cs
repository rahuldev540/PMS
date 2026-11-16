namespace AuthDemo.Models;

public class PharmacyBill
{
    public int Id { get; set; }

    // =====================================
    // PHARMACY & BRANCH OWNERSHIP
    // =====================================

    // Standalone pharmacy ownership.
    public int PharmacyId { get; set; }

    // Bill always belongs to a specific pharmacy branch.
    public int BranchId { get; set; }


    // =====================================
    // PATIENT
    // =====================================

    public int PatientId { get; set; }

    // =====================================
    // PRESCRIPTION
    // =====================================

    public int PrescriptionId { get; set; }

    public Prescription? Prescription { get; set; }

    // =====================================
    // BILL AMOUNTS
    // =====================================

    public decimal SubTotal { get; set; }

    public decimal Discount { get; set; }

    public decimal GSTAmount { get; set; }

    public decimal GrandTotal { get; set; }

    // =====================================
    // PAYMENT
    // =====================================

    public string PaymentStatus { get; set; } = "Pending";

    public DateTime BillDate { get; set; } = DateTime.UtcNow;

    // =====================================
    // BILL ITEMS
    // =====================================

    public ICollection<PharmacyBillItem> BillItems { get; set; }
        = new List<PharmacyBillItem>();
}
