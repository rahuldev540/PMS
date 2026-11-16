using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.Controllers;

public sealed class BranchPaymentSettingsDto
{
    [Required, MaxLength(200)] public string MerchantName { get; set; } = "";
    [Required] public string Environment { get; set; } = "Test";
    [MaxLength(120)] public string? RazorpayKeyId { get; set; }
    public string? RazorpayKeySecret { get; set; }
    public string? RazorpayWebhookSecret { get; set; }
    public bool Cash { get; set; } = true;
    public bool Upi { get; set; } = true;
    public bool Card { get; set; } = true;
    public bool BankTransfer { get; set; } = true;
    public bool AllowPartialPayments { get; set; } = true;
}

[ApiController]
[Route("api/payment-settings/branches")]
[Authorize(Roles = "PharmacyAdmin")]
public sealed class BranchPaymentSettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyWorkflowAccess _access;
    private readonly PmsRazorpaySupport _gateway;
    public BranchPaymentSettingsController(AppDbContext db, PharmacyWorkflowAccess access, PmsRazorpaySupport gateway)
    { _db = db; _access = access; _gateway = gateway; }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid)) return Forbid();
        var admin = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid && x.IsActive && x.Role == "PharmacyAdmin");
        if (admin?.PharmacyId is not int pharmacy) return Forbid();
        if (!await _access.CanAsync(uid, "Settings", "View")) return Forbid();
        var branches = await _db.Branches.AsNoTracking().Where(x => x.PharmacyId == pharmacy && x.IsActive)
            .Join(_db.PharmacyBranchIdentities, b => b.Id, i => i.ExistingBranchId, (b,i) => new { b, i })
            .Where(x => x.i.PharmacyId == pharmacy).Select(x => new { x.i.BranchId, x.b.Name, x.b.Id }).ToListAsync();
        var settings = await _db.BranchGatewaySettings.AsNoTracking().Where(x => x.PharmacyId == pharmacy).ToListAsync();
        return Ok(new { data = new { items = branches.Select(x => new { branchId = x.BranchId, branchName = x.Name,
            configured = settings.Any(s => s.BranchId == x.Id), merchantName = settings.FirstOrDefault(s => s.BranchId == x.Id)?.MerchantName }).ToList() } });
    }

    [HttpGet("{branchId:int}")]
    public async Task<IActionResult> Get(int branchId)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Settings", "View")) return Forbid();
        var s = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch);
        if (s == null) return Ok(new { data = new { branchId, configured = false } });
        return Ok(new { data = new { branchId, configured = true, s.MerchantName, s.Environment, razorpayKeyId = s.KeyId,
            credentialsConfigured = !string.IsNullOrEmpty(s.EncryptedKeySecret), webhookConfigured = !string.IsNullOrEmpty(s.EncryptedWebhookSecret),
            methods = new { cash = s.CashEnabled, upi = s.UpiEnabled, card = s.CardEnabled, bankTransfer = s.BankEnabled },
            allowPartialPayments = s.PartialPaymentsEnabled, s.UpdatedAt } });
    }

    [HttpPut("{branchId:int}")]
    public async Task<IActionResult> Put(int branchId, [FromBody] BranchPaymentSettingsDto dto)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Settings", "Edit")) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.MerchantName) || dto.Environment is not ("Test" or "Live"))
            return BadRequest(new { message = "MerchantName and environment (Test or Live) are required." });
        var s = await _db.BranchGatewaySettings.FirstOrDefaultAsync(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch);
        var newSettings = s == null;
        s ??= new BranchGatewaySettings { PharmacyId = a.Pharmacy, BranchId = a.Branch };
        var keyChanged = !string.IsNullOrWhiteSpace(dto.RazorpayKeySecret) ||
            !string.IsNullOrWhiteSpace(dto.RazorpayKeyId) && dto.RazorpayKeyId != s.KeyId ||
            !string.Equals(dto.Environment, s.Environment, StringComparison.Ordinal);
        if (keyChanged && !newSettings && await _db.PharmacyGatewayAttempts.AnyAsync(x => x.GatewaySettingsId == s.Id && x.Status == "Pending"))
            return Conflict(new { message = "Resolve pending gateway attempts before rotating credentials." });
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.RazorpayKeySecret)) s.EncryptedKeySecret = _gateway.Encrypt(dto.RazorpayKeySecret.Trim());
            if (!string.IsNullOrWhiteSpace(dto.RazorpayWebhookSecret)) s.EncryptedWebhookSecret = _gateway.Encrypt(dto.RazorpayWebhookSecret.Trim());
        }
        catch (InvalidOperationException) { return StatusCode(503, new { message = "Gateway encryption key is not configured on the server." }); }
        if (!string.IsNullOrWhiteSpace(dto.RazorpayKeyId)) s.KeyId = dto.RazorpayKeyId.Trim();
        if ((dto.Upi || dto.Card) && (string.IsNullOrWhiteSpace(s.KeyId) || string.IsNullOrWhiteSpace(s.EncryptedKeySecret)))
            return BadRequest(new { message = "Razorpay credentials are required to enable UPI or card." });
        if ((dto.Upi || dto.Card) &&
            (dto.Environment == "Test" && !s.KeyId.StartsWith("rzp_test_", StringComparison.Ordinal) ||
             dto.Environment == "Live" && !s.KeyId.StartsWith("rzp_live_", StringComparison.Ordinal)))
            return BadRequest(new { message = "Razorpay Key ID must match the selected Test or Live environment." });
        s.MerchantName = dto.MerchantName.Trim(); s.Environment = dto.Environment;
        s.CashEnabled = dto.Cash; s.UpiEnabled = dto.Upi; s.CardEnabled = dto.Card; s.BankEnabled = dto.BankTransfer;
        s.PartialPaymentsEnabled = dto.AllowPartialPayments; s.UpdatedAt = DateTime.UtcNow;
        if (newSettings) _db.BranchGatewaySettings.Add(s);
        await _db.SaveChangesAsync();
        return Ok(new { data = new { branchId, configured = true, s.MerchantName, s.Environment,
            webhookRoute = $"/api/payments/gateway/webhook/{s.WebhookRoutingKey}",
            note = "Keep the webhook secret private. Configure this route in the matching Razorpay merchant account." } });
    }
}
