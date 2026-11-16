namespace AuthDemo.Models;

public class PharmacyPharmacistIdentity
{
    public int PharmacyId { get; set; }
    public int PharmacistId { get; set; }
    public int ExistingUserId { get; set; }
    public User User { get; set; } = null!;
}
