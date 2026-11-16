using AuthDemo.Data;
using AuthDemo.DTOs;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthDemo.Helpers;
using AuthDemo.Services.Interfaces;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-super-admin")]
[Authorize(Roles = "PharmacySuperAdmin")]
public class PharmacySuperAdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    // Pharmacy Admin sidebar modules. Super Admin grants View/Create/Edit/Delete
    // only against these modules; no hidden/internal API module names are exposed.
    private static readonly string[] PermissionModules =
        AuthDemo.Constants.PermissionModules.PharmacyAdmin;

    public PharmacySuperAdminController(
        AppDbContext db,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _db = db;
        _emailService = emailService;
        _configuration = configuration;
    }

    // ============================================================
    // COMMON
    // ============================================================

    private int CurrentUserId()
    {
        var value =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(value, out var id)
            ? id
            : 0;
    }

    private string CurrentUserName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.FindFirst(ClaimTypes.Email)?.Value
               ?? "Pharmacy Super Admin";
    }

    private string GetClientIpAddress()
    {
        // When the API is behind ngrok / reverse proxy, the original
        // client IP is normally forwarded in X-Forwarded-For.
        var forwardedFor =
            Request.Headers["X-Forwarded-For"]
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp =
                forwardedFor
                    .Split(',')
                    .FirstOrDefault()?
                    .Trim();

            if (!string.IsNullOrWhiteSpace(firstIp))
                return firstIp;
        }

        // Some proxies provide X-Real-IP instead.
        var realIp =
            Request.Headers["X-Real-IP"]
                .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Trim();

        var remoteIp =
            HttpContext.Connection.RemoteIpAddress;

        if (remoteIp == null)
            return string.Empty;

        if (remoteIp.IsIPv4MappedToIPv6)
            remoteIp = remoteIp.MapToIPv4();

        var ip = remoteIp.ToString();

        // ::1 is IPv6 localhost. Show a familiar complete local IPv4
        // address in local development.
        if (ip == "::1")
            return "127.0.0.1";

        return ip;
    }

    private void AddAudit(
        string action,
        string? systemAction = null,
        int? branchId = null)
    {
        var userId = CurrentUserId();

        if (userId <= 0)
            return;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = CurrentUserName(),
            Role = "PharmacySuperAdmin",

            BranchId = branchId,

            Action = action,
            SystemAction = systemAction ?? action,

            IsLoginActivity = false,

            IpAddress =
                GetClientIpAddress(),

            Browser =
                Request.Headers.UserAgent.ToString(),

            Device = "Unknown",

            LoginTime = DateTime.UtcNow,
            IsOnline = true,
            Timestamp = DateTime.UtcNow
        });
    }

    // Phase 2: legacy CMS hospital listing, status changes, and hospital-branch
    // routes were removed. Pharmacy and branch read APIs remain below.

    // ============================================================
    // ALL STANDALONE PHARMACY BRANCHES
    // GET /api/pharmacy-super-admin/branches
    // ============================================================

    [HttpGet("branches")]
    public async Task<IActionResult> AllBranches(
        [FromQuery] string? search,
        [FromQuery] int? pharmacyId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            _db.Branches
                .AsNoTracking()
                .Where(x => x.PharmacyId.HasValue)
                .AsQueryable();

        if (pharmacyId.HasValue)
            query = query.Where(x => x.PharmacyId == pharmacyId.Value);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(x =>
                (x.Name ?? "").Contains(keyword) ||
                (x.Address ?? "").Contains(keyword) ||
                (x.City ?? "").Contains(keyword) ||
                (x.State ?? "").Contains(keyword) ||
                (x.District ?? "").Contains(keyword) ||
                (x.Country ?? "").Contains(keyword) ||
                (x.PostalCode ?? "").Contains(keyword) ||
                (x.Email ?? "").Contains(keyword) ||
                (x.Phone ?? "").Contains(keyword) ||
                (x.Pharmacy != null
                    ? (x.Pharmacy.Name ?? "")
                    : "").Contains(keyword));
        }

        var total = await query.CountAsync();

        var rows = await query
            .OrderBy(x => x.PharmacyId)
            .ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.PharmacyId,
                PharmacyName = x.Pharmacy != null
                    ? x.Pharmacy.Name
                    : string.Empty,
                BranchName = x.Name,
                x.Address,
                x.City,
                x.State,
                x.District,
                x.Country,
                x.PostalCode,
                x.Phone,
                x.Email,
                x.IsActive,
                x.CreatedAt
            })
            .ToListAsync();

        var branches = rows.Select(x => new
        {
            x.Id,
            x.PharmacyId,
            x.PharmacyName,
            x.BranchName,

            Location = string.Join(
                ", ",
                new[]
                {
                    x.Address,
                    x.City,
                    x.District,
                    x.State,
                    x.Country,
                    x.PostalCode
                }
                .Where(v => !string.IsNullOrWhiteSpace(v))),

            Contact = x.Phone ?? string.Empty,
            Email = x.Email ?? string.Empty,
            Status = x.IsActive ? "Active" : "Inactive",
            x.IsActive,
            x.CreatedAt
        }).ToList();

        return Ok(new
        {
            Success = true,
            Total = total,
            Page = page,
            PageSize = pageSize,
            Branches = branches
        });
    }


    // ============================================================
    // CREATE STANDALONE PHARMACY BRANCH
    // POST /api/pharmacy-super-admin/branches
    //
    // Super Admin version. The Pharmacy Admin controller should
    // derive PharmacyId from the authenticated Admin instead.
    // ============================================================

    // Super Admin cannot create branches. Pharmacy Admin owns branch creation.

    [HttpGet("branches/{id:int}")]
    public async Task<IActionResult> GetBranch(int id)
    {
        var branch = await _db.Branches
            .AsNoTracking()
            .Where(x => x.Id == id && x.PharmacyId.HasValue)
            .Select(x => new
            {
                x.Id,
                x.PharmacyId,
                PharmacyName = x.Pharmacy != null
                    ? x.Pharmacy.Name
                    : string.Empty,
                BranchName = x.Name,
                x.Phone,
                x.Email,
                x.Address,
                x.City,
                x.State,
                x.District,
                x.Country,
                x.PostalCode,
                x.IsActive,
                x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (branch == null)
            return NotFound(new
            {
                Success = false,
                Message = "Pharmacy branch was not found."
            });

        return Ok(new
        {
            Success = true,
            Branch = branch
        });
    }


    // ============================================================
    // UPDATE STANDALONE PHARMACY BRANCH
    // PUT /api/pharmacy-super-admin/branches/{id}
    // ============================================================

    


    // ============================================================
    // BRANCH STATUS
    // PATCH /api/pharmacy-super-admin/branches/{id}/status
    // ============================================================

    


    // ============================================================
    // DELETE STANDALONE PHARMACY BRANCH
    // DELETE /api/pharmacy-super-admin/branches/{id}
    // ============================================================

    




    // ============================================================
    // MEDICINES FOR SUPER ADMIN
    // GET /api/pharmacy-super-admin/medicines
    //
    // This endpoint avoids the scoped MedicineController
    // permission problem shown in the frontend.
    // ============================================================

    

    // ============================================================
    // MEDICINE STATUS
    // PATCH /api/pharmacy-super-admin/medicines/{id}/status
    // ============================================================

    

    // ============================================================
    // PHARMACY + BRANCH OVERVIEW
    // GET /api/pharmacy-super-admin/pharmacies/overview
    // ============================================================

    [HttpGet("pharmacies/overview")]
    public async Task<IActionResult> PharmacyOverview()
    {
        var pharmacies = await _db.Pharmacies
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                PharmacyName = x.Name,
                x.Name,
                x.Email,
                x.Phone,
                x.Address,
                x.City,
                x.State,
                x.District,
                x.Country,
                x.PostalCode,
                x.IsActive,
                x.CreatedAt,

                Branches = x.Branches
                    .OrderBy(b => b.Name)
                    .Select(b => new
                    {
                        b.Id,
                        BranchName = b.Name,
                        b.Phone,
                        b.Email,
                        b.Address,
                        b.City,
                        b.State,
                        b.District,
                        b.Country,
                        b.PostalCode,
                        b.IsActive,
                        Status = b.IsActive ? "Active" : "Inactive",
                        b.CreatedAt
                    })
                    .ToList(),

                Admins = _db.Users
                    .Where(u =>
                        u.Role == "PharmacyAdmin" &&
                        u.PharmacyId == x.Id)
                    .OrderBy(u => u.Name)
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.MobileNumber,
                        u.IsActive,
                        u.AccountStatus,
                        Status = u.IsActive ? "Active" : "Inactive"
                    })
                    .ToList()
            })
            .ToListAsync();

        var result = pharmacies.Select(x => new
        {
            x.Id,
            x.PharmacyName,
            x.Name,
            x.Email,
            x.Phone,
            x.Address,
            x.City,
            x.State,
            x.District,
            x.Country,
            x.PostalCode,
            x.IsActive,
            x.CreatedAt,

            TotalBranches = x.Branches.Count,
            ActiveBranches = x.Branches.Count(b => b.IsActive),
            InactiveBranches = x.Branches.Count(b => !b.IsActive),

            TotalAdmins = x.Admins.Count,
            x.Admins,
            x.Branches
        }).ToList();

        return Ok(new
        {
            Success = true,
            TotalPharmacies = result.Count,
            TotalBranches = result.Sum(x => x.TotalBranches),
            Pharmacies = result
        });
    }


    // ============================================================
    // CREATE PHARMACY ADMIN
    // POST /api/pharmacy-super-admin/admins
    //
    // Creates the standalone PharmacyTenant and its Admin.
    // It does NOT create a Hospital or Branch.
    // ============================================================

    [HttpPost("admins")]
    public async Task<IActionResult> CreateAdmin([FromBody] CreatePharmacyAdminDto dto)
    {
        if (dto == null) return BadRequest(new { Success = false, Message = "Request body is required." });
        var name = dto.Name?.Trim() ?? string.Empty;
        var email = dto.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var mobile = string.IsNullOrWhiteSpace(dto.MobileNumber) ? null : dto.MobileNumber.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)) return BadRequest(new { Success = false, Message = "Admin name and email are required." });
        if (await _db.Users.AnyAsync(x => x.Email != null && x.Email.ToLower() == email)) return Conflict(new { Success = false, Message = "Email already exists." });
        if (mobile != null && await _db.Users.AnyAsync(x => x.MobileNumber == mobile)) return Conflict(new { Success = false, Message = "Mobile number already exists." });

        // Permissions are user-specific in this PMS.
        // No permission role/template is required for authorization.
        var temporaryPassword = PasswordGenerator.Generate(16);
        
