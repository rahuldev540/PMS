namespace AuthDemo.Models;

// All new entities are additive: existing prescriptions, bills and payments are untouched.
public sealed class PharmacyPrescriptionDraft
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; } // Internal FK; public branch number is resolved at API boundary.
    public int CreatedByUserId { get; set; }
    public int? PublicPatientId { get; set; }
    public string PatientName { get; set; } = "";
    public string PatientPhone { get; set; } = "";
    public int PatientAge { get; set; }
    public string PatientGender { get; set; } = "Unknown";
    public string DoctorName { get; set; } = "";
    public DateTime PrescriptionDate { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public int? SubmittedPrescriptionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<PharmacyPrescriptionDraftLine> Lines { get; set; } = new();
}
public sealed class PharmacyPrescriptionDraftLine
{
    public int Id { get; set; }
    public int DraftId { get; set; }
    public PharmacyPrescriptionDraft Draft { get; set; } = null!;
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = "";
    public string Strength { get; set; } = "";
    public int MorningUnits { get; set; }
    public int AfternoonUnits { get; set; }
    public int EveningUnits { get; set; }
    public int DurationDays { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string Instructions { get; set; } = "";
}
public sealed class PharmacyPrescriptionDoseDetail
{
    public int Id { get; set; }
    public int PrescriptionItemId { get; set; }
    public int MorningUnits { get; set; }
    public int AfternoonUnits { get; set; }
    public int EveningUnits { get; set; }
    public int DurationDays { get; set; }
    public decimal UnitPriceAtPrescription { get; set; }
}
public sealed class PharmacyUploadedEvidence
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int UploadedByUserId { get; set; }
    public string EvidenceType { get; set; } = ""; // PrescriptionPhoto or PaymentReceipt
    public int? DraftId { get; set; }
    public int? PrescriptionId { get; set; }
    public int? BillId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string Sha256 { get; set; } = "";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
public sealed class BranchGatewaySettings
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public string MerchantName { get; set; } = "";
    public string Environment { get; set; } = "Test";
    public string KeyId { get; set; } = "";
    public string EncryptedKeySecret { get; set; } = "";
    public string EncryptedWebhookSecret { get; set; } = "";
    public string WebhookRoutingKey { get; set; } = Guid.NewGuid().ToString("N");
    public bool CashEnabled { get; set; } = true;
    public bool UpiEnabled { get; set; } = true;
    public bool CardEnabled { get; set; } = true;
    public bool BankEnabled { get; set; } = true;
    public bool PartialPaymentsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PharmacyGatewayAttempt
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int BillId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? GatewaySettingsId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayQrId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PharmacyGatewayWebhookEvent
{
    public int Id { get; set; }
    public int GatewaySettingsId { get; set; }
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PharmacyBankTransferRequest
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int BillId { get; set; }
    public decimal Amount { get; set; }
    public string Utr { get; set; } = "";
    public string BankName { get; set; } = "";
    public string Status { get; set; } = "PendingVerification";
    public int SubmittedByUserId { get; set; }
    public int? VerifiedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
}
