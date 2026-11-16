using System.Text.Json.Serialization;

namespace AuthDemo.Models;

public class Branch
{
    public int Id { get; set; }



    // =====================================================
    // STANDALONE PHARMACY RELATIONSHIP
    //
    // Standalone PMS branches belong directly to Pharmacy.
    // Example:
    //
    // Pharmacy
    //   ├── Branch 1
    //   ├── Branch 2
    //   └── Branch 3
    // =====================================================

    public int? PharmacyId { get; set; }

    [JsonIgnore]
    public PharmacyTenant? Pharmacy { get; set; }


    // =====================================================
    // LEGACY DATABASE COMPATIBILITY
    //
    //
    // This nullable column remains mapped until a coordinated EF migration.
    // =====================================================

// =====================================================
    // BRANCH DETAILS
    // =====================================================

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? District { get; set; }

    public string? Country { get; set; }

    public string? PostalCode { get; set; }


    // =====================================================
    // STATUS
    // =====================================================

    public bool IsActive { get; set; } = true;

    // Exactly one standalone branch per pharmacy should be the main branch.
    public bool IsMainBranch { get; set; } = false;


    // =====================================================
    // EXTERNAL / CMS BRANCH REFERENCE
    // =====================================================

    public int? ExternalBranchId { get; set; }

    public string? SourceSystem { get; set; }


    // =====================================================
    // CREATED DATE
    // =====================================================

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}