namespace AuthDemo.DTOs;

public class PharmacyPrescriptionDto
{
    // =====================================================
    // PRESCRIPTION
    // =====================================================

    public int PrescriptionId { get; set; }

    public DateTime PrescriptionDate { get; set; }

    public string Status { get; set; } = string.Empty;

    // =====================================================
    // PATIENT
    // =====================================================

    public string PatientName { get; set; } = string.Empty;

    public string PatientMobile { get; set; } = string.Empty;

    // =====================================================
    // DOCTOR
    // =====================================================

    public string DoctorName { get; set; } = string.Empty;

    // =====================================================
    // DIAGNOSIS
    // =====================================================

    public string? Diagnosis { get; set; }

    public string? Instructions { get; set; }

    // =====================================================
    // MEDICINES
    // =====================================================

    public List<PharmacyPrescriptionItemDto> Medicines
    { get; set; } = new();
}