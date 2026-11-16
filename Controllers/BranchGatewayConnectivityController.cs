using AuthDemo.Data;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

/// <summary>
/// Non-destructive merchant connectivity checks. Branch IDs in routes are public, pharmacy-scoped IDs.
/// Does not create an order, collect money or store a test transaction.
/// </summary>
[ApiController]
[Route("api/payment-settings/branches")]
[Authorize(Roles = "PharmacyAdmin")]
public sealed class BranchGatewayConnectivityController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyWorkflowAccess _access;
    private readonly PmsRazorpaySupport _razorpay;

    public BranchGatewayConnectivityController(AppDbContext db, PharmacyWorkflowAccess access, PmsRazorpaySupport razorpay)
    { _db = db; _access = access; _razorpay = razorpay; }

    [HttpGet("{branchId:int}/status")]
    public async Task<IActionResult> Status(int branchId)
    {
        var scope = await _access.ResolveAsync(User, branchId);
        if (scope.Error != null) return StatusCode(403, new { message = scope.Error });
        if (!await _access.CanAsync(scope.User, "Settings", "View")) return Forbid();

        var s = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x =>
            x.PharmacyId == scope.Pharmacy && x.BranchId == scope.Branch);
        if (s == null) return Ok(new { data = new { branchId, configured = false, credentialsReadyForTest = false } });
        var credentialConfigured = !string.IsNullOrWhiteSpace(s.KeyId) && !string.IsNullOrWhiteSpace(s.EncryptedKeySecret);
        var envMatches = s.Environment switch
        {
            "Test" => s.KeyId.StartsWith("rzp_test_", StringComparison.Ordinal),
            "Live" => s.KeyId.StartsWith("rzp_live_", StringComparison.Ordinal),
            _ => false
        };
        return Ok(new { data = new {
            branchId, configured = true, provider = "Razorpay", s.MerchantName, s.Environment,
            credentialsReadyForTest = credentialConfigured && envMatches && (s.UpiEnabled || s.CardEnabled),
            credentialConfigured, credentialEnvironmentMatches = envMatches,
            webhookConfigured = !string.IsNullOrWhiteSpace(s.EncryptedWebhookSecret),
            webhookPath = $"/api/payments/gateway/webhook/{s.WebhookRoutingKey}",
            methods = new { cash = s.CashEnabled, upi = s.UpiEnabled, card = s.CardEnabled, bankTransfer = s.BankEnabled },
            allowPartialPayments = s.PartialPaymentsEnabled,
            note = "Connectivity and merchant activation must be verified separately; this endpoint never reveals secrets." } });
    }

    [HttpPost("{branchId:int}/verify")]
    public async Task<IActionResult> Verify(int branchId)
    {
        var scope = await _access.ResolveAsync(User, branchId);
        if (scope.Error != null) return StatusCode(403, new { message = scope.Error });
        if (!await _access.CanAsync(scope.User, "Settings", "Edit")) return Forbid();
        var s = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x =>
            x.PharmacyId == scope.Pharmacy && x.BranchId == scope.Branch);
        if (s == null || string.IsNullOrWhiteSpace(s.KeyId) || string.IsNullOrWhiteSpace(s.EncryptedKeySecret))
            return Conflict(new { message = "Configure Razorpay Key ID and secret for this branch first." });
        if (s.Environment is not ("Test" or "Live") ||
            s.Environment == "Test" && !s.KeyId.StartsWith("rzp_test_", StringComparison.Ordinal) ||
            s.Environment == "Live" && !s.KeyId.StartsWith("rzp_live_", StringComparison.Ordinal))
            return Conflict(new { message = "Branch environment and Razorpay Key ID prefix do not match." });
        try
        {
            var client = _razorpay.Client(s);
            using var response = await client.GetAsync("https://api.razorpay.com/v1/orders?count=1");
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return StatusCode(502, new { message = "Razorpay rejected these merchant credentials. Confirm the account and Test/Live mode." });
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { message = "Could not verify Razorpay merchant access. Check provider status and permissions." });
            return Ok(new { data = new { branchId, provider = "Razorpay", environment = s.Environment,
                credentialsValid = true, apiAccessVerified = true,
                note = "Read-only API authentication succeeded. QR, checkout, webhooks and settlement activation must be tested separately." } });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException or System.Security.Cryptography.CryptographicException)
        { return StatusCode(503, new { message = "Merchant connectivity could not be verified. Check credentials, encryption key and network." }); }
    }
}
