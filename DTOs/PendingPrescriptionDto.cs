namespace AuthDemo.DTOs;

public class PendingPrescriptionDto
{
    public int PrescriptionId { get; set; }

    public int AppointmentId { get; set; }

    public int PatientId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string PatientMobile { get; set; } = string.Empty;

    public string DoctorName { get; set; } = string.Empty;

    public DateTime PrescriptionDate { get; set; }

    public int TotalMedicines { get; set; }

    public string Status { get; set; } = string.Empty;
}