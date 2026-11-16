using AuthDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Helpers;

/// <summary>Maps stored branch foreign keys to pharmacy-scoped API branch IDs.</summary>
public static class PharmacyPublicBranchMapper
{
    public static async Task<int> GetPublicBranchIdAsync(
        AppDbContext db, int pharmacyId, int internalBranchId,
        CancellationToken cancellationToken = default)
    {
        var publicId = await db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId && x.ExistingBranchId == internalBranchId)
            .Select(x => (int?)x.BranchId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!publicId.HasValue)
            throw new InvalidOperationException($"Missing branch identity for pharmacy {pharmacyId} and internal branch {internalBranchId}.");
        return publicId.Value;
    }
}
