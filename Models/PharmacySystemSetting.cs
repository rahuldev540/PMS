namespace AuthDemo.Models;

public class PharmacySystemSetting
{
    public int Id { get; set; }

    public string AppName { get; set; } = "PMS";

    public string Timezone { get; set; } = "Asia/Kolkata";

    public string Currency { get; set; } = "INR";

    public bool IsEnabled { get; set; } = true;

    public string? ConfigurationNotes { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? UpdatedByUserId { get; set; }
}