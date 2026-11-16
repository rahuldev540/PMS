namespace AuthDemo.Models;

/// <summary>
/// Represents an independent pharmacy tenant.
///
/// Standalone PMS architecture:
/// PharmacyTenant
///     ├── Branch 1
///     ├── Branch 2
///     └── Branch N
///
/// Standalone pharmacy operations do not require a Hospital.
///
/// </summary>
public class PharmacyTenant
{
    // =====================================================
    // PRIMARY KEY
    // =====================================================

    public int Id { get; set; }


    // =====================================================
    // PHARMACY DETAILS
    // =====================================================

    public string Name { get; set; } = "Pharmacy Console";

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? District { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }


    // =====================================================
    // BRANCHES
    //
    // Standalone branches belong directly to PharmacyTenant
    // using Branch.PharmacyId.
    // =====================================================

    public ICollection<Branch> Branches { get; set; } =
        new List<Branch>();


    // =====================================================
    // AUDIT FIELDS
    // =====================================================

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } =
        DateTime.UtcNow;
}