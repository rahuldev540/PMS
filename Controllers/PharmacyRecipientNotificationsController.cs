using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

// Additive notification read endpoints. Existing Super Admin endpoints and schema are untouched.
// Recipient visibility is evaluated against the authenticated user's internal IDs.
public abstract class PharmacyRecipientNotificationsBase : ControllerBase
{
    protected readonly AppDbContext Db;
    protected PharmacyRecipientNotificationsBase(AppDbContext db) => Db = db;

    protected async Task<(User? account, IActionResult? error)> GetAccountAsync()
    {
        var rawId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(rawId, out var id) || id <= 0)
            return (null, Unauthorized(new { success = false, message = "Invalid session." }));

        var account = await Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (account == null || !account.IsActive || !account.PharmacyId.HasValue)
            return (null, StatusCode(403, new { success = false, message = "Active pharmacy assignment required." }));
        return (account, null);
    }

    protected IQueryable<PharmacyNotification> VisibleTo(User account)
    {
        var pharmacyId = account.PharmacyId!.Value;
        var isPharmacist = string.Equals(account.Role, "Pharmacist", StringComparison.OrdinalIgnoreCase);
        var branchId = account.BranchId;

        // Branch and user targets must be in this user's pharmacy. A pharmacist can
        // only see notifications for their own branch or their own user account.
        return Db.PharmacyNotifications.Where(n =>
            n.RecipientType == "All" ||
            (n.RecipientType == "User" && n.UserId == account.Id) ||
            (n.RecipientType == "Branch" && n.BranchId.HasValue &&
                Db.Branches.Any(b => b.Id == n.BranchId.Value && b.PharmacyId == pharmacyId &&
                    (!isPharmacist || (branchId.HasValue && b.Id == branchId.Value)))));
    }

    protected async Task<IActionResult> ListAsync(string status, int page, int pageSize)
    {
        var (account, error) = await GetAccountAsync();
        if (error != null) return error;
        if (account!.Role == "Pharmacist" && !account.BranchId.HasValue)
            return StatusCode(403, new { success = false, message = "Branch assignment required." });
        status = (status ?? "all").Trim().ToLowerInvariant();
        if (status != "all" && status != "read" && status != "unread" && status != "sent")
            return BadRequest(new { success = false, message = "Status must be all, read, unread or sent." });
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = VisibleTo(account).AsNoTracking();
        query = status switch
        {
            "read" => query.Where(n => n.IsRead),
            "unread" => query.Where(n => !n.IsRead),
            "sent" => query.Where(n => n.IsSent),
            _ => query
        };
        var total = await query.CountAsync();
        var notifications = await query.OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new { n.Id, n.Title, n.Message, n.RecipientType, n.IsSent,
                n.IsRead, n.CreatedAt, n.ReadAt }).ToListAsync();
        return Ok(new { success = true, total, page, pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize), notifications });
    }

    protected async Task<IActionResult> ReadAsync(int id)
    {
        var (account, error) = await GetAccountAsync();
        if (error != null) return error;
        if (account!.Role == "Pharmacist" && !account.BranchId.HasValue)
            return StatusCode(403, new { success = false, message = "Branch assignment required." });
        var item = await VisibleTo(account).FirstOrDefaultAsync(n => n.Id == id);
        if (item == null) return NotFound(new { success = false, message = "Notification not found." });
        if (!item.IsRead)
        {
            item.IsRead = true;
            item.ReadAt = DateTime.UtcNow;
            await Db.SaveChangesAsync();
        }
        return Ok(new { success = true, notification = new { item.Id, item.IsRead, item.ReadAt } });
    }

    protected async Task<IActionResult> UnreadCountAsync()
    {
        var (account, error) = await GetAccountAsync();
        if (error != null) return error;
        if (account!.Role == "Pharmacist" && !account.BranchId.HasValue)
            return StatusCode(403, new { success = false, message = "Branch assignment required." });
        var count = await VisibleTo(account).CountAsync(n => !n.IsRead);
        return Ok(new { success = true, unreadCount = count });
    }

    protected async Task<IActionResult> MarkAllAsync()
    {
        var (account, error) = await GetAccountAsync();
        if (error != null) return error;
        if (account!.Role == "Pharmacist" && !account.BranchId.HasValue)
            return StatusCode(403, new { success = false, message = "Branch assignment required." });
        var items = await VisibleTo(account).Where(n => !n.IsRead).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var item in items) { item.IsRead = true; item.ReadAt = now; }
        if (items.Count > 0) await Db.SaveChangesAsync();
        return Ok(new { success = true, updatedCount = items.Count });
    }
}

[ApiController]
[Authorize(Roles = "PharmacyAdmin")]
[Route("api/pharmacy-admin/notifications")]
public sealed class PharmacyAdminRecipientNotificationsController : PharmacyRecipientNotificationsBase
{
    public PharmacyAdminRecipientNotificationsController(AppDbContext db) : base(db) { }

    [HttpGet]
    public Task<IActionResult> Get([FromQuery] string status = "all", [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) => ListAsync(status, page, pageSize);

    [HttpPatch("{id:int}/read")]
    public Task<IActionResult> Read(int id) => ReadAsync(id);
}

[ApiController]
[Authorize(Roles = "Pharmacist")]
[Route("api/pharmacist/notifications")]
public sealed class PharmacistRecipientNotificationsController : PharmacyRecipientNotificationsBase
{
    public PharmacistRecipientNotificationsController(AppDbContext db) : base(db) { }

    [HttpGet]
    public Task<IActionResult> Get([FromQuery] string status = "all", [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) => ListAsync(status, page, pageSize);

    [HttpPatch("{id:int}/read")]
    public Task<IActionResult> Read(int id) => ReadAsync(id);
}

[ApiController]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
[Route("api/notifications")]
public sealed class PharmacySharedRecipientNotificationsController : PharmacyRecipientNotificationsBase
{
    public PharmacySharedRecipientNotificationsController(AppDbContext db) : base(db) { }

    [HttpGet("unread-count")]
    public Task<IActionResult> UnreadCount() => UnreadCountAsync();

    [HttpPatch("mark-all-read")]
    public Task<IActionResult> MarkAllRead() => MarkAllAsync();
}
