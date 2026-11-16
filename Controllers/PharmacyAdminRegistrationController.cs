using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-admin/pharmacy")]
[Authorize(Roles = "PharmacyAdmin")]
public sealed class PharmacyAdminRegistrationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtHelper _jwtHelper;
    public PharmacyAdminRegistrationController(AppDbContext db, JwtHelper jwtHelper)
    {
        _db = db;
        _jwtHelper = jwtHelper;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] CreatePharmacyDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized(new { Success = false, Message = "Invalid Admin token." });

        // Serializable transaction prevents the same Admin from registering twice concurrently.
        await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var admin = await _db.Users.FirstOrDefaultAsync(u => u.Id == adminId && u.Role == "PharmacyAdmin" && u.IsActive);
        if (admin == null) return Unauthorized(new { Success = false, Message = "Admin account not found." });
        if (admin.PharmacyId.HasValue)
            return Conflict(new { Success = false, Message = "This Admin already has a pharmacy." });
        if (admin.AccountStatus != "Pending")
            return StatusCode(403, new { Success = false, Message = "Only a pending, unassigned Admin can register a pharmacy." });

        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { Success = false, Message = "Pharmacy name is required." });
        if (await _db.Pharmacies.AnyAsync(p => p.Name.ToLower() == name.ToLower()))
            return Conflict(new { Success = false, Message = "Pharmacy name already exists." });

        var now = DateTime.UtcNow;
        var pharmacy = new PharmacyTenant
        {
            Name = name, Phone = Clean(dto.Phone), Email = Clean(dto.Email),
            Address = Clean(dto.Address), City = Clean(dto.City), State = Clean(dto.State),
            District = Clean(dto.District), Country = Clean(dto.Country), PostalCode = Clean(dto.PostalCode),
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        _db.Pharmacies.Add(pharmacy);
        await _db.SaveChangesAsync();

        var main = new Branch
        {
            PharmacyId = pharmacy.Id,
            Name = pharmacy.Name + " - Main Branch", IsMainBranch = true, IsActive = true,
            Phone = pharmacy.Phone, Email = pharmacy.Email, Address = pharmacy.Address,
            City = pharmacy.City, State = pharmacy.State, District = pharmacy.District,
            Country = pharmacy.Country, PostalCode = pharmacy.PostalCode,
            SourceSystem = "PharmacyStandalone", CreatedAt = now, UpdatedAt = now
        };
        _db.Branches.Add(main);
        await _db.SaveChangesAsync();

        _db.PharmacyBranchIdentities.Add(new PharmacyBranchIdentity
        {
            PharmacyId = pharmacy.Id,
            BranchId = 1,
            ExistingBranchId = main.Id
        });
        await _db.SaveChangesAsync();

        admin.PharmacyId = pharmacy.Id;
        admin.BranchId = main.Id;
        admin.AccountStatus = "Active";
        admin.PharmacyAssignedAt = now;
        admin.PharmacyAssignedByUserId = admin.Id;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Issue a fresh token containing the newly assigned PharmacyId and BranchId.
        // The frontend must replace its previous access token with this value.
        var accessToken = _jwtHelper.GenerateToken(admin);

        return StatusCode(201, new
        {
            Success = true,
            Message = "Pharmacy created, Main Branch 1 created, and Admin assigned automatically. Use the returned access token for subsequent requests.",
            PharmacyId = pharmacy.Id,
            PharmacyName = pharmacy.Name,
            MainBranch = new { BranchId = 1, main.Name, main.IsMainBranch },
            AdminId = admin.Id,
            AccessToken = accessToken,
            TokenType = "Bearer"
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Unauthorized();
        var admin = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == adminId && u.Role == "PharmacyAdmin" && u.IsActive);
        if (admin == null) return Unauthorized();
        if (!admin.PharmacyId.HasValue) return NotFound(new { Success = false, Message = "Register your pharmacy first." });
        var pharmacy = await _db.Pharmacies.AsNoTracking().Include(x => x.Branches).FirstOrDefaultAsync(x => x.Id == admin.PharmacyId.Value);
        return pharmacy == null ? NotFound() : Ok(new { Success = true, Pharmacy = pharmacy });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
