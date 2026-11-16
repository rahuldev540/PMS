namespace AuthDemo.Models;

public class PharmacyPermissionRole
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PharmacyRolePermission> Permissions { get; set; }
        = new List<PharmacyRolePermission>();
}

public class PharmacyRolePermission
{
    public int Id { get; set; }

    public int PharmacyPermissionRoleId { get; set; }

    public PharmacyPermissionRole PharmacyPermissionRole { get; set; } = null!;

    public string Module { get; set; } = string.Empty;

    public bool CanView { get; set; }

    public bool CanCreate { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }
}