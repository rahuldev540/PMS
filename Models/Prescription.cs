namespace AuthDemo.Models;

public class Prescription
{
    public const string StatusDraft = "Draft";
    public const string StatusPending = "Pending";
    public const string StatusPartiallyDispensed = "PartiallyDispensed";
    public const string StatusDispensed = "Dispensed";
    public const string StatusCancelled = "Cancelled";

    public int Id { get; set; }

    // ==============================
    // Pharmacy / Branch Scope
    // ==============================

    // Standalone pharmacy ownership.
    public int PharmacyId { get; set; }

    public int BranchId { get; set; }

    // Optional CMS/legacy hospital reference.
    // Standalone pharmacy prescriptions do not require HospitalId.

public int AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

   
    public DateTime PrescriptionDate { get; set; }
        = DateTime.UtcNow;

    public string Diagnosis { get; set; } = string.Empty;
    public string Advice { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string? Instructions { get; set; }

    public DateTime? FollowUpDate { get; set; }
    public int? ExternalPrescriptionId { get; set; }

    public string? SourceSystem { get; set; }

    public string? RequestId { get; set; }

    // Optional validity window. Null preserves legacy prescriptions.
    public DateTime? ValidUntil { get; set; }
    public DateTime? ReservationExpiresAt { get; set; }

    // Draft, Pending, PartiallyDispensed, Dispensed, Cancelled
    public string Status { get; set; } = "Pending";

    public bool IsPrinted { get; set; }
    public DateTime? PrintedAt { get; set; }
    public int? PrintedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;

    public ICollection<PrescriptionItem> Medicines { get; set; }
        = new List<PrescriptionItem>();
}
