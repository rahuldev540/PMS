using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

/// <summary>
/// Collection reporting from the existing PharmacyPayments ledger; no second payment ledger.
/// Scope is determined from authenticated assignments, never from client-supplied PharmacyId.
/// </summary>
[ApiController]
[Route("api/payments/reports")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public sealed class PharmacyPaymentReportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyWorkflowAccess _access;
    public PharmacyPaymentReportsController(AppDbContext db, PharmacyWorkflowAccess access)
    { _db = db; _access = access; }

    [HttpGet("branch/{branchId:int}")]
    public async Task<IActionResult> Branch(int branchId)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Reports", "View")) return Forbid();
        var stats = await _db.PharmacyPayments.AsNoTracking()
            .Where(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch)
            .GroupBy(x => x.BranchId)
            .Select(g => new { count = g.Count(), collected = g.Sum(x => x.PaidAmount), refunded = g.Sum(x => x.RefundedAmount) })
            .FirstOrDefaultAsync();
        return Ok(new { data = new { branchId, currency = "INR", transactionCount = stats?.count ?? 0,
            grossCollected = stats?.collected ?? 0m, refunded = stats?.refunded ?? 0m,
            netCollected = (stats?.collected ?? 0m) - (stats?.refunded ?? 0m) } });
    }

    [HttpGet("pharmacy")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> Pharmacy()
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Forbid();
        var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == userId && x.Role == "PharmacyAdmin" && x.IsActive && x.PharmacyId.HasValue && x.AccountStatus == "Active");
        if (actor?.PharmacyId is not int pharmacyId) return Forbid();
        if (!await _access.CanAsync(actor.Id, "Reports", "View")) return Forbid();

        var branchRows = await (from b in _db.Branches.AsNoTracking()
            join identity in _db.PharmacyBranchIdentities.AsNoTracking() on b.Id equals identity.ExistingBranchId
            where b.PharmacyId == pharmacyId && identity.PharmacyId == pharmacyId
            orderby identity.BranchId
            select new { InternalId = b.Id, PublicId = identity.BranchId, b.Name }).ToListAsync();
        var aggregates = await _db.PharmacyPayments.AsNoTracking().Where(x => x.PharmacyId == pharmacyId)
            .GroupBy(x => x.BranchId)
            .Select(g => new { InternalId = g.Key, Count = g.Count(), Gross = g.Sum(x => x.PaidAmount), Refunded = g.Sum(x => x.RefundedAmount) })
            .ToListAsync();
        var items = branchRows.Select(b => {
            var a = aggregates.FirstOrDefault(x => x.InternalId == b.InternalId);
            return new { branchId = b.PublicId, branchName = b.Name, transactionCount = a?.Count ?? 0,
                grossCollected = a?.Gross ?? 0m, refunded = a?.Refunded ?? 0m,
                netCollected = (a?.Gross ?? 0m) - (a?.Refunded ?? 0m) };
        }).ToList();
        // Pharmacy totals must not silently omit legacy payments from a branch whose
        // public identity mapping has not yet been backfilled.
        var gross = aggregates.Sum(x => x.Gross);
        var refunds = aggregates.Sum(x => x.Refunded);
        var unmapped = aggregates.Count(a => !branchRows.Any(b => b.InternalId == a.InternalId));
        return Ok(new { data = new { currency = "INR",
            transactionCount = aggregates.Sum(x => x.Count),
            grossCollected = gross, refunded = refunds, netCollected = gross - refunds,
            branchesWithoutPublicMapping = unmapped, items,
            note = "Collections from recorded PharmacyPayments, not merchant settlements or unpaid bill value. Resolve public branch mappings if branchesWithoutPublicMapping > 0." } });
    }
}
