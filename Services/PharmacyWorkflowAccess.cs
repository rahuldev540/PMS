using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

// Centralized tenant, branch and permission checks for newly added endpoints.
public sealed class PharmacyWorkflowAccess
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissions;
    public PharmacyWorkflowAccess(AppDbContext db, PharmacyPermissionChecker permissions)
    { _db = db; _permissions = permissions; }

    public async Task<(int Pharmacy, int Branch, int User, string? Error)> ResolveAsync(
        ClaimsPrincipal actor, int? publicBranchId, int? resourceInternalBranchId = null)
    {
        if (!int.TryParse(actor.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? actor.FindFirst("userId")?.Value, out var uid))
            return (0, 0, 0, "Authentication required.");
        var user = await _db.Users.AsNoTracking().Where(x => x.Id == uid && x.IsActive)
            .Select(x => new { x.Id, x.PharmacyId, x.BranchId, x.Role, x.AccountStatus }).FirstOrDefaultAsync();
        if (user?.PharmacyId is not int pharmacy || pharmacy <= 0 ||
            !string.Equals(user.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return (0, 0, uid, "Active pharmacy assignment required.");
        if (!actor.IsInRole(user.Role ?? "")) return (0, 0, uid, "Invalid role.");
        int branch;
        if (user.Role == "Pharmacist")
        {
            if (user.BranchId is not int assigned || assigned <= 0)
                return (0, 0, uid, "Branch assignment required.");
            branch = assigned;
        }
        else if (user.Role == "PharmacyAdmin")
        {
            if (resourceInternalBranchId.HasValue) branch = resourceInternalBranchId.Value;
            else if (publicBranchId is > 0)
            {
                branch = await _db.PharmacyBranchIdentities.AsNoTracking()
                    .Where(x => x.PharmacyId == pharmacy && x.BranchId == publicBranchId.Value)
                    .Select(x => x.ExistingBranchId).FirstOrDefaultAsync();
            }
            else return (0, 0, uid, "Select a public branchId.");
        }
        else return (0, 0, uid, "Role is not permitted.");
        if (resourceInternalBranchId.HasValue && branch != resourceInternalBranchId.Value)
            return (0, 0, uid, "This resource belongs to another branch.");
        if (!await _db.Branches.AsNoTracking().AnyAsync(x => x.Id == branch && x.PharmacyId == pharmacy && x.IsActive))
            return (0, 0, uid, "Branch is not active in your pharmacy.");
        if (publicBranchId.HasValue && user.Role == "Pharmacist")
        {
            var publicId = await _db.PharmacyBranchIdentities.AsNoTracking()
                .Where(x => x.PharmacyId == pharmacy && x.ExistingBranchId == branch)
                .Select(x => (int?)x.BranchId).FirstOrDefaultAsync();
            if (publicId != publicBranchId) return (0, 0, uid, "Branch mismatch.");
        }
        return (pharmacy, branch, uid, null);
    }

    public Task<bool> CanAsync(int userId, string module, string action) =>
        _permissions.HasPermissionAsync(userId, module, action);

    public Task<int?> PublicBranchAsync(int pharmacyId, int internalBranchId) =>
        _db.PharmacyBranchIdentities.AsNoTracking().Where(x => x.PharmacyId == pharmacyId && x.ExistingBranchId == internalBranchId)
            .Select(x => (int?)x.BranchId).FirstOrDefaultAsync();
}
