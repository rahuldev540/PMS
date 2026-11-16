using System.Data;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

public sealed class PmsGatewayOrderDto
{
    public int BillId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "upi";
    public string IdempotencyKey { get; set; } = "";
}
public sealed class PmsGatewayVerifyDto
{
    public int PaymentAttemptId { get; set; }
    public string RazorpayOrderId { get; set; } = "";
    public string RazorpayPaymentId { get; set; } = "";
    public string RazorpaySignature { get; set; } = "";
}
public sealed class PmsCashDto
{
    public int BillId { get; set; }
    public decimal Amount { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string? Notes { get; set; }
}
public sealed class PmsBankTransferDto
{
    public int BillId { get; set; }
    public decimal Amount { get; set; }
    public string Utr { get; set; } = "";
    public string BankName { get; set; } = "";
}
public sealed class PmsBankConfirmDto
{
    public string Decision { get; set; } = "Confirm";
    public string? Notes { get; set; }
}

[ApiController]
[Route("api/payments")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public sealed class PharmacyGatewayPaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyWorkflowAccess _access;
    private readonly PmsRazorpaySupport _razorpay;
    private readonly IWebHostEnvironment _environment;
    public PharmacyGatewayPaymentsController(AppDbContext db, PharmacyWorkflowAccess access, PmsRazorpaySupport razorpay, IWebHostEnvironment environment)
    { _db = db; _access = access; _razorpay = razorpay; _environment = environment; }

    private async Task<(PharmacyBill? Bill, int UserId, string? Error)> BillAccess(int billId, string permission = "View")
    {
        var bill = await _db.PharmacyBills.AsNoTracking().FirstOrDefaultAsync(x => x.Id == billId);
        if (bill == null) return (null, 0, "Bill not found.");
        var a = await _access.ResolveAsync(User, null, bill.BranchId);
        if (a.Error != null || a.Pharmacy != bill.PharmacyId) return (null, 0, "Bill is not accessible.");
        if (!await _access.CanAsync(a.User, "Bills", permission)) return (null, 0, "Bills permission denied.");
        return (bill, a.User, null);
    }
    private async Task<BranchGatewaySettings?> Settings(PharmacyBill bill) =>
        await _db.BranchGatewaySettings.FirstOrDefaultAsync(x => x.PharmacyId == bill.PharmacyId && x.BranchId == bill.BranchId);

    private async Task<decimal> NetPaid(PharmacyBill bill) =>
        await _db.PharmacyPayments.Where(x => x.PharmacyBillId == bill.Id && x.PharmacyId == bill.PharmacyId && x.BranchId == bill.BranchId)
            .SumAsync(x => (decimal?)(x.PaidAmount - x.RefundedAmount)) ?? 0m;

    [HttpGet("bills/{billId:int}/status")]
    [HttpGet("gateway/invoice-status/{billId:int}")]
    public async Task<IActionResult> BillStatus(int billId)
    {
        var (bill, _, error) = await BillAccess(billId);
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        var paid = await NetPaid(bill!);
        return Ok(new { data = new { billId, grandTotal = bill!.GrandTotal, paidAmount = paid, balanceDue = Math.Max(0m, bill.GrandTotal - paid),
            status = paid >= bill.GrandTotal ? "Paid" : paid > 0 ? "PartiallyPaid" : "Pending",
            currency = "INR", branchId = await _access.PublicBranchAsync(bill.PharmacyId, bill.BranchId) } });
    }

    [HttpGet("bills/{billId:int}/invoice-pdf")]
    public async Task<IActionResult> InvoicePdf(int billId)
    {
        var (bill, _, error) = await BillAccess(billId);
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        var pharmacy = await _db.Pharmacies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bill!.PharmacyId);
        var branch = await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bill!.BranchId);
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bill!.PatientId && x.PharmacyId == bill.PharmacyId);
        var items = await _db.PharmacyBillItems.AsNoTracking().Where(x => x.PharmacyBillId == billId).ToListAsync();
        var paid = await NetPaid(bill!);
        var bytes = Document.Create(document => document.Page(page => {
            page.Size(PageSizes.A4); page.Margin(35);
            page.Content().Column(column => {
                column.Spacing(8);
                column.Item().Text(pharmacy?.Name ?? "Pharmacy").FontSize(18).Bold();
                column.Item().Text($"Branch: {branch?.Name ?? ""}  |  Bill #{billId}");
                column.Item().Text($"Patient: {patient?.Name ?? ""}  |  Date (UTC): {bill!.BillDate:yyyy-MM-dd HH:mm}");
                column.Item().LineHorizontal(1);
                foreach (var item in items)
                    column.Item().Text($"{item.MedicineName}   Qty: {item.Quantity}   Rate: INR {item.Rate:0.00}   Total: INR {item.Total:0.00}");
                column.Item().LineHorizontal(1);
                column.Item().Text($"Subtotal: INR {bill.SubTotal:0.00}");
                column.Item().Text($"Discount: INR {bill.Discount:0.00}");
                column.Item().Text($"GST: INR {bill.GSTAmount:0.00}");
                column.Item().Text($"Total: INR {bill.GrandTotal:0.00}").Bold();
                column.Item().Text($"Paid: INR {paid:0.00}  |  Balance: INR {Math.Max(0, bill.GrandTotal-paid):0.00}");
                column.Item().Text("Generated from verified PMS billing records.").FontSize(9);
            });
        })).GeneratePdf();
        Response.Headers["Cache-Control"] = "no-store, private";
        return File(bytes, "application/pdf", $"pharmacy-bill-{billId}.pdf");
    }

    [HttpGet("bills/{billId:int}/history")]
    public async Task<IActionResult> History(int billId)
    {
        var (bill, _, error) = await BillAccess(billId);
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        var rows = await _db.PharmacyPayments.AsNoTracking().Where(x => x.PharmacyBillId == billId && x.PharmacyId == bill!.PharmacyId && x.BranchId == bill.BranchId)
            .OrderByDescending(x => x.PaymentDate)
            .Select(x => new { paymentId = x.Id, x.PaymentMode, x.PaidAmount, x.RefundedAmount, x.TransactionId, x.Status, x.PaymentDate }).ToListAsync();
        return Ok(new { data = new { items = rows, total = rows.Count } });
    }

    [HttpPost("gateway/create-order")]
    public async Task<IActionResult> CreateOrder([FromBody] PmsGatewayOrderDto dto)
    {
        var (bill, actorId, error) = await BillAccess(dto.BillId, "Create");
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        if (dto.Method is not ("upi" or "card") || string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 100)
            return BadRequest(new { message = "Method must be upi or card and idempotencyKey is required (max 100)." });
        var settings = await Settings(bill!);
        if (settings == null || dto.Method == "upi" && !settings.UpiEnabled || dto.Method == "card" && !settings.CardEnabled)
            return Conflict(new { message = "Selected payment method is not configured for this branch." });
        if (settings.Environment == "Test" && !settings.KeyId.StartsWith("rzp_test_", StringComparison.Ordinal) ||
            settings.Environment == "Live" && !settings.KeyId.StartsWith("rzp_live_", StringComparison.Ordinal))
            return Conflict(new { message = "Razorpay credentials do not match the configured branch environment." });
        var existing = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.PharmacyId == bill!.PharmacyId && x.BranchId == bill.BranchId && x.IdempotencyKey == dto.IdempotencyKey);
        if (existing != null)
        {
            if (existing.BillId != dto.BillId || existing.Amount != dto.Amount || existing.Method != dto.Method)
                return Conflict(new { message = "Idempotency key already used with different payment details." });
            return Ok(new { data = new { paymentAttemptId = existing.Id, existing.RazorpayOrderId, existing.Amount,
                amountPaise = ToPaise(existing.Amount), currency = "INR", razorpayKeyId = settings.KeyId, existing.Status } });
        }
        var check = await ValidateAmount(bill, settings, dto.Amount);
        if (check != null) return BadRequest(new { message = check });
        try
        {
            var client = _razorpay.Client(settings);
            var body = new { amount = ToPaise(dto.Amount), currency = "INR", receipt = $"pms_{Guid.NewGuid():N}"[..32],
                notes = new { billId = bill!.Id.ToString(), branch = bill.BranchId.ToString() } };
            using var response = await client.PostAsJsonAsync("https://api.razorpay.com/v1/orders", body);
            if (!response.IsSuccessStatusCode) return StatusCode(502, new { message = "Razorpay order creation failed. Verify merchant credentials and account status." });
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var orderId = json.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(orderId)) return StatusCode(502, new { message = "Razorpay did not return an order ID." });
            var attempt = new PharmacyGatewayAttempt { PharmacyId = bill!.PharmacyId, BranchId = bill.BranchId,
                BillId = bill.Id, CreatedByUserId = actorId, GatewaySettingsId = settings.Id, IdempotencyKey = dto.IdempotencyKey.Trim(),
                Amount = dto.Amount, Method = dto.Method, RazorpayOrderId = orderId };
            _db.PharmacyGatewayAttempts.Add(attempt); await _db.SaveChangesAsync();
            return StatusCode(201, new { data = new { paymentAttemptId = attempt.Id, razorpayOrderId = orderId,
                razorpayKeyId = settings.KeyId, amount = attempt.Amount, amountPaise = ToPaise(attempt.Amount), currency = "INR", status = "Pending" } });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        { return StatusCode(502, new { message = "Gateway unavailable or merchant credentials are not configured." }); }
    }

    [HttpPost("gateway/create-qr")]
    public async Task<IActionResult> CreateQr([FromBody] PmsGatewayOrderDto dto)
    {
        var (bill, actorId, error) = await BillAccess(dto.BillId, "Create");
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 100) return BadRequest(new { message = "idempotencyKey required." });
        var settings = await Settings(bill!);
        if (settings == null || !settings.UpiEnabled) return Conflict(new { message = "UPI not configured for this branch." });
        if (settings.Environment == "Test" && !settings.KeyId.StartsWith("rzp_test_", StringComparison.Ordinal) ||
            settings.Environment == "Live" && !settings.KeyId.StartsWith("rzp_live_", StringComparison.Ordinal))
            return Conflict(new { message = "Razorpay credentials do not match the configured branch environment." });
        var existing = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.PharmacyId == bill!.PharmacyId && x.BranchId == bill.BranchId && x.IdempotencyKey == dto.IdempotencyKey);
        if (existing != null)
        {
            if (existing.BillId != dto.BillId || existing.Amount != dto.Amount || existing.Method != "qr") return Conflict(new { message = "Idempotency key reused." });
            return Ok(new { data = new { paymentAttemptId = existing.Id, existing.RazorpayQrId, existing.Amount, existing.Status } });
        }
        var check = await ValidateAmount(bill, settings, dto.Amount);
        if (check != null) return BadRequest(new { message = check });
        try
        {
            var client = _razorpay.Client(settings);
            var expiry = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
            var body = new { type = "upi_qr", name = $"PMS bill {bill!.Id}", usage = "single_use", fixed_amount = true,
                payment_amount = ToPaise(dto.Amount), description = "Pharmacy bill payment", close_by = expiry,
                notes = new { billId = bill.Id.ToString() } };
            using var response = await client.PostAsJsonAsync("https://api.razorpay.com/v1/payments/qr_codes", body);
            if (!response.IsSuccessStatusCode) return StatusCode(502, new { message = "Razorpay QR creation failed; confirm that this merchant supports UPI QR." });
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            var qrId = root.GetProperty("id").GetString();
            var imageUrl = root.TryGetProperty("image_url", out var image) ? image.GetString() : null;
            if (string.IsNullOrWhiteSpace(qrId)) return StatusCode(502, new { message = "Gateway did not return QR ID." });
            var attempt = new PharmacyGatewayAttempt { PharmacyId = bill!.PharmacyId, BranchId = bill.BranchId,
                BillId = bill.Id, CreatedByUserId = actorId, GatewaySettingsId = settings.Id, IdempotencyKey = dto.IdempotencyKey.Trim(),
                Amount = dto.Amount, Method = "qr", RazorpayQrId = qrId };
            _db.PharmacyGatewayAttempts.Add(attempt); await _db.SaveChangesAsync();
            return StatusCode(201, new { data = new { paymentAttemptId = attempt.Id, qrId, qrImageUrl = imageUrl,
                amount = dto.Amount, expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiry), status = "Pending" } });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        { return StatusCode(502, new { message = "Gateway QR service unavailable." }); }
    }

    [HttpGet("gateway/attempts/{id:int}/status")]
    public async Task<IActionResult> AttemptStatus(int id)
    {
        var attempt = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (attempt == null) return NotFound();
        var (bill, _, error) = await BillAccess(attempt.BillId);
        if (error != null || bill!.PharmacyId != attempt.PharmacyId || bill.BranchId != attempt.BranchId) return Forbid();
        return Ok(new { data = new { paymentAttemptId = attempt.Id, attempt.BillId, attempt.Amount,
            attempt.Method, attempt.Status, attempt.CreatedAt,
            balanceDue = Math.Max(0m, bill.GrandTotal - await NetPaid(bill)) } });
    }

    // Recovery path when checkout callback or webhook is missed. Always fetches confirmed
    // payments from the recorded branch merchant; never trusts a client-supplied payment ID.
    [HttpPost("gateway/attempts/{id:int}/reconcile")]
    public async Task<IActionResult> ReconcileAttempt(int id)
    {
        var attempt = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (attempt == null) return NotFound();
        var (bill, _, error) = await BillAccess(attempt.BillId, "Create");
        if (error != null || bill!.PharmacyId != attempt.PharmacyId || bill.BranchId != attempt.BranchId)
            return Forbid();
        if (attempt.Method is not ("upi" or "card" or "qr") || attempt.GatewaySettingsId == null)
            return Conflict(new { message = "Only Razorpay checkout or QR attempts can be reconciled." });
        if (attempt.Status == "Captured")
            return Ok(new { data = new { paymentAttemptId = attempt.Id, status = "Captured", alreadyProcessed = true } });
        var merchant = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == attempt.GatewaySettingsId && x.PharmacyId == attempt.PharmacyId && x.BranchId == attempt.BranchId);
        if (merchant == null) return Conflict(new { message = "Original branch merchant settings not found." });
        var path = attempt.Method == "qr" && !string.IsNullOrWhiteSpace(attempt.RazorpayQrId)
            ? $"https://api.razorpay.com/v1/payments/qr_codes/{Uri.EscapeDataString(attempt.RazorpayQrId)}/payments?count=100"
            : !string.IsNullOrWhiteSpace(attempt.RazorpayOrderId)
                ? $"https://api.razorpay.com/v1/orders/{Uri.EscapeDataString(attempt.RazorpayOrderId)}/payments"
                : null;
        if (path == null) return Conflict(new { message = "Attempt is missing a provider order or QR reference." });
        try
        {
            var client = _razorpay.Client(merchant);
            using var response = await client.GetAsync(path);
            if (!response.IsSuccessStatusCode)
                return StatusCode(502, new { message = "Could not fetch this merchant's payments from Razorpay." });
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!json.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return StatusCode(502, new { message = "Unexpected Razorpay response format." });
            var confirmedIds = new List<string>();
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("status", out var status) || status.GetString() != "captured" ||
                    !item.TryGetProperty("amount", out var amount) || amount.GetInt64() != ToPaise(attempt.Amount) ||
                    !item.TryGetProperty("currency", out var currency) || currency.GetString() != "INR" ||
                    !item.TryGetProperty("id", out var paymentId) || string.IsNullOrWhiteSpace(paymentId.GetString()))
                    continue;
                if (attempt.Method != "qr" &&
                    (!item.TryGetProperty("order_id", out var order) || order.GetString() != attempt.RazorpayOrderId))
                    continue;
                confirmedIds.Add(paymentId.GetString()!);
            }
            if (confirmedIds.Count == 0)
                return Ok(new { data = new { paymentAttemptId = id, status = "Pending",
                    message = "No captured payment found for this attempt. The bill was not credited." } });
            if (confirmedIds.Count > 1)
                return Conflict(new { message = "Multiple captured payments found; manual reconciliation is required." });
            return await Reconcile(attempt.Id, confirmedIds[0]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or FormatException)
        { return StatusCode(502, new { message = "Razorpay reconciliation is temporarily unavailable." }); }
    }

    [HttpPost("gateway/verify")]
    public async Task<IActionResult> Verify([FromBody] PmsGatewayVerifyDto dto)
    {
        var attempt = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.PaymentAttemptId);
        if (attempt == null || attempt.Method is not ("upi" or "card")) return NotFound();
        var (bill, _, error) = await BillAccess(attempt.BillId, "Create");
        if (error != null || bill!.PharmacyId != attempt.PharmacyId || bill.BranchId != attempt.BranchId) return Forbid();
        var settings = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attempt.GatewaySettingsId && x.PharmacyId == bill.PharmacyId && x.BranchId == bill.BranchId);
        if (settings == null || dto.RazorpayOrderId != attempt.RazorpayOrderId || string.IsNullOrWhiteSpace(dto.RazorpayPaymentId))
            return BadRequest(new { message = "Order or merchant mismatch." });
        if (!_razorpaySignature(attempt, dto, settings)) return BadRequest(new { message = "Invalid gateway signature." });
        return await Reconcile(attempt.Id, dto.RazorpayPaymentId);
    }
    private bool _razorpaySignature(PharmacyGatewayAttempt attempt, PmsGatewayVerifyDto dto, BranchGatewaySettings settings)
    {
        try { return PmsRazorpaySupport.VerifyHmac($"{attempt.RazorpayOrderId}|{dto.RazorpayPaymentId}", dto.RazorpaySignature, _razorpay.Decrypt(settings.EncryptedKeySecret)); }
        catch (Exception) { return false; }
    }

    // Called only after signature verification or signed gateway webhook.
    private async Task<IActionResult> Reconcile(int attemptId, string paymentId)
    {
        var attempt = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attemptId);
        if (attempt == null || attempt.GatewaySettingsId == null) return NotFound();
        var merchant = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attempt.GatewaySettingsId);
        if (merchant == null) return Conflict(new { message = "Merchant settings not found." });
        try
        {
            var client = _razorpay.Client(merchant);
            using var response = await client.GetAsync($"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}");
            if (!response.IsSuccessStatusCode) return StatusCode(502, new { message = "Gateway payment verification failed." });
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            var status = root.GetProperty("status").GetString();
            var amountPaise = root.GetProperty("amount").GetInt64();
            var currency = root.GetProperty("currency").GetString();
            var gatewayOrder = root.TryGetProperty("order_id", out var order) && order.ValueKind == JsonValueKind.String ? order.GetString() : null;
            if (status != "captured") return Ok(new { data = new { status = "Pending", message = "Payment not captured; bill not credited." } });
            if (amountPaise != ToPaise(attempt.Amount) || currency != "INR" ||
                attempt.RazorpayOrderId != null && gatewayOrder != attempt.RazorpayOrderId)
                return Conflict(new { message = "Gateway amount, currency or order mismatch; payment not credited." });
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var tracked = await _db.PharmacyGatewayAttempts.FirstAsync(x => x.Id == attemptId);
            if (tracked.Status == "Captured")
            {
                if (tracked.RazorpayPaymentId != paymentId)
                    return Conflict(new { message = "An additional captured payment requires manual reconciliation; no duplicate bill credit was applied." });
                return Ok(new { data = new { status = "Captured", paymentAttemptId = attemptId, alreadyProcessed = true } });
            }
            if (await _db.PharmacyGatewayAttempts.AnyAsync(x => x.RazorpayPaymentId == paymentId && x.Id != attemptId))
                return Conflict(new { message = "Gateway payment ID has already been used." });
            var bill = await _db.PharmacyBills.FirstAsync(x => x.Id == tracked.BillId && x.PharmacyId == tracked.PharmacyId && x.BranchId == tracked.BranchId);
            var paid = await NetPaid(bill);
            if (tracked.Amount > bill.GrandTotal - paid)
            {
                tracked.Status = "ReconciliationRequired"; tracked.RazorpayPaymentId = paymentId;
                await _db.SaveChangesAsync(); await tx.CommitAsync();
                return Conflict(new { message = "Captured payment exceeds outstanding balance. Manual reconciliation or refund required; bill was not credited." });
            }
            _db.PharmacyPayments.Add(new PharmacyPayment { PharmacyId = bill.PharmacyId, BranchId = bill.BranchId,
                PharmacyBillId = bill.Id, PaymentMode = tracked.Method == "qr" ? "UPI" : "Razorpay",
                TransactionId = paymentId, PaidAmount = tracked.Amount, Status = "Paid", ReceivedByUserId = tracked.CreatedByUserId });
            tracked.Status = "Captured"; tracked.RazorpayPaymentId = paymentId;
            bill.PaymentStatus = paid + tracked.Amount >= bill.GrandTotal ? "Paid" : "PartiallyPaid";
            await _db.SaveChangesAsync(); await tx.CommitAsync();
            return Ok(new { data = new { status = "Captured", billId = bill.Id, paidAmount = tracked.Amount,
                totalPaid = paid + tracked.Amount, balanceDue = Math.Max(0, bill.GrandTotal - paid - tracked.Amount), billStatus = bill.PaymentStatus } });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
        { return StatusCode(502, new { message = "Could not confirm payment with gateway." }); }
    }

    [AllowAnonymous]
    [HttpPost("gateway/webhook/{merchantKey}")]
    [RequestSizeLimit(256_000)]
    public async Task<IActionResult> Webhook(string merchantKey)
    {
        var merchant = await _db.BranchGatewaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.WebhookRoutingKey == merchantKey);
        if (merchant == null || string.IsNullOrWhiteSpace(merchant.EncryptedWebhookSecret)) return NotFound();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        try
        {
            if (!PmsRazorpaySupport.VerifyHmac(raw, signature, _razorpay.Decrypt(merchant.EncryptedWebhookSecret)))
                return Unauthorized(new { message = "Invalid webhook signature." });
        }
        catch (Exception) { return StatusCode(503, new { message = "Webhook secret configuration unavailable." }); }
        var eventId = Request.Headers["x-razorpay-event-id"].ToString();
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 120) return BadRequest(new { message = "Missing event ID." });
        if (await _db.PharmacyGatewayWebhookEvents.AnyAsync(x => x.GatewaySettingsId == merchant.Id && x.EventId == eventId)) return Ok(new { received = true, duplicate = true });
        JsonDocument doc;
        try { doc = JsonDocument.Parse(raw); }
        catch (JsonException) { return BadRequest(new { message = "Invalid webhook JSON." }); }
        using (doc)
        {
        var root = doc.RootElement;
        var eventType = root.TryGetProperty("event", out var e) ? e.GetString() ?? "" : "";
        if (eventType == "payment.captured" || eventType == "qr_code.credited")
        {
            if (!root.TryGetProperty("payload", out var payload) || !payload.TryGetProperty("payment", out var pay) ||
                !pay.TryGetProperty("entity", out var entity) || !entity.TryGetProperty("id", out var payId))
                return BadRequest(new { message = "Payment payload missing." });
            var paymentId = payId.GetString();
            if (string.IsNullOrWhiteSpace(paymentId)) return BadRequest();
            var orderId = entity.TryGetProperty("order_id", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : null;
            var qrId = payload.TryGetProperty("qr_code", out var qr) && qr.TryGetProperty("entity", out var qrEntity) && qrEntity.TryGetProperty("id", out var qrIdentifier) ? qrIdentifier.GetString() : null;
            var attempt = await _db.PharmacyGatewayAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.GatewaySettingsId == merchant.Id &&
                (orderId != null && x.RazorpayOrderId == orderId || qrId != null && x.RazorpayQrId == qrId));
            // Do not acknowledge an unmatched captured payment: Razorpay may retry after an
            // order/QR persistence race. Operators can also reconcile from the merchant dashboard.
            if (attempt == null) return StatusCode(503, new { message = "Captured payment cannot yet be matched to a PMS attempt; retry required." });
            var result = await Reconcile(attempt.Id, paymentId);
            if (result is ObjectResult obj && obj.StatusCode is >= 400) return result;
        }
        _db.PharmacyGatewayWebhookEvents.Add(new PharmacyGatewayWebhookEvent { GatewaySettingsId = merchant.Id, EventId = eventId, EventType = eventType });
        await _db.SaveChangesAsync();
        return Ok(new { received = true });
        }
    }

    [HttpPost("manual")]
    public async Task<IActionResult> Cash([FromBody] PmsCashDto dto)
    {
        var (bill, user, error) = await BillAccess(dto.BillId, "Create");
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        var settings = await Settings(bill!);
        if (settings?.CashEnabled != true) return Conflict(new { message = "Cash is not enabled for this branch." });
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 100) return BadRequest(new { message = "idempotencyKey required." });
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var existing = await _db.PharmacyGatewayAttempts.FirstOrDefaultAsync(x => x.PharmacyId == bill!.PharmacyId && x.BranchId == bill.BranchId && x.IdempotencyKey == dto.IdempotencyKey);
        if (existing != null)
        {
            if (existing.BillId != dto.BillId || existing.Amount != dto.Amount || existing.Method != "cash") return Conflict(new { message = "Idempotency key reused." });
            return Ok(new { data = new { paymentAttemptId = existing.Id, existing.Status, alreadyProcessed = true } });
        }
        var check = await ValidateAmount(bill!, settings, dto.Amount);
        if (check != null) return BadRequest(new { message = check });
        var attempt = new PharmacyGatewayAttempt { PharmacyId = bill!.PharmacyId, BranchId = bill.BranchId,
            BillId = bill.Id, CreatedByUserId = user, IdempotencyKey = dto.IdempotencyKey.Trim(), Amount = dto.Amount, Method = "cash", Status = "Captured" };
        _db.PharmacyGatewayAttempts.Add(attempt);
        _db.PharmacyPayments.Add(new PharmacyPayment { PharmacyId = bill.PharmacyId, BranchId = bill.BranchId, PharmacyBillId = bill.Id,
            PaymentMode = "Cash", TransactionId = $"PMS-CASH-{Guid.NewGuid():N}", PaidAmount = dto.Amount,
            Status = "Paid", ReceivedByUserId = user });
        var paid = await NetPaid(bill);
        bill = await _db.PharmacyBills.FirstAsync(x => x.Id == bill.Id);
        bill.PaymentStatus = paid + dto.Amount >= bill.GrandTotal ? "Paid" : "PartiallyPaid";
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return StatusCode(201, new { data = new { paymentAttemptId = attempt.Id, billId = bill.Id, paidAmount = dto.Amount,
            totalPaid = paid + dto.Amount, balanceDue = Math.Max(0, bill.GrandTotal - paid - dto.Amount), billStatus = bill.PaymentStatus } });
    }

    [HttpPost("bank-transfers")]
    public async Task<IActionResult> BankTransfer([FromBody] PmsBankTransferDto dto)
    {
        var (bill, user, error) = await BillAccess(dto.BillId, "Create");
        if (error != null) return StatusCode(error == "Bill not found." ? 404 : 403, new { message = error });
        var settings = await Settings(bill!);
        if (settings?.BankEnabled != true) return Conflict(new { message = "Bank transfer is not enabled for this branch." });
        if (string.IsNullOrWhiteSpace(dto.Utr) || dto.Utr.Length > 100 || string.IsNullOrWhiteSpace(dto.BankName))
            return BadRequest(new { message = "UTR and bank name required." });
        var check = await ValidateAmount(bill!, settings, dto.Amount);
        if (check != null) return BadRequest(new { message = check });
        if (await _db.PharmacyBankTransferRequests.AnyAsync(x => x.PharmacyId == bill!.PharmacyId && x.BranchId == bill.BranchId && x.Utr == dto.Utr.Trim()))
            return Conflict(new { message = "UTR already submitted for this branch." });
        var request = new PharmacyBankTransferRequest { PharmacyId = bill!.PharmacyId, BranchId = bill.BranchId,
            BillId = bill.Id, Amount = dto.Amount, Utr = dto.Utr.Trim(), BankName = dto.BankName.Trim(), SubmittedByUserId = user };
        _db.PharmacyBankTransferRequests.Add(request); await _db.SaveChangesAsync();
        return StatusCode(201, new { data = new { transferId = request.Id, request.Status, amount = request.Amount,
            note = "Payment is not credited until bank receipt is independently verified." } });
    }

    [HttpPost("bank-transfers/{id:int}/confirm")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> ConfirmBank(int id, [FromBody] PmsBankConfirmDto dto)
    {
        var request = await _db.PharmacyBankTransferRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (request == null) return NotFound();
        var (bill, user, error) = await BillAccess(request.BillId, "Edit");
        if (error != null || bill!.PharmacyId != request.PharmacyId || bill.BranchId != request.BranchId) return Forbid();
        if (dto.Decision is not ("Confirm" or "Reject")) return BadRequest(new { message = "Decision must be Confirm or Reject." });
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tracked = await _db.PharmacyBankTransferRequests.FirstAsync(x => x.Id == id);
        if (tracked.Status != "PendingVerification") return Conflict(new { message = "Transfer has already been reviewed." });
        if (dto.Decision == "Reject")
        { tracked.Status = "Rejected"; tracked.VerifiedByUserId = user; tracked.VerifiedAt = DateTime.UtcNow;
          await _db.SaveChangesAsync(); await tx.CommitAsync(); return Ok(new { data = new { transferId = id, status = "Rejected" } }); }
        var paid = await NetPaid(bill!);
        if (tracked.Amount > bill!.GrandTotal - paid) return Conflict(new { message = "Transfer exceeds outstanding balance. Reconcile before confirming." });
        _db.PharmacyPayments.Add(new PharmacyPayment { PharmacyId = bill.PharmacyId, BranchId = bill.BranchId, PharmacyBillId = bill.Id,
            PaymentMode = "BankTransfer", TransactionId = tracked.Utr, PaidAmount = tracked.Amount, Status = "Paid", ReceivedByUserId = user });
        tracked.Status = "Verified"; tracked.VerifiedByUserId = user; tracked.VerifiedAt = DateTime.UtcNow;
        var update = await _db.PharmacyBills.FirstAsync(x => x.Id == bill.Id);
        update.PaymentStatus = paid + tracked.Amount >= update.GrandTotal ? "Paid" : "PartiallyPaid";
        await _db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { data = new { transferId = id, status = "Verified", billStatus = update.PaymentStatus } });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] int? branchId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Bills", "View")) return Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.PharmacyPayments.AsNoTracking().Where(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.PaymentDate).Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new { paymentId = x.Id, billId = x.PharmacyBillId, x.PaymentMode, x.PaidAmount,
                x.RefundedAmount, x.Status, x.TransactionId, x.PaymentDate }).ToListAsync();
        return Ok(new { data = new { items, total, hasMore = (long)page * pageSize < total } });
    }

    [HttpGet("transactions/export")]
    [HttpGet("gateway/export-excel")]
    public async Task<IActionResult> Export([FromQuery] int? branchId)
    {
        var a = await _access.ResolveAsync(User, branchId);
        if (a.Error != null) return StatusCode(403, new { message = a.Error });
        if (!await _access.CanAsync(a.User, "Reports", "View")) return Forbid();
        var rows = await (from payment in _db.PharmacyPayments.AsNoTracking()
            join bill in _db.PharmacyBills.AsNoTracking() on payment.PharmacyBillId equals bill.Id
            join patient in _db.Patients.AsNoTracking() on bill.PatientId equals patient.Id
            where payment.PharmacyId == a.Pharmacy && payment.BranchId == a.Branch &&
                bill.PharmacyId == a.Pharmacy && bill.BranchId == a.Branch
            orderby payment.PaymentDate descending
            select new { payment.Id, payment.PharmacyBillId, payment.PaymentDate, payment.PaymentMode,
                payment.TransactionId, payment.PaidAmount, payment.RefundedAmount, payment.Status,
                bill.GrandTotal, PatientName = patient.Name, PatientPhone = patient.Phone }).Take(10000).ToListAsync();
        var sb = new StringBuilder("PaymentId,BillId,DateIST,Patient,Phone,InvoiceTotal,PaidAmount,RefundedAmount,Method,Reference,Status\r\n");
        foreach (var x in rows)
        {
            var ist = x.PaymentDate.AddHours(5).AddMinutes(30); // Stored timestamps are UTC.
            sb.AppendLine(string.Join(",", new[] { x.Id.ToString(), x.PharmacyBillId.ToString(), Csv(ist.ToString("yyyy-MM-dd HH:mm:ss")),
                Csv(x.PatientName), Csv(x.PatientPhone), x.GrandTotal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.PaidAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                x.RefundedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), Csv(x.PaymentMode),
                Csv(x.TransactionId), Csv(x.Status) }));
        }
        return File(new UTF8Encoding(true).GetBytes(sb.ToString()), "text/csv", "pms-payments.csv");
    }
    private static string Csv(string? value)
    {
        var s = value ?? "";
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    [HttpPost("gateway/simulate-qr-payment/{billId:int}")]
    public IActionResult Simulate(int billId) => _environment.IsDevelopment()
        ? Ok(new { simulated = true, billId, credited = false, message = "Development simulation only; no real payment or bill credit." })
        : NotFound();

    private async Task<string?> ValidateAmount(PharmacyBill bill, BranchGatewaySettings settings, decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount) return "Amount must be positive INR with up to 2 decimal places.";
        var outstanding = bill.GrandTotal - await NetPaid(bill);
        if (outstanding <= 0 || amount > outstanding) return "Payment exceeds the outstanding bill balance.";
        if (!settings.PartialPaymentsEnabled && amount != outstanding) return "Partial payments are disabled for this branch.";
        return null;
    }
    private static long ToPaise(decimal amount) => checked((long)(amount * 100m));
}
