namespace AuthDemo.Models;

public class PharmacyNotification
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // All / Hospital / Branch / User
    public string RecipientType { get; set; } = "All";


    public int? BranchId { get; set; }

    public int? UserId { get; set; }

    public bool IsRead { get; set; }

    public bool IsSent { get; set; } = true;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}