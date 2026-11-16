namespace AuthDemo.Models;

/// <summary>Pharmacy-scoped database branch identity. Existing Branch.Id remains
/// the legacy FK for CMS and existing transactional tables.</summary>
public class PharmacyBranchIdentity
{
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
    public int ExistingBranchId { get; set; }
    public Branch Branch { get; set; } = null!;
}
