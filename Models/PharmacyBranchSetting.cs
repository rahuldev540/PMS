namespace AuthDemo.Models;

public class PharmacyBranchSetting
{
    public int Id { get; set; }
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }

    public string PharmacyName { get; set; } = "Pharmacy Console";
    public string? LicenseNumber { get; set; }
    public string? GSTNumber { get; set; }

    public string Currency { get; set; } = "INR";
    public decimal TaxGSTRate { get; set; } = 18m;
    public string InvoicePrefix { get; set; } = "PHAR-";
    public string PaymentTerms { get; set; } = "Due on Receipt";
    public string BusinessHours { get; set; } = "9:00 AM - 10:00 PM";

    public int LowStockThreshold { get; set; } = 10;
    public int ExpiryAlertHorizonDays { get; set; } = 90;
    public bool StockManagementEnabled { get; set; } = true;
    public bool BatchExpiryTrackingEnabled { get; set; } = true;

    public decimal MaximumAllowedDiscount { get; set; } = 10m;
    public string SalesInvoiceDetails { get; set; } = "Show Tax Details";
    public int ReturnWindowDays { get; set; } = 7;
    public bool AutomaticReorderingEnabled { get; set; }

    public bool LowStockNotificationsEnabled { get; set; } = true;
    public bool ExpiryWarningAlertsEnabled { get; set; } = true;
    public bool OrderTransferWarningsEnabled { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
}
