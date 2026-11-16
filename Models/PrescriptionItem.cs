namespace AuthDemo.Models;

public class PrescriptionItem
{
    public int Id { get; set; }

    // Scope is inherited through Prescription:
    // Pharmacy -> Branch -> Prescription -> PrescriptionItem.
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    // Optional snapshot of the medicine name at prescription time
    public string? MedicineName { get; set; }

    public string Dosage { get; set; } = string.Empty;
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string Instructions { get; set; } = string.Empty;

    public bool Morning { get; set; }
    public bool Afternoon { get; set; }
    public bool Night { get; set; }

    public int Days { get; set; }
    public int Quantity { get; set; }

    // Cumulative quantity already dispensed. Enables safe partial dispensing.
    public int DispensedQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    // Pending, PartiallyDispensed, Dispensed, Cancelled
    public string Status { get; set; } = "Pending";

    public string? Notes { get; set; }
}