var admin = new User
{
    Name = name,
    Email = email,
    MobileNumber = mobile,

    // Pharmacy Admin personal address
    Address = dto.Address?.Trim(),
    City = dto.City?.Trim(),
    State = dto.State?.Trim(),
    Country = dto.Country?.Trim(),
    PostalCode = dto.PostalCode?.Trim(),

    PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
    Role = "PharmacyAdmin",

    // Pharmacy will be assigned separately
    PharmacyId = null,

    BranchId = null,

    AccountStatus = "Pending",
    CreatedByUserId = CurrentUserId(),
    IsActive = true,
    MustChangePassword = true,
    CreatedAt = DateTime.UtcNow
};

        _db.Users.Add(admin);
        AddAudit("Create Pharmacy Admin", $"Unassigned Pharmacy Admin '{email}' created.");
        await _db.SaveChangesAsync();

        // Every new Pharmacy Admin starts with all applicable user permissions TRUE.
        // Super Admin can change these later per user.
        var adminDefaultPermissions = PermissionModules
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(module => new UserPermission
            {
                UserId = admin.Id,

                Module = module,
                CanView = true,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true,
                AssignedByUserId = CurrentUserId(),
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        _db.UserPermissions.AddRange(adminDefaultPermissions);
        await _db.SaveChangesAsync();

        var loginUrl = _configuration["PharmacyFrontend:LoginUrl"];
        var emailSent = false; string? emailWarning = null;
        if (string.IsNullOrWhiteSpace(loginUrl)) emailWarning = "Admin created, but frontend login URL is not configured.";
        else try { await _emailService.SendPharmacyAdminWelcomeEmailAsync(admin.Email!, admin.Name ?? "Pharmacy Admin", temporaryPassword, loginUrl); emailSent = true; }

            catch (Exception ex)
            {
                Console.WriteLine(
                    "EMAIL SENDING FAILED: " + ex.ToString());

                emailWarning =
                    "Admin created, but welcome email could not be sent.";
            }

        return CreatedAtAction(nameof(GetAdmin), new { id = admin.Id }, new
        {
            Success = true,
            Message = emailSent ? "Pharmacy Admin created. Temporary login credentials were emailed. Assign a pharmacy to activate pharmacy access." : "Pharmacy Admin created. Assign a pharmacy to activate pharmacy access. " + emailWarning,
            EmailSent = emailSent,
            Admin = new { admin.Id, AdminName = admin.Name, admin.Email, admin.MobileNumber, admin.Role, admin.PharmacyId, admin.AccountStatus, admin.MustChangePassword }
        });
    }

    // ============================================================
    // GET ALL PHARMACY ADMINS
    // GET /api/pharmacy-super-admin/admins
    // ============================================================

    [HttpGet("admins")]
    public async Task<IActionResult> GetAdmins()
    {
        var admins = await _db.Users
            .AsNoTracking()
            .Where(x => x.Role == "PharmacyAdmin")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.Role,
                x.PharmacyId,
                PharmacyName = x.Pharmacy != null ? x.Pharmacy.Name : string.Empty,
                Pharmacy = x.Pharmacy == null ? null : new
                {
                    x.Pharmacy.Id,
                    x.Pharmacy.Name,
                    x.Pharmacy.Email,
                    x.Pharmacy.Phone,
                    x.Pharmacy.Address,
                    x.Pharmacy.City,
                    x.Pharmacy.State,
                    x.Pharmacy.District,
                    x.Pharmacy.Country,
                    x.Pharmacy.PostalCode,
                    x.Pharmacy.IsActive,
                    x.Pharmacy.CreatedAt,
                    x.Pharmacy.UpdatedAt
                },
                BranchId = x.BranchId.HasValue && x.PharmacyId.HasValue
                    ? _db.PharmacyBranchIdentities
                        .Where(m => m.PharmacyId == x.PharmacyId.Value && m.ExistingBranchId == x.BranchId.Value)
                        .Select(m => (int?)m.BranchId).SingleOrDefault()
                    : null,
                BranchName = x.Branch != null ? x.Branch.Name : string.Empty,
                Branch = x.Branch == null ? null : new
                {
                    Id = _db.PharmacyBranchIdentities
                        .Where(m => m.PharmacyId == x.Branch.PharmacyId && m.ExistingBranchId == x.Branch.Id)
                        .Select(m => (int?)m.BranchId).SingleOrDefault(),
                    x.Branch.Name,
                    x.Branch.Phone,
                    x.Branch.Email,
                    x.Branch.Address,
                    x.Branch.City,
                    x.Branch.State,
                    x.Branch.District,
                    x.Branch.Country,
                    x.Branch.PostalCode,
                    x.Branch.IsMainBranch,
                    x.Branch.IsActive
                },
                x.IsActive,
                x.MustChangePassword,
                x.AccountStatus,
                IsPharmacyAssigned = x.PharmacyId.HasValue && x.AccountStatus == "Active" && x.IsActive,
                PermissionCount = _db.UserPermissions.Count(p => p.UserId == x.Id),
                x.CreatedAt
            })
            .ToListAsync();

        if (admins.Any(x => x.BranchId == null && x.Branch != null || x.Branch?.Id == null && x.Branch != null))
            return StatusCode(500, new { Success = false, Message = "A branch identity mapping is missing. Contact the administrator." });

        return Ok(new { Success = true, Total = admins.Count, Admins = admins });
    }

    // ============================================================
    // GET PHARMACY ADMIN
    // GET /api/pharmacy-super-admin/admins/{id}
    // ============================================================

    [HttpGet("admins/{id:int}")]
    public async Task<IActionResult> GetAdmin(int id)
    {
        var admin = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == id && x.Role == "PharmacyAdmin")
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.Role,
                x.PharmacyId,
                PharmacyName = x.Pharmacy != null ? x.Pharmacy.Name : string.Empty,
                Pharmacy = x.Pharmacy == null ? null : new
                {
                    x.Pharmacy.Id,
                    x.Pharmacy.Name,
                    x.Pharmacy.Email,
                    x.Pharmacy.Phone,
                    x.Pharmacy.Address,
                    x.Pharmacy.City,
                    x.Pharmacy.State,
                    x.Pharmacy.District,
                    x.Pharmacy.Country,
                    x.Pharmacy.PostalCode,
                    x.Pharmacy.IsActive,
                    x.Pharmacy.CreatedAt,
                    x.Pharmacy.UpdatedAt
                },
                BranchId = x.BranchId.HasValue && x.PharmacyId.HasValue
                    ? _db.PharmacyBranchIdentities
                        .Where(m => m.PharmacyId == x.PharmacyId.Value && m.ExistingBranchId == x.BranchId.Value)
                        .Select(m => (int?)m.BranchId).SingleOrDefault()
                    : null,
                BranchName = x.Branch != null ? x.Branch.Name : string.Empty,
                Branch = x.Branch == null ? null : new
                {
                    Id = _db.PharmacyBranchIdentities
                        .Where(m => m.PharmacyId == x.Branch.PharmacyId && m.ExistingBranchId == x.Branch.Id)
                        .Select(m => (int?)m.BranchId).SingleOrDefault(),
                    x.Branch.Name,
                    x.Branch.Phone,
                    x.Branch.Email,
                    x.Branch.Address,
                    x.Branch.City,
                    x.Branch.State,
                    x.Branch.District,
                    x.Branch.Country,
                    x.Branch.PostalCode,
                    x.Branch.IsMainBranch,
                    x.Branch.IsActive
                },
                x.IsActive,
                x.MustChangePassword,
                x.AccountStatus,
                IsPharmacyAssigned = x.PharmacyId.HasValue && x.AccountStatus == "Active" && x.IsActive,
                Permissions = _db.UserPermissions
                    .Where(p => p.UserId == x.Id)
                    .OrderBy(p => p.Module)
                    .Select(p => new { p.Module, p.CanView, p.CanCreate, p.CanEdit, p.CanDelete, p.UpdatedAt })
                    .ToList(),
                x.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (admin == null)
            return NotFound(new { Success = false, Message = "Pharmacy Admin was not found." });

        if (admin.Branch != null && (admin.BranchId == null || admin.Branch.Id == null))
            return StatusCode(500, new { Success = false, Message = "A branch identity mapping is missing. Contact the administrator." });

        return Ok(new { Success = true, Admin = admin });
    }

    // ============================================================
    // USER-SPECIFIC PHARMACY ADMIN PERMISSIONS
    // Super Admin controls exactly what this Admin can access.
    // ============================================================
    [HttpGet("admins/{id:int}/permissions")]
    public async Task<IActionResult> GetAdminPermissions(int id)
    {
        var admin = await _db.Users.AsNoTracking()
            .Where(x => x.Id == id && x.Role == "PharmacyAdmin")
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Email,
                x.MobileNumber,
                x.Role,
                x.PharmacyId,
                PharmacyName = x.Pharmacy != null ? x.Pharmacy.Name : string.Empty
            })
            .FirstOrDefaultAsync();

        if (admin == null)
            return NotFound(new { Success = false, Message = "Pharmacy Admin was not found." });

        var saved = await _db.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == id).ToListAsync();

        var permissions = PermissionModules.Select(module =>
        {
            var p = saved.FirstOrDefault(x => string.Equals(x.Module, module, StringComparison.OrdinalIgnoreCase));
            return new
            {
                Module = module,
                CanView = p?.CanView ?? true,
                CanCreate = p?.CanCreate ?? true,
                CanEdit = p?.CanEdit ?? true,
                CanDelete = p?.CanDelete ?? true,
                UpdatedAt = p?.UpdatedAt
            };
        }).ToList();

        return Ok(new
        {
            Success = true,
            UserId = admin.Id,
            Name = admin.Name,
            Email = admin.Email,
            MobileNumber = admin.MobileNumber,
            Role = admin.Role,
            PharmacyId = admin.PharmacyId,
            PharmacyName = admin.PharmacyName,
            Permissions = permissions
        });
    }

    [HttpPut("admins/{id:int}/permissions")]
    public async Task<IActionResult> SaveAdminPermissions(int id, [FromBody] List<UserPermissionUpdateDto> dto)
    {
        var admin = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == "PharmacyAdmin");
        if (admin == null) return NotFound(new { Success = false, Message = "Pharmacy Admin was not found." });
        if (dto == null) return BadRequest(new { Success = false, Message = "Permissions are required." });

        var invalid = dto.Where(x => string.IsNullOrWhiteSpace(x.Module) ||
            !PermissionModules.Contains(x.Module.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Module).ToList();
        if (invalid.Count > 0) return BadRequest(new { Success = false, Message = "Invalid permission module.", Modules = invalid });

        var duplicates = dto.Where(x => !string.IsNullOrWhiteSpace(x.Module))
            .GroupBy(x => x.Module.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0) return BadRequest(new { Success = false, Message = "Duplicate permission modules are not allowed.", Modules = duplicates });

        var existing = await _db.UserPermissions.Where(x => x.UserId == id).ToListAsync();
        var byModule = existing.ToDictionary(x => x.Module, StringComparer.OrdinalIgnoreCase);
        var requested = dto.ToDictionary(x => x.Module.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var module in PermissionModules)
        {
            var values = requested.TryGetValue(module, out var supplied) ? supplied : null;
            if (!byModule.TryGetValue(module, out var permission))
            {
                permission = new UserPermission { UserId = id, Module = module };
                _db.UserPermissions.Add(permission);
            }
            // Missing module in the request must NOT become false.
            // New/missing rows start TRUE; existing omitted rows keep their values.
            if (values != null)
            {
                permission.CanView = values.CanView;
                permission.CanCreate = values.CanCreate;
                permission.CanEdit = values.CanEdit;
                permission.CanDelete = values.CanDelete;
            }
            else if (!byModule.ContainsKey(module))
            {
                permission.CanView = true;
                permission.CanCreate = true;
                permission.CanEdit = true;
                permission.CanDelete = true;
            }
            permission.AssignedByUserId = CurrentUserId();
            permission.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new
        {
            Success = true,
            Message = "Pharmacy Admin user permissions updated successfully.",
            UserId = admin.Id,
            Name = admin.Name
        });
    }

    // ============================================================
    // UPDATE PHARMACY ADMIN
    // PUT /api/pharmacy-super-admin/admins/{id}
    // ============================================================

    [HttpPut("admins/{id:int}")]
    public async Task<IActionResult> UpdateAdmin(
      int id,
      [FromBody] UpdatePharmacyAdminDto dto)
    {
        var admin =
            await _db.Users.FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "PharmacyAdmin");

        if (admin == null)
        {
            return NotFound(new
            {
                Success = false,
                Message = "Pharmacy Admin was not found."
            });
        }

        // These fields are nullable in the shared update DTO; reject missing
        // values instead of throwing and returning HTTP 500.
        if (string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Email))
        {
            return BadRequest(new
            {
                Success = false,
                Message = "Name and email are required when updating a Pharmacy Admin."
            });
        }

        var normalizedEmail =
            dto.Email.Trim().ToLowerInvariant();

        var normalizedMobile =
            dto.MobileNumber?.Trim();

        var emailExists =
            await _db.Users.AnyAsync(x =>
                x.Id != id &&
                x.Email != null &&
                x.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return Conflict(new
            {
                Success = false,
                Message = "Email already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(normalizedMobile))
        {
            var mobileExists =
                await _db.Users.AnyAsync(x =>
                    x.Id != id &&
                    x.MobileNumber == normalizedMobile);

            if (mobileExists)
            {
                return Conflict(new
                {
                    Success = false,
                    Message = "Mobile number already exists."
                });
            }
        }

        admin.Name = dto.Name.Trim();
        admin.Email = normalizedEmail;
        admin.MobileNumber = normalizedMobile;
        admin.IsActive = dto.IsActive ?? admin.IsActive;

        AddAudit(
            "Update Pharmacy Admin",
            $"Pharmacy Admin ID {id} updated",
            branchId: admin.BranchId);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Pharmacy Admin updated successfully."
        });
    }

    // ============================================================
    // DELETE / DEACTIVATE PHARMACY ADMIN
    // DELETE /api/pharmacy-super-admin/admins/{id}
    // ============================================================

    [HttpDelete("admins/{id:int}")]
    public async Task<IActionResult> DeleteAdmin(
        int id)
    {
        var admin =
            await _db.Users.FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "PharmacyAdmin");

        if (admin == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Pharmacy Admin was not found."
            });
        }

        if (!admin.IsActive)
            return NoContent();

        admin.IsActive = false;

        AddAudit(
            "Deactivate Pharmacy Admin",
            $"Pharmacy Admin ID {id} deactivated",
            branchId: admin.BranchId);

        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ============================================================
    // ASSIGN PHARMACY + BRANCH TO PHARMACY ADMIN
    // PUT /api/pharmacy-super-admin/admins/{id}/pharmacy-assignment
    // ============================================================

    // Manual assignment removed: registration assigns the logged-in Admin automatically.

    [HttpPatch("admins/{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(
        int id,
        [FromBody] StatusDto dto)
    {
        var admin =
            await _db.Users.FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "PharmacyAdmin");

        if (admin == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Pharmacy Admin was not found."
            });
        }

        if (dto.IsActive &&
            admin.PharmacyId.HasValue &&
            admin.BranchId.HasValue)
        {
            var anotherActiveAdmin =
                await _db.Users.AnyAsync(x =>
                    x.Id != id &&
                    x.Role ==
                    "PharmacyAdmin" &&
                    x.PharmacyId ==
                    admin.PharmacyId &&
                    x.BranchId ==
                    admin.BranchId &&
                    x.IsActive);

            if (anotherActiveAdmin)
            {
                return Conflict(new
                {
                    Success = false,

                    Message =
                        "Another active Pharmacy Admin already exists for this branch."
                });
            }
        }

        admin.IsActive = dto.IsActive;

        AddAudit(
            "Change Admin Status",
            $"Pharmacy Admin ID {id} changed to {(dto.IsActive ? "Active" : "Inactive")}",
            branchId: admin.BranchId);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message = dto.IsActive
                ? "Pharmacy Admin activated successfully."
                : "Pharmacy Admin deactivated successfully."
        });
    }

    // ============================================================
    // RESET ADMIN PASSWORD
    // POST /api/pharmacy-super-admin/admins/{id}/reset-password
    // ============================================================

    [HttpPost(
        "admins/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetPasswordDto dto)
    {
        var admin =
            await _db.Users.FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.Role == "PharmacyAdmin");

        if (admin == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Pharmacy Admin was not found."
            });
        }

        if (!IsStrongPassword(dto.NewPassword))
        {
            return BadRequest(new
            {
                Success = false,

                Message =
                    "Password must contain at least 8 characters, one uppercase letter, one lowercase letter, one number and one special character."
            });
        }

        admin.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        admin.MustChangePassword = true;

        admin.PasswordResetOtpHash = null;
        admin.PasswordResetOtpExpiresAt = null;
        admin.PasswordResetOtpLastSentAt = null;
        admin.PasswordResetOtpAttempts = 0;
        admin.PasswordResetTokenHash = null;
        admin.PasswordResetTokenExpiresAt = null;

        AddAudit(
            "Reset Admin Password",
            $"Password reset for Pharmacy Admin ID {id}",
            branchId: admin.BranchId);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "Pharmacy Admin password reset successfully."
        });
    }

    // ============================================================
    // SYSTEM SETTINGS
    // GET /api/pharmacy-super-admin/settings
    // ============================================================

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var settings =
            await _db.PharmacySystemSettings
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

        if (settings == null)
        {
            return Ok(new
            {
                Success = true,

                Settings = new
                {
                    AppName = "PMS",

                    Timezone =
                        "Asia/Kolkata",

                    Currency = "INR",

                    IsEnabled = true,

                    Status = "Enabled",

                    ConfigurationNotes =
                        string.Empty
                }
            });
        }

        return Ok(new
        {
            Success = true,

            Settings = new
            {
                settings.Id,

                settings.AppName,

                settings.Timezone,

                settings.Currency,

                settings.IsEnabled,

                Status = settings.IsEnabled
                    ? "Enabled"
                    : "Disabled",

                settings.ConfigurationNotes,

                settings.UpdatedAt,

                settings.UpdatedByUserId
            }
        });
    }

    // ============================================================
    // SAVE SYSTEM SETTINGS
    // PUT /api/pharmacy-super-admin/settings
    // ============================================================

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(
        [FromBody] SuperAdminSettingsDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.AppName))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Application name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Timezone))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Timezone is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Currency))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Currency is required."
            });
        }

        var settings =
            await _db.PharmacySystemSettings
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings =
                new PharmacySystemSetting();

            _db.PharmacySystemSettings
                .Add(settings);
        }

        settings.AppName =
            dto.AppName.Trim();

        settings.Timezone =
            dto.Timezone.Trim();

        settings.Currency =
            dto.Currency
                .Trim()
                .ToUpperInvariant();

        settings.IsEnabled =
            dto.IsEnabled;

        settings.ConfigurationNotes =
            dto.ConfigurationNotes?.Trim();

        settings.UpdatedByUserId =
            CurrentUserId();

        settings.UpdatedAt =
            DateTime.UtcNow;

        AddAudit(
            "Update System Settings",
            "Pharmacy Super Admin updated system settings");

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "System settings saved successfully.",

            Settings = new
            {
                settings.AppName,
                settings.Timezone,
                settings.Currency,
                settings.IsEnabled,

                Status = settings.IsEnabled
                    ? "Enabled"
                    : "Disabled",

                settings.ConfigurationNotes
            }
        });
    }

    // ============================================================
    // NOTIFICATIONS
    // GET /api/pharmacy-super-admin/notifications
    // ============================================================

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(
        [FromQuery] string status = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedStatus =
            string.IsNullOrWhiteSpace(status)
                ? "all"
                : status.Trim().ToLowerInvariant();

        var validStatuses =
            new[]
            {
            "all",
            "unread",
            "read",
            "sent"
            };

        if (!validStatuses.Contains(normalizedStatus))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Status must be all, unread, read or sent."
            });
        }

        var query =
            _db.PharmacyNotifications
                .AsNoTracking()
                .AsQueryable();

        switch (normalizedStatus)
        {
            case "unread":

                query = query.Where(x =>
                    !x.IsRead);

                break;

            case "read":

                query = query.Where(x =>
                    x.IsRead);

                break;

            case "sent":

                query = query.Where(x =>
                    x.IsSent);

                break;
        }

        var total =
            await query.CountAsync();

        var notifications =
            await query
                .OrderByDescending(x =>
                    x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,

                    x.Title,

                    x.Message,

                    x.RecipientType,

                    // Standalone PMS: restrict audit queries to pharmacy-owned identifiers
                    // and do not join external tenant tables.
                    x.BranchId,

                    BranchName =
                        x.BranchId.HasValue
                            ? _db.Branches
                                .Where(b =>
                                    b.Id ==
                                    x.BranchId.Value)
                                .Select(b =>
                                    b.Name)
                                .FirstOrDefault()
                            : null,

                    x.UserId,

                    UserName =
                        x.UserId.HasValue
                            ? _db.Users
                                .Where(u =>
                                    u.Id ==
                                    x.UserId.Value)
                                .Select(u =>
                                    u.Name)
                                .FirstOrDefault()
                            : null,

                    x.IsRead,

                    x.IsSent,

                    x.CreatedAt,

                    x.ReadAt
                })
                .ToListAsync();

        var allCount =
            await _db.PharmacyNotifications
                .CountAsync();

        var unreadCount =
            await _db.PharmacyNotifications
                .CountAsync(x =>
                    !x.IsRead);

        var readCount =
            await _db.PharmacyNotifications
                .CountAsync(x =>
                    x.IsRead);

        var sentCount =
            await _db.PharmacyNotifications
                .CountAsync(x =>
                    x.IsSent);

        return Ok(new
        {
            Success = true,

            Counts = new
            {
                All = allCount,
                Unread = unreadCount,
                Read = readCount,
                Sent = sentCount
            },

            Total = total,

            Page = page,

            PageSize = pageSize,

            TotalPages =
                total == 0
                    ? 0
                    : (int)Math.Ceiling(
                        total /
                        (double)pageSize),

            Notifications =
                notifications
        });
    }

    // ============================================================
    // SEND NOTIFICATION
    // POST /api/pharmacy-super-admin/notifications
    // ============================================================

    [HttpPost("notifications")]
    public async Task<IActionResult> SendNotification(
        [FromBody] SendPharmacyNotificationDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.Title))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Notification title is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.Message))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Notification message is required."
            });
        }

        var recipientType =
            string.IsNullOrWhiteSpace(
                dto.RecipientType)
                ? "All"
                : dto.RecipientType.Trim();

        var validRecipientTypes =
            new[]
            {
            "All",
            "Branch",
            "User"
            };

        var normalizedRecipientType =
            validRecipientTypes
                .FirstOrDefault(x =>
                    x.Equals(
                        recipientType,
                        StringComparison.OrdinalIgnoreCase));

        if (normalizedRecipientType == null)
        {
            return BadRequest(new
            {
                Success = false,

                Message =
                    "RecipientType must be All, Branch or User."
            });
        }

        int? branchId = null;
        int? userId = null;

        // ========================================================
        // BRANCH RECIPIENT
        // ========================================================

        if (normalizedRecipientType == "Branch")
        {
            if (!dto.BranchId.HasValue)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "BranchId is required."
                });
            }

            var branch =
                await _db.Branches
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                        dto.BranchId.Value)
                    .Select(x => new
                    {
                        x.Id
                    })
                    .FirstOrDefaultAsync();

            if (branch == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Valid branch is required."
                });
            }

            branchId =
                branch.Id;

        }

        // ========================================================
        // USER RECIPIENT
        // ========================================================

        if (normalizedRecipientType == "User")
        {
            if (!dto.UserId.HasValue)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "UserId is required."
                });
            }

            var user =
                await _db.Users
                    .AsNoTracking()
                    .Where(x =>
                        x.Id ==
                        dto.UserId.Value)
                    .Select(x => new
                    {
                        x.Id,
                        x.BranchId,
                        x.IsActive
                    })
                    .FirstOrDefaultAsync();

            if (user == null ||
                !user.IsActive)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Valid active user is required."
                });
            }

            userId =
                user.Id;


            branchId =
                user.BranchId;
        }

        // ========================================================
        // ALL RECIPIENTS
        // ========================================================

        if (normalizedRecipientType == "All")
        {
            branchId = null;
            userId = null;
        }

        var notification =
            new PharmacyNotification
            {
                Title =
                    dto.Title.Trim(),

                Message =
                    dto.Message.Trim(),

                RecipientType =
                    normalizedRecipientType,

                // Legacy nullable column retained until schema migration.

                BranchId =
                    branchId,

                UserId =
                    userId,

                IsRead =
                    false,

                IsSent =
                    true,

                CreatedByUserId =
                    CurrentUserId(),

                CreatedAt =
                    DateTime.UtcNow,

                ReadAt =
                    null
            };

        _db.PharmacyNotifications.Add(
            notification);

        AddAudit(
            "Send Notification",
            $"Notification sent: {notification.Title}",
            branchId: notification.BranchId);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "Notification sent successfully.",

            Notification = new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.RecipientType,
                notification.BranchId,
                notification.UserId,
                notification.IsSent,
                notification.IsRead,
                notification.CreatedAt
            }
        });
    }

    // ============================================================
    // MARK ONE NOTIFICATION AS READ
    // PATCH /api/pharmacy-super-admin/notifications/{id}/read
    // ============================================================

    [HttpPatch("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkNotificationRead(
        int id)
    {
        var notification =
            await _db.PharmacyNotifications
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (notification == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Notification was not found."
            });
        }

        if (!notification.IsRead)
        {
            notification.IsRead =
                true;

            notification.ReadAt =
                DateTime.UtcNow;

            AddAudit(
                "Read Notification",
                $"Notification ID {id} marked as read",
                branchId: notification.BranchId);

            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            Success = true,

            Message =
                "Notification marked as read.",

            Notification = new
            {
                notification.Id,
                notification.IsRead,
                notification.ReadAt
            }
        });
    }

    // ============================================================
    // MARK ALL NOTIFICATIONS AS READ
    // PATCH /api/pharmacy-super-admin/notifications/mark-all-read
    // ============================================================

    [HttpPatch("notifications/mark-all-read")]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        var unreadNotifications =
            await _db.PharmacyNotifications
                .Where(x =>
                    !x.IsRead)
                .ToListAsync();

        if (unreadNotifications.Count == 0)
        {
            return Ok(new
            {
                Success = true,
                Message =
                    "There are no unread notifications.",
                UpdatedCount = 0
            });
        }

        var readAt =
            DateTime.UtcNow;

        foreach (var notification in
                 unreadNotifications)
        {
            notification.IsRead =
                true;

            notification.ReadAt =
                readAt;
        }

        AddAudit(
            "Read Notifications",
            $"{unreadNotifications.Count} notifications marked as read");

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "All notifications marked as read.",

            UpdatedCount =
                unreadNotifications.Count
        });
    }

    // ============================================================
    // DELETE NOTIFICATION
    // DELETE /api/pharmacy-super-admin/notifications/{id}
    // ============================================================

    [HttpDelete("notifications/{id:int}")]
    public async Task<IActionResult> DeleteNotification(
        int id)
    {
        var notification =
            await _db.PharmacyNotifications
                .FirstOrDefaultAsync(x =>
                    x.Id == id);

        if (notification == null)
        {
            return NotFound(new
            {
                Success = false,

                Message =
                    "Notification was not found."
            });
        }

        var branchId =
            notification.BranchId;

        _db.PharmacyNotifications.Remove(
            notification);

        AddAudit(
            "Delete Notification",
            $"Notification ID {id} deleted",
            branchId: branchId);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "Notification deleted successfully."
        });
    }

    // ============================================================
    // AUDIT LOG SUMMARY
    // GET /api/pharmacy-super-admin/audit-logs/summary
    // ============================================================

    [HttpGet("audit-logs/summary")]
    public async Task<IActionResult> AuditLogSummary(
        [FromQuery] string? search,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? branchId,
        [FromQuery] string? activityType,
        [FromQuery] string? role,
        [FromQuery] string? action,
        [FromQuery] bool? isOnline)
    {
        var validationError =
            await ValidateAuditFilters(
                startDate,
                endDate,
                branchId,
                activityType);

        if (validationError != null)
            return validationError;

        var query =
            BuildAuditLogQuery(
                search,
                startDate,
                endDate,
                branchId,
                activityType,
                role,
                action,
                isOnline);

        var total =
            await query.CountAsync();

        var logins =
            await query.CountAsync(x =>
                x.IsLoginActivity);

        var dataChanges =
            await query.CountAsync(x =>
                !x.IsLoginActivity);

        var onlineSessions =
            await query.CountAsync(x =>
                x.IsLoginActivity &&
                x.IsOnline);

        return Ok(new
        {
            Success = true,

            PharmacyAuditRecords =
                total,

            StaffLoginActivities =
                logins,

            InventoryAndDataChanges =
                dataChanges,

            OnlineSessions =
                onlineSessions
        });
    }

    // ============================================================
    // AUDIT LOG LIST
    // GET /api/pharmacy-super-admin/audit-logs
    //
    // activityType:
    // all   -> all audit records
    // login -> login history only
    // data  -> non-login / system changes only
    //
    // loginOnly is kept for backward compatibility with existing
    // frontend/API calls.
    // ============================================================

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs(
        [FromQuery] string? search,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? branchId,
        [FromQuery] bool? loginOnly,
        [FromQuery] string? activityType,
        [FromQuery] string? role,
        [FromQuery] string? action,
        [FromQuery] bool? isOnline,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Backward compatibility:
        // ?loginOnly=true behaves the same as ?activityType=login.
        var normalizedActivityType =
            loginOnly == true
                ? "login"
                : activityType;

        var validationError =
            await ValidateAuditFilters(
                startDate,
                endDate,
                branchId,
                normalizedActivityType);

        if (validationError != null)
            return validationError;

        var query =
            BuildAuditLogQuery(
                search,
                startDate,
                endDate,
                branchId,
                normalizedActivityType,
                role,
                action,
                isOnline);

        var total =
            await query.CountAsync();

        var records =
            await query
                .OrderByDescending(x =>
                    x.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,

                    x.UserId,

                    // Prefer the live Users table so older audit rows
                    // containing "Unknown User" can still show the
                    // correct user name when UserId is valid.
                    UserName =
                        _db.Users
                            .Where(u =>
                                u.Id == x.UserId)
                            .Select(u =>
                                u.Name)
                            .FirstOrDefault()
                        ?? x.UserName
                        ?? "Unknown User",

                    Email =
                        _db.Users
                            .Where(u =>
                                u.Id == x.UserId)
                            .Select(u =>
                                u.Email)
                            .FirstOrDefault()
                        ?? string.Empty,

                    Action =
                        x.Action ?? string.Empty,

                    IpAddress =
                        x.IpAddress ?? string.Empty,

                    Timestamp =
                        x.Timestamp,

                    Role =
                        x.Role ?? string.Empty
                })
                .ToListAsync();

        return Ok(new
        {
            Success = true,

            Total = total,

            Page = page,

            PageSize = pageSize,

            TotalPages =
                total == 0
                    ? 0
                    : (int)Math.Ceiling(
                        total /
                        (double)pageSize),

            Records = records
        });
    }

    // ============================================================
    // AUDIT LOG FILTER HELPERS
    // ============================================================

    private IQueryable<AuditLog> BuildAuditLogQuery(
        string? search,
        DateTime? startDate,
        DateTime? endDate,
        int? branchId,
        string? activityType,
        string? role,
        string? action,
        bool? isOnline)
    {
        var query =
            _db.AuditLogs
                .AsNoTracking()
                .AsQueryable();

        if (startDate.HasValue)
        {
            var from =
                startDate.Value.Date;

            query = query.Where(x =>
                x.Timestamp >= from);
        }

        if (endDate.HasValue)
        {
            var until =
                endDate.Value.Date.AddDays(1);

            query = query.Where(x =>
                x.Timestamp < until);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x =>
                x.BranchId ==
                branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(activityType))
        {
            var type =
                activityType.Trim()
                    .ToLowerInvariant();

            if (type == "login")
            {
                query = query.Where(x =>
                    x.IsLoginActivity);
            }
            else if (type == "data")
            {
                query = query.Where(x =>
                    !x.IsLoginActivity);
            }
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleValue =
                role.Trim();

            query = query.Where(x =>
                x.Role ==
                roleValue);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionValue =
                action.Trim();

            query = query.Where(x =>
                x.Action.Contains(actionValue) ||
                (x.SystemAction ?? "")
                    .Contains(actionValue));
        }

        if (isOnline.HasValue)
        {
            query = query.Where(x =>
                x.IsLoginActivity &&
                x.IsOnline ==
                isOnline.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword =
                search.Trim();

            query = query.Where(x =>
                x.UserName.Contains(keyword) ||
                x.Action.Contains(keyword) ||
                (x.SystemAction ?? "")
                    .Contains(keyword) ||
                (x.IpAddress ?? "")
                    .Contains(keyword) ||
                (x.Role ?? "")
                    .Contains(keyword) ||
                (x.Branch != null
                    ? x.Branch.Name
                    : "")
                    .Contains(keyword) ||
                _db.Users.Any(u =>
                    u.Id == x.UserId &&
                    (u.Email ?? "")
                        .Contains(keyword)));
        }

        return query;
    }

    private async Task<IActionResult?> ValidateAuditFilters(
        DateTime? startDate,
        DateTime? endDate,
        int? branchId,
        string? activityType)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            endDate.Value.Date <
            startDate.Value.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        if (!string.IsNullOrWhiteSpace(activityType))
        {
            var type =
                activityType.Trim()
                    .ToLowerInvariant();

            if (type != "all" &&
                type != "login" &&
                type != "data")
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "ActivityType must be all, login or data."
                });
            }
        }

        if (branchId.HasValue)
        {
            var branchExists =
                await _db.Branches
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == branchId.Value &&
                        x.PharmacyId.HasValue);

            if (!branchExists)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "Pharmacy branch was not found."
                });
            }
        }

        return null;
    }

    // Standalone pharmacy-wide revenue. No branch details are exposed to Super Admin.
    // Revenue is based on paid pharmacy bills, grouped by owning pharmacy.
    [HttpGet("reports/revenue")]
    public async Task<IActionResult> PharmacyWiseRevenueReport(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? pharmacyId)
    {
        var today = DateTime.UtcNow.Date;
        var from = (startDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var through = (endDate ?? today).Date;
        if (through < from)
            return BadRequest(new { success = false, message = "End date must be on or after start date." });

        var pharmaciesQuery = _db.Pharmacies.AsNoTracking();
        if (pharmacyId.HasValue)
        {
            if (!await pharmaciesQuery.AnyAsync(p => p.Id == pharmacyId.Value))
                return NotFound(new { success = false, message = "Pharmacy not found." });
            pharmaciesQuery = pharmaciesQuery.Where(p => p.Id == pharmacyId.Value);
        }

        var totals = await _db.PharmacyBills.AsNoTracking()
            .Where(b => b.BillDate >= from && b.BillDate < through.AddDays(1)
                        && b.PaymentStatus == "Paid")
            .GroupBy(b => b.PharmacyId)
            .Select(g => new { PharmacyId = g.Key, Revenue = g.Sum(b => b.GrandTotal), PaidBillCount = g.Count() })
            .ToListAsync();
        var totalsByPharmacy = totals.ToDictionary(x => x.PharmacyId);
        var pharmacies = await pharmaciesQuery.OrderBy(p => p.Name)
            .Select(p => new { PharmacyId = p.Id, PharmacyName = p.Name, IsActive = p.IsActive })
            .ToListAsync();
        var rows = pharmacies.Select(p => new
        {
            p.PharmacyId,
            p.PharmacyName,
            p.IsActive,
            Revenue = totalsByPharmacy.TryGetValue(p.PharmacyId, out var t) ? t.Revenue : 0m,
            PaidBillCount = totalsByPharmacy.TryGetValue(p.PharmacyId, out var c) ? c.PaidBillCount : 0
        }).ToList();
        return Ok(new
        {
            success = true,
            startDate = from,
            endDate = through,
            summary = new { totalRevenue = rows.Sum(x => x.Revenue), totalPharmacies = rows.Count,
                            pharmaciesWithRevenue = rows.Count(x => x.PaidBillCount > 0) },
            pharmacyRevenue = rows
        });
    }


    // DASHBOARD DATA
    // GET /api/pharmacy-super-admin/dashboard
    //
    // Use this only if your separate dashboard controller
    // has not already been updated.
    // ============================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        // Super Admin sees operational counts only, never prices or sales.
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var pharmacies = _db.Pharmacies.AsNoTracking();
        var branches = _db.Branches.AsNoTracking().Where(b => b.PharmacyId.HasValue);
        var admins = _db.Users.AsNoTracking().Where(u => u.Role == "PharmacyAdmin");

        return Ok(new
        {
            Success = true,
            Summary = new
            {
                TotalPharmacies = await pharmacies.CountAsync(),
                ActivePharmacies = await pharmacies.CountAsync(p => p.IsActive && !p.IsDeleted),
                InactivePharmacies = await pharmacies.CountAsync(p => !p.IsActive || p.IsDeleted),
                TotalBranches = await branches.CountAsync(),
                ActiveBranches = await branches.CountAsync(b => b.IsActive),
                InactiveBranches = await branches.CountAsync(b => !b.IsActive),
                TotalAdmins = await admins.CountAsync(),
                ActiveAdmins = await admins.CountAsync(u => u.IsActive),
                InactiveAdmins = await admins.CountAsync(u => !u.IsActive),
                NewPharmaciesThisMonth = await pharmacies.CountAsync(p => p.CreatedAt >= monthStart)
            },
            Message = "Platform administration overview. Pharmacy financial information is restricted to pharmacy administrators."
        });
    }

    // Read-only pharmacy directory for the Super Admin dashboard.
    [HttpGet("dashboard/pharmacies")]
    public async Task<IActionResult> DashboardPharmacies(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.Pharmacies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(p => p.Name.Contains(keyword) ||
                (p.Email != null && p.Email.Contains(keyword)) ||
                (p.City != null && p.City.Contains(keyword)) ||
                _db.Users.Any(u => u.Role == "PharmacyAdmin" &&
                    u.PharmacyId == p.Id && u.Name != null && u.Name.Contains(keyword)));
        }
        if (isActive.HasValue)
            query = query.Where(p => (p.IsActive && !p.IsDeleted) == isActive.Value);

        var total = await query.CountAsync();
        var rows = await query.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new { p.Id, p.Name, p.City, p.State, p.CreatedAt, p.IsActive, p.IsDeleted })
            .ToListAsync();
        var ids = rows.Select(p => p.Id).ToList();
        var branchCounts = await _db.Branches.AsNoTracking()
            .Where(b => b.PharmacyId.HasValue && ids.Contains(b.PharmacyId.Value))
            .GroupBy(b => b.PharmacyId!.Value)
            .Select(g => new { PharmacyId = g.Key, Total = g.Count(), Active = g.Count(b => b.IsActive) })
            .ToListAsync();
        var assignedAdmins = await _db.Users.AsNoTracking()
            .Where(u => u.Role == "PharmacyAdmin" && u.PharmacyId.HasValue && ids.Contains(u.PharmacyId.Value))
            .Select(u => new { PharmacyId = u.PharmacyId!.Value, AdminName = u.Name, u.IsActive })
            .ToListAsync();
        var result = rows.Select(p => new
        {
            PharmacyId = p.Id,
            PharmacyName = p.Name,
            p.City,
            p.State,
            p.CreatedAt,
            IsActive = p.IsActive && !p.IsDeleted,
            Status = p.IsActive && !p.IsDeleted ? "Active" : "Inactive",
            TotalBranches = branchCounts.FirstOrDefault(b => b.PharmacyId == p.Id)?.Total ?? 0,
            ActiveBranches = branchCounts.FirstOrDefault(b => b.PharmacyId == p.Id)?.Active ?? 0,
            Admins = assignedAdmins.Where(a => a.PharmacyId == p.Id)
                .Select(a => new { a.AdminName, a.IsActive }).ToList()
        }).ToList();
        return Ok(new
        {
            Success = true,
            Total = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Pharmacies = result
        });
    }

    // ============================================================
    // PASSWORD VALIDATION
    // Matches frontend password rules
    // ============================================================

    private static bool IsStrongPassword(
        string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (password.Length < 8)
            return false;

        if (!password.Any(char.IsUpper))
            return false;

        if (!password.Any(char.IsLower))
            return false;

        if (!password.Any(char.IsDigit))
            return false;

        if (!password.Any(x =>
                !char.IsLetterOrDigit(x)))
            return false;

        return true;
    }
}

/// <summary>
/// Request model for updating direct per-user permissions.
/// This replaces the old role-permission DTO naming.
/// </summary>
public sealed class UserPermissionUpdateDto
{
    public string Module { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public sealed class AssignPharmacyDto
{
    public int PharmacyId { get; set; }
    public int BranchId { get; set; }
}


