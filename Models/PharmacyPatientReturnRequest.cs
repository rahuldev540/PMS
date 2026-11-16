namespace AuthDemo.Models;

/// <summary>
/// Additive header for a patient return containing one or more existing PharmacyReturn rows.
/// Historical PharmacyReturns are unchanged and remain accessible using their original APIs.
/// </summary>
public sealed class PharmacyPatientReturnRequest
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int PharmacyBillId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool VerifiedForRestocking { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<PharmacyPatientReturnRequestLine> Lines { get; set; } = new List<PharmacyPatientReturnRequestLine>();
}

public sealed class PharmacyPatientReturnRequestLine
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public PharmacyPatientReturnRequest Request { get; set; } = null!;
    public int PharmacyReturnId { get; set; }
    public PharmacyReturn PharmacyReturn { get; set; } = null!;
    public string OriginalBatchNumber { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
}
