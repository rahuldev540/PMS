namespace AuthDemo.Models;

public class UserPermission
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Module { get; set; } = string.Empty;

    // Default permissions = TRUE
    public bool CanView { get; set; } = true;
    public bool CanCreate { get; set; } = true;
    public bool CanEdit { get; set; } = true;
    public bool CanDelete { get; set; } = true;

    public int AssignedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}