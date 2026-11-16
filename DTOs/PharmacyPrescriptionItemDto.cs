namespace AuthDemo.DTOs;

public class PharmacyPrescriptionItemDto
{
    public int PrescriptionItemId { get; set; }

    public int MedicineId { get; set; }

    public string MedicineName { get; set; } = string.Empty;

    public string Dosage { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    // Medicine Timing
    public bool Morning { get; set; }

    public bool Afternoon { get; set; }

    public bool Night { get; set; }

    // Duration
    public int Days { get; set; }

    // Prescribed Quantity
    public int Quantity { get; set; }

    // Current Available Stock
    public int AvailableStock { get; set; }

    // Dispense Status
    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }
}