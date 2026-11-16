namespace AuthDemo.Models;

public class Patient
{
    public int Id { get; set; }

    // Display number, unique within PharmacyId; Id remains the global FK.
    public int? PharmacyPatientId { get; set; }

    // =====================================================
    // PATIENT CODE
    // =====================================================

    public string PatientCode
    { get; set; }
    public int? ExternalPatientId { get; set; }

    public string? SourceSystem { get; set; }

    // =====================================================
    // BASIC DETAILS
    // =====================================================

    public string Name
    { get; set; }

    public string Phone
    { get; set; }

    public int Age
    { get; set; }

    public string Gender
    { get; set; }

    public string? Email
    { get; set; }

    public string? Address
    { get; set; }

    // =====================================================
    // EXTRA DETAILS
    // =====================================================

    public string? BloodGroup
    { get; set; }

    public DateTime? DateOfBirth
    { get; set; }

    public string? EmergencyContactName
    { get; set; }

    public string? EmergencyContactPhone
    { get; set; }

    // =====================================================
    // MULTI HOSPITAL SUPPORT
    // =====================================================

    public int? PharmacyId { get; set; }
    public int? BranchId { get; set; }

// =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt
    { get; set; }
        = DateTime.UtcNow;

    // =====================================================
    // APPOINTMENTS
    // =====================================================

    public ICollection<Appointment>
        Appointments
    { get; set; }
        = new List<Appointment>();

}
