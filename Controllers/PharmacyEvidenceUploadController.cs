using System.Security.Cryptography;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
[Route("api/pharmacy")]
public sealed class PharmacyEvidenceUploadController : ControllerBase
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private readonly AppDbContext _db;
    private readonly PharmacyWorkflowAccess _access;
    public PharmacyEvidenceUploadController(AppDbContext db, PharmacyWorkflowAccess access)
    { _db = db; _access = access; }

    [HttpPost("dispensing/drafts/{draftId:int}/prescription-photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageBytes + 100_000)]
    public async Task<IActionResult> DraftPhoto(int draftId, IFormFile file)
    {
        var draft = await _db.PharmacyPrescriptionDrafts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == draftId);
        if (draft == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, draft.BranchId);
        if (a.Error != null || a.Pharmacy != draft.PharmacyId || a.User != draft.CreatedByUserId) return Forbid();
        if (!await _access.CanAsync(a.User, "Prescriptions", "Edit")) return Forbid();
        if (draft.Status != "Draft") return Conflict(new { message = "Upload photos before submitting the draft." });
        return await SaveImage(file, a.Pharmacy, a.Branch, a.User, "PrescriptionPhoto", draftId, null, null);
    }

    [HttpPost("prescriptions/{prescriptionId:int}/photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageBytes + 100_000)]
    public async Task<IActionResult> PrescriptionPhoto(int prescriptionId, IFormFile file)
    {
        var rx = await _db.Prescriptions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == prescriptionId);
        if (rx == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, rx.BranchId);
        if (a.Error != null || a.Pharmacy != rx.PharmacyId) return Forbid();
        if (!await _access.CanAsync(a.User, "Prescriptions", "Edit")) return Forbid();
        return await SaveImage(file, a.Pharmacy, a.Branch, a.User, "PrescriptionPhoto", null, prescriptionId, null);
    }

    [HttpPost("bills/{billId:int}/receipt-photo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageBytes + 100_000)]
    public async Task<IActionResult> BillReceiptPhoto(int billId, IFormFile file)
    {
        var bill = await _db.PharmacyBills.AsNoTracking().FirstOrDefaultAsync(x => x.Id == billId);
        if (bill == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, bill.BranchId);
        if (a.Error != null || a.Pharmacy != bill.PharmacyId) return Forbid();
        if (!await _access.CanAsync(a.User, "Bills", "Edit")) return Forbid();
        return await SaveImage(file, a.Pharmacy, a.Branch, a.User, "PaymentReceipt", null, null, billId);
    }

    [HttpGet("evidence/{id:int}")]
    public async Task<IActionResult> GetImage(int id)
    {
        var evidence = await _db.PharmacyUploadedEvidences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (evidence == null) return NotFound();
        var a = await _access.ResolveAsync(User, null, evidence.BranchId);
        if (a.Error != null || a.Pharmacy != evidence.PharmacyId) return Forbid();
        var module = evidence.EvidenceType == "PaymentReceipt" ? "Bills" : "Prescriptions";
        if (evidence.DraftId.HasValue && !evidence.PrescriptionId.HasValue &&
            !await _db.PharmacyPrescriptionDrafts.AsNoTracking().AnyAsync(x => x.Id == evidence.DraftId && x.CreatedByUserId == a.User)) return Forbid();
        if (!await _access.CanAsync(a.User, module, "View")) return Forbid();
        Response.Headers["Cache-Control"] = "no-store, private";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(evidence.Content, evidence.ContentType);
    }

    [HttpGet("evidence")]
    public async Task<IActionResult> List([FromQuery] int? prescriptionId, [FromQuery] int? draftId, [FromQuery] int? billId)
    {
        if (new[] { prescriptionId.HasValue, draftId.HasValue, billId.HasValue }.Count(x => x) != 1)
            return BadRequest(new { message = "Supply exactly one of prescriptionId, draftId, or billId." });
        var query = _db.PharmacyUploadedEvidences.AsNoTracking().AsQueryable();
        if (prescriptionId.HasValue) query = query.Where(x => x.PrescriptionId == prescriptionId);
        if (draftId.HasValue) query = query.Where(x => x.DraftId == draftId);
        if (billId.HasValue) query = query.Where(x => x.BillId == billId);
        var first = await query.Select(x => new { x.PharmacyId, x.BranchId, x.DraftId, x.EvidenceType }).FirstOrDefaultAsync();
        if (first == null) return Ok(new { data = new { items = Array.Empty<object>(), total = 0 } });
        var a = await _access.ResolveAsync(User, null, first.BranchId);
        if (a.Error != null || a.Pharmacy != first.PharmacyId) return Forbid();
        if (first.DraftId.HasValue && !await _db.PharmacyPrescriptionDrafts.AsNoTracking()
            .AnyAsync(x => x.Id == first.DraftId && x.CreatedByUserId == a.User)) return Forbid();
        if (!await _access.CanAsync(a.User, first.EvidenceType == "PaymentReceipt" ? "Bills" : "Prescriptions", "View")) return Forbid();
        var items = await query.Where(x => x.PharmacyId == a.Pharmacy && x.BranchId == a.Branch)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new { x.Id, x.FileName, x.ContentType, sizeBytes = x.Content.Length, x.EvidenceType, x.UploadedAt }).ToListAsync();
        return Ok(new { data = new { items, total = items.Count } });
    }

    private async Task<IActionResult> SaveImage(IFormFile file, int pharmacy, int branch, int user,
        string kind, int? draftId, int? prescriptionId, int? billId)
    {
        if (file == null || file.Length <= 0 || file.Length > MaxImageBytes)
            return BadRequest(new { message = "Upload a nonempty image no larger than 10 MB." });
        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory);
        var bytes = memory.ToArray();
        if (bytes.LongLength != file.Length || bytes.Length > MaxImageBytes)
            return BadRequest(new { message = "Image size mismatch." });
        var mime = DetectImage(bytes);
        if (mime == null) return BadRequest(new { message = "Only valid JPEG, PNG or WebP images are accepted." });
        var evidence = new PharmacyUploadedEvidence {
            PharmacyId = pharmacy, BranchId = branch, UploadedByUserId = user, EvidenceType = kind,
            DraftId = draftId, PrescriptionId = prescriptionId, BillId = billId,
            FileName = Path.GetFileName(file.FileName ?? "image")[..Math.Min(255, Path.GetFileName(file.FileName ?? "image").Length)],
            ContentType = mime, Content = bytes, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        _db.PharmacyUploadedEvidences.Add(evidence);
        await _db.SaveChangesAsync();
        return StatusCode(201, new { data = new { evidenceId = evidence.Id, evidence.EvidenceType, evidence.FileName,
            evidence.ContentType, sizeBytes = bytes.Length, imageUrl = $"/api/pharmacy/evidence/{evidence.Id}" } });
    }
    private static string? DetectImage(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
