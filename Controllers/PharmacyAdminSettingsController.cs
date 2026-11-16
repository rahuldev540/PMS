using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/settings")]
[Authorize(Roles = "PharmacyAdmin")]
public sealed class PharmacyAdminSettingsController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public PharmacyAdminSettingsController(AppDbContext db, PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    private async Task<IActionResult?> CheckSettingsPermission(string action, string module = "Settings")
    {
        if (await _permissionChecker.HasPermissionAsync(UserId, module, action))
            return null;

        return StatusCode(StatusCodes.Status403Forbidden,
            new { Success = false, Message = $"You do not have {action} permission for {module}." });
    }

    // PATCH /api/pharmacy/settings/pharmacy-status
    // This endpoint deliberately does not require an active pharmacy, so its
    // authenticated admin can reactivate the pharmacy after deactivation.
    [HttpPatch("pharmacy-status")]
    public async Task<IActionResult> ChangeOwnPharmacyStatus(
        [FromBody] StatusDto dto, CancellationToken ct)
    {
        var isAssignedAdmin = await _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == UserId && u.Role == "PharmacyAdmin" && u.IsActive &&
            u.PharmacyId == PharmacyId, ct);
        if (!isAssignedAdmin)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "You are not assigned as an active admin of this pharmacy." });

        var pharmacy = await _db.Pharmacies.FirstOrDefaultAsync(p => p.Id == PharmacyId, ct);
        if (pharmacy == null)
            return NotFound(new { Success = false, Message = "Your pharmacy was not found." });

        if (pharmacy.IsDeleted)
            return Conflict(new { Success = false, Message = "A soft-deleted pharmacy cannot be reactivated through the normal status API." });

        pharmacy.IsActive = dto.IsActive;
        pharmacy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new
        {
            Success = true,
            PharmacyId = pharmacy.Id,
            IsActive = pharmacy.IsActive,
            Message = dto.IsActive ? "Pharmacy activated successfully." : "Pharmacy deactivated successfully."
        });
    }

    // DELETE /api/pharmacy/settings/pharmacy - soft delete, preserve historical data.
    [HttpDelete("pharmacy")]
    public async Task<IActionResult> SoftDeleteOwnPharmacy(CancellationToken ct)
    {
        var isAssignedAdmin = await _db.Users.AsNoTracking().AnyAsync(u =>
            u.Id == UserId && u.Role == "PharmacyAdmin" && u.IsActive &&
            u.PharmacyId == PharmacyId, ct);
        if (!isAssignedAdmin)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "Only the assigned Pharmacy Admin may delete this pharmacy." });

        var pharmacy = await _db.Pharmacies.FirstOrDefaultAsync(p => p.Id == PharmacyId, ct);
        if (pharmacy == null)
            return NotFound(new { Success = false, Message = "Your pharmacy was not found." });
        if (pharmacy.IsDeleted)
            return Conflict(new { Success = false, Message = "Pharmacy is already soft-deleted." });

        pharmacy.IsActive = false;
        pharmacy.IsDeleted = true;
        pharmacy.DeletedAt = DateTime.UtcNow;
        pharmacy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { Success = true, PharmacyId = pharmacy.Id, IsActive = false,
            IsDeleted = true, pharmacy.DeletedAt, Message = "Pharmacy soft-deleted successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var permissionError = await CheckSettingsPermission("View");
        if (permissionError != null) return permissionError;

        var pharmacy = await _db.Pharmacies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == PharmacyId, ct);

        if (pharmacy == null)
            return NotFound(new { Success = false, Message = "Pharmacy tenant was not found." });

        var branch = await _db.Branches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == BranchId && x.PharmacyId == PharmacyId, ct);

        if (branch == null)
            return NotFound(new { Success = false, Message = "Pharmacy branch was not found." });

        var setting = await _db.PharmacyBranchSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId, ct);

        return Ok(new
        {
            Success = true,
            Settings = new
            {
                PharmacyName = pharmacy.Name,
                BranchLocation = branch.Name,
                PhoneNumber = branch.Phone,
                EmailAddress = branch.Email,
                LicenseNumber = setting?.LicenseNumber,
                GSTNumber = setting?.GSTNumber,
                Address = branch.Address,
                PharmacyStatus = branch.IsActive ? "Active" : "Inactive",
                IsActive = branch.IsActive,
                Currency = setting?.Currency ?? "INR",
                TaxGSTRate = setting?.TaxGSTRate ?? 18m,
                InvoicePrefix = setting?.InvoicePrefix ?? "PHAR-",
                PaymentTerms = setting?.PaymentTerms ?? "Due on Receipt",
                BusinessHours = setting?.BusinessHours ?? "9:00 AM - 10:00 PM",
                LowStockThreshold = setting?.LowStockThreshold ?? 10,
                ExpiryAlertHorizonDays = setting?.ExpiryAlertHorizonDays ?? 90,
                StockManagementEnabled = setting?.StockManagementEnabled ?? true,
                BatchExpiryTrackingEnabled = setting?.BatchExpiryTrackingEnabled ?? true,
                MaximumAllowedDiscount = setting?.MaximumAllowedDiscount ?? 10m,
                SalesInvoiceDetails = setting?.SalesInvoiceDetails ?? "Show Tax Details",
                ReturnWindowDays = setting?.ReturnWindowDays ?? 7,
                AutomaticReorderingEnabled = setting?.AutomaticReorderingEnabled ?? false,
                LowStockNotificationsEnabled = setting?.LowStockNotificationsEnabled ?? true,
                ExpiryWarningAlertsEnabled = setting?.ExpiryWarningAlertsEnabled ?? true,
                OrderTransferWarningsEnabled = setting?.OrderTransferWarningsEnabled ?? true,
                PharmacyId = pharmacy.Id,
                OperationMode = "Standalone",
                // Only standalone pharmacy settings are returned.
            }
        });
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] PharmacyAdminSettingsDto dto, CancellationToken ct)
    {
        var permissionError = await CheckSettingsPermission("Edit");
        if (permissionError != null) return permissionError;

        if (dto.TaxGSTRate < 0 || dto.TaxGSTRate > 100 ||
            dto.MaximumAllowedDiscount < 0 || dto.MaximumAllowedDiscount > 100 ||
            dto.LowStockThreshold < 0 || dto.ExpiryAlertHorizonDays < 1 || dto.ReturnWindowDays < 0)
        {
            return BadRequest(new { Success = false, Message = "One or more settings values are invalid." });
        }

        var branch = await _db.Branches
            .FirstOrDefaultAsync(x => x.Id == BranchId && x.PharmacyId == PharmacyId, ct);

        if (branch == null)
            return NotFound(new { Success = false, Message = "Pharmacy branch was not found." });

        branch.Phone = dto.PhoneNumber?.Trim();
        branch.Email = dto.EmailAddress?.Trim();
        branch.Address = dto.Address?.Trim();
        branch.IsActive = dto.IsActive;

        var setting = await _db.PharmacyBranchSettings
            .FirstOrDefaultAsync(x => x.PharmacyId == PharmacyId && x.BranchId == BranchId, ct);

        if (setting == null)
        {
            setting = new PharmacyBranchSetting
            {
                PharmacyId = PharmacyId,
                BranchId = BranchId,

            };
            _db.PharmacyBranchSettings.Add(setting);
        }

        // Pharmacy settings are managed by the assigned Pharmacy Admin.
        var pharmacy = await _db.Pharmacies.FirstOrDefaultAsync(x => x.Id == PharmacyId, ct);
        if (pharmacy == null)
            return NotFound(new { Success = false, Message = "Pharmacy tenant was not found." });

        {
            if (string.IsNullOrWhiteSpace(dto.PharmacyName))
                return BadRequest(new { Success = false, Message = "PharmacyName is required in standalone mode." });

            pharmacy.Name = dto.PharmacyName.Trim();
            pharmacy.UpdatedAt = DateTime.UtcNow;
            setting.PharmacyName = pharmacy.Name;
        }
        setting.LicenseNumber = dto.LicenseNumber?.Trim();
        setting.GSTNumber = dto.GSTNumber?.Trim();
        setting.Currency = dto.Currency.Trim();
        setting.TaxGSTRate = dto.TaxGSTRate;
        setting.InvoicePrefix = dto.InvoicePrefix.Trim();
        setting.PaymentTerms = dto.PaymentTerms.Trim();
        setting.BusinessHours = dto.BusinessHours.Trim();
        setting.LowStockThreshold = dto.LowStockThreshold;
        setting.ExpiryAlertHorizonDays = dto.ExpiryAlertHorizonDays;
        setting.StockManagementEnabled = dto.StockManagementEnabled;
        setting.BatchExpiryTrackingEnabled = dto.BatchExpiryTrackingEnabled;
        setting.MaximumAllowedDiscount = dto.MaximumAllowedDiscount;
        setting.SalesInvoiceDetails = dto.SalesInvoiceDetails.Trim();
        setting.ReturnWindowDays = dto.ReturnWindowDays;
        setting.AutomaticReorderingEnabled = dto.AutomaticReorderingEnabled;
        setting.LowStockNotificationsEnabled = dto.LowStockNotificationsEnabled;
        setting.ExpiryWarningAlertsEnabled = dto.ExpiryWarningAlertsEnabled;
        setting.OrderTransferWarningsEnabled = dto.OrderTransferWarningsEnabled;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByUserId = UserId;

        await _db.SaveChangesAsync(ct);

        return Ok(new { Success = true, Message = "Pharmacy settings saved successfully." });
    }



}
