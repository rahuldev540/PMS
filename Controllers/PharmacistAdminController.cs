using AuthDemo.Constants;
using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using AuthDemo.Services;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-admin/pharmacists")]
[Authorize(Roles = "PharmacyAdmin")]
public class PharmacistAdminController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public PharmacistAdminController(
        AppDbContext db,
        IEmailService emailService,
        IConfiguration configuration,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _emailService = emailService;
        _configuration = configuration;
        _permissionChecker = permissionChecker;
    }

    // API IDs are pharmacy-scoped. Users.Id remains the global auth/FK key.
    private async Task<int?> ResolvePharmacistUserIdAsync(int publicId)
    {
        return await _db.PharmacyPharmacistIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.PharmacistId == publicId)
            .Select(x => (int?)x.ExistingUserId).FirstOrDefaultAsync();
    }

    // =====================================================
    // COMMON PERMISSION CHECK
    // =====================================================

    private async Task<IActionResult?> CheckPermission(
        string module,
        string action)
    {
        var allowed =
            await _permissionChecker.HasPermissionAsync(
                UserId,
                module,
                action);

        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        $"You do not have {action} permission for {module}."
                });
        }

        return null;
    }

    // =====================================================
    // CREATE PHARMACIST
    // POST: /api/pharmacy-admin/pharmacists
    //
    // Users -> Create
    //
    // If permissions are supplied:
    // Users -> Edit
    //
    // If permissions are NOT supplied:
    // Default Pharmacist permissions are created as TRUE.
    // =====================================================

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePharmacistDto dto)
    {
        var permissionError =
            await CheckPermission(
                "Users",
                "Create");

        if (permissionError != null)
            return permissionError;

        // Creation always assigns all Pharmacist permissions TRUE.
        // dto.Permissions is ignored during creation; use the PUT permission
        // endpoint after creation to customize this individual Pharmacist.

        var normalizedEmail =
            dto.Email.Trim()
                .ToLowerInvariant();

        var normalizedMobile =
            dto.MobileNumber?.Trim();

        var adminPharmacyId =
            OptionalPharmacyId;

        if (!adminPharmacyId.HasValue)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "The Pharmacy Admin must have an active pharmacy assignment before creating pharmacists."
            });
        }

        var pharmacyIsValid =
            await _db.Pharmacies
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == adminPharmacyId.Value &&
                    x.IsActive);

        if (!pharmacyIsValid)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "The assigned pharmacy is inactive or invalid."
            });
        }

        var selectedBranch =
            await _db.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == dto.BranchId &&
                    x.PharmacyId == adminPharmacyId.Value &&
                    x.IsActive);

        if (selectedBranch == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "The selected branch is inactive, invalid, or does not belong to your pharmacy."
            });
        }

        // =================================================
        // EMAIL DUPLICATE CHECK
        // =================================================

        var emailExists =
            await _db.Users.AnyAsync(x =>
                x.Email != null &&
                x.Email.ToLower() ==
                normalizedEmail);

        if (emailExists)
        {
            return Conflict(new
            {
                Success = false,
                Message =
                    "Email already exists."
            });
        }

        // =================================================
        // MOBILE DUPLICATE CHECK
        // =================================================

        if (!string.IsNullOrWhiteSpace(
                normalizedMobile))
        {
            var mobileExists =
                await _db.Users.AnyAsync(x =>
                    x.MobileNumber ==
                    normalizedMobile);

            if (mobileExists)
            {
                return Conflict(new
                {
                    Success = false,
                    Message =
                        "Mobile number already exists."
                });
            }
        }

        // Permissions supplied in the CREATE payload are intentionally ignored.
        // New Pharmacists always start with every applicable module/action TRUE.

        // =================================================
        // CREATE TEMPORARY PASSWORD
        // =================================================

        var temporaryPassword =
            GenerateTemporaryPassword();

        // =================================================
        // CREATE USER
        // =================================================

        var pharmacist =
            new User
            {
                Name =
                    dto.Name.Trim(),

                Email =
                    normalizedEmail,

                MobileNumber =
                    normalizedMobile,

                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        temporaryPassword),

                Role =
                    "Pharmacist",

                PharmacyId =
                    adminPharmacyId.Value,

                BranchId =
                    selectedBranch.Id,

                AccountStatus =
                    "Active",

                PharmacyAssignedAt =
                    DateTime.UtcNow,

                PharmacyAssignedByUserId =
                    UserId,

                CreatedByUserId =
                    UserId,

                IsActive =
                    true,

                MustChangePassword =
                    true,

                CreatedAt =
                    DateTime.UtcNow
            };

        // =================================================
        // DATABASE TRANSACTION
        // =================================================

        int nextPublicId;
        await using var transaction =
            await _db.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            // ---------------------------------------------
            // Save Pharmacist first so pharmacist.Id exists
            // ---------------------------------------------

            _db.Users.Add(
                pharmacist);

            await _db.SaveChangesAsync();

            nextPublicId = (await _db.PharmacyPharmacistIdentities
                .Where(x => x.PharmacyId == adminPharmacyId.Value)
                .MaxAsync(x => (int?)x.PharmacistId) ?? 0) + 1;
            _db.PharmacyPharmacistIdentities.Add(new PharmacyPharmacistIdentity
            {
                PharmacyId = adminPharmacyId.Value,
                PharmacistId = nextPublicId,
                ExistingUserId = pharmacist.Id
            });
            await _db.SaveChangesAsync();

            // ---------------------------------------------
            // PERMISSIONS
            // ---------------------------------------------

            // ---------------------------------------------
            // DEFAULT USER PERMISSIONS
            // ---------------------------------------------
            // Requirement: every newly created Pharmacist starts
            // with ALL applicable permissions TRUE.
            // Any permission values sent in the CREATE request are
            // intentionally ignored. Admin can change them later
            // through PUT /api/pharmacy-admin/pharmacists/{id}/permissions.

            var defaultPermissions =
                PermissionModules
                    .Pharmacist
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(module =>
                        new UserPermission
                        {
                            UserId = pharmacist.Id,

                            Module = module,
                            CanView = true,
                            CanCreate = true,
                            CanEdit = true,
                            CanDelete = true,
                            AssignedByUserId = UserId,
                            UpdatedAt = DateTime.UtcNow
                        })
                    .ToList();

            _db.UserPermissions.AddRange(defaultPermissions);
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // =================================================
        // WELCOME EMAIL
        // =================================================

        var loginUrl =
            _configuration[
                "PharmacyFrontend:LoginUrl"];

        var emailSent =
            false;

        string? emailWarning =
            null;

        if (string.IsNullOrWhiteSpace(
                loginUrl))
        {
            emailWarning =
                "Pharmacist was created, but the pharmacy login URL is not configured.";
        }
        else
        {
            try
            {
                await _emailService
                    .SendPharmacistWelcomeEmailAsync(
                        pharmacist.Email!,
                        pharmacist.Name ??
                        "Pharmacist",
                        temporaryPassword,
                        loginUrl);

                emailSent =
                    true;
            }
            catch
            {
                emailWarning =
                    "Pharmacist was created, but the welcome email could not be sent.";
            }
        }

        // =================================================
        // RESPONSE
        // =================================================

        return CreatedAtAction(
            nameof(Get),
            new
            {
                id =
                    nextPublicId
            },
            new
            {
                Success =
                    true,

                Message =
                    emailSent
                        ? "Pharmacist created successfully. Login credentials were sent to the registered email address."
                        : emailWarning,

                EmailSent =
                    emailSent,

                Pharmacist = new
                {
                    PharmacistId = nextPublicId,
                    pharmacist.Name,
                    pharmacist.Email,
                    pharmacist.MobileNumber,
                    pharmacist.Role,
                    pharmacist.PharmacyId,
                    BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == selectedBranch.PharmacyId && i.ExistingBranchId == selectedBranch.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
                    pharmacist.IsActive,
                    pharmacist.MustChangePassword,
                    pharmacist.AccountStatus,

                    IsPharmacyAssigned =
                        true,

                    PermissionsAssigned =
                        true
                }
            });
    }

    // =====================================================
    // LIST PHARMACISTS
    // GET: /api/pharmacy-admin/pharmacists
    //
    // Users -> View
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var permissionError =
            await CheckPermission(
                "Users",
                "View");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacists =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId)
                .OrderBy(x =>
                    x.Name)
                .Select(x => new
                {
                    PharmacistId = _db.PharmacyPharmacistIdentities
                        .Where(i => i.PharmacyId == adminPharmacyId && i.ExistingUserId == x.Id)
                        .Select(i => (int?)i.PharmacistId).FirstOrDefault(),
                    x.Name,
                    x.Email,
                    x.MobileNumber,
                    x.Role,
                    x.PharmacyId,
                    BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.BranchId).Select(i => (int?)i.BranchId).FirstOrDefault(),

                    Pharmacy =
                        x.Pharmacy != null
                            ? x.Pharmacy.Name
                            : null,

                    BranchName =
                        x.Branch != null
                            ? x.Branch.Name
                            : null,

                    x.IsActive,
                    x.MustChangePassword,
                    x.AccountStatus,

                    IsPharmacyAssigned =
                        x.PharmacyId.HasValue &&
                        x.BranchId.HasValue &&
                        x.AccountStatus ==
                        "Active",

                    x.CreatedAt
                })
                .ToListAsync();

        return Ok(new
        {
            Success =
                true,

            Total =
                pharmacists.Count,

            Pharmacists =
                pharmacists
        });
    }

    // =====================================================
    // GET ONE PHARMACIST
    // GET: /api/pharmacy-admin/pharmacists/{id}
    //
    // Users -> View
    // =====================================================

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "View");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId)
                .Select(x => new
                {
                    PharmacistId = id,
                    x.Name,
                    x.Email,
                    x.MobileNumber,
                    x.Role,
                    x.PharmacyId,
                    BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.BranchId).Select(i => (int?)i.BranchId).FirstOrDefault(),

                    Pharmacy =
                        x.Pharmacy != null
                            ? x.Pharmacy.Name
                            : null,

                    BranchName =
                        x.Branch != null
                            ? x.Branch.Name
                            : null,

                    x.IsActive,
                    x.MustChangePassword,
                    x.AccountStatus,

                    IsPharmacyAssigned =
                        x.PharmacyId.HasValue &&
                        x.BranchId.HasValue &&
                        x.AccountStatus ==
                        "Active",

                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        return Ok(new
        {
            Success =
                true,

            Pharmacist =
                pharmacist
        });
    }

    // =====================================================
    // GET PHARMACIST PERMISSIONS
    // GET: /api/pharmacy-admin/pharmacists/{id}/permissions
    //
    // Users -> View
    // =====================================================

    [HttpGet("{id:int}/permissions")]
    public async Task<IActionResult> GetPermissions(
        int id)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "View");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId)
                .Select(x => new
                {
                    PharmacistId = id,
                    x.Name,
                    x.Email,
                    BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.BranchId).Select(i => (int?)i.BranchId).FirstOrDefault(),
                    x.AccountStatus
                })
                .FirstOrDefaultAsync();

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        var savedPermissions =
            await _db.UserPermissions
                .AsNoTracking()
                .Where(x =>
                    x.UserId == internalPharmacistId.Value)
                .ToListAsync();

        var permissions =
            PermissionModules
                .Pharmacist
                .Select(module =>
                {
                    var saved =
                        savedPermissions
                            .FirstOrDefault(x =>
                                string.Equals(
                                    x.Module,
                                    module,
                                    StringComparison.OrdinalIgnoreCase));

                    return new
                    {
                        Module =
                            module,

                        CanView =
                            saved?.CanView ?? true,

                        CanCreate =
                            saved?.CanCreate ?? true,

                        CanEdit =
                            saved?.CanEdit ?? true,

                        CanDelete =
                            saved?.CanDelete ?? true,

                        UpdatedAt =
                            saved?.UpdatedAt
                    };
                })
                .ToList();

        return Ok(new
        {
            Success =
                true,

            Pharmacist = new
            {
                pharmacist.PharmacistId,
                pharmacist.Name,
                pharmacist.Email
            },

            Permissions =
                permissions
        });
    }

    // =====================================================
    // UPDATE PHARMACIST
    // PUT: /api/pharmacy-admin/pharmacists/{id}
    //
    // Users -> Edit
    // =====================================================

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdatePharmacistDto dto)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "Edit");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId);

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        var normalizedEmail =
            dto.Email.Trim()
                .ToLowerInvariant();

        var normalizedMobile =
            dto.MobileNumber?.Trim();

        var emailExists =
            await _db.Users.AnyAsync(x =>
                x.Id != internalPharmacistId.Value &&
                x.Email != null &&
                x.Email.ToLower() ==
                normalizedEmail);

        if (emailExists)
        {
            return Conflict(new
            {
                Success =
                    false,

                Message =
                    "Email already exists."
            });
        }

        if (!string.IsNullOrWhiteSpace(
                normalizedMobile))
        {
            var mobileExists =
                await _db.Users.AnyAsync(x =>
                    x.Id != internalPharmacistId.Value &&
                    x.MobileNumber ==
                    normalizedMobile);

            if (mobileExists)
            {
                return Conflict(new
                {
                    Success =
                        false,

                    Message =
                        "Mobile number already exists."
                });
            }
        }

        pharmacist.Name =
            dto.Name.Trim();

        pharmacist.Email =
            normalizedEmail;

        pharmacist.MobileNumber =
            normalizedMobile;

        if (dto.BranchId.HasValue)
        {
            var targetBranch =
                await _db.Branches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == dto.BranchId.Value &&
                        x.PharmacyId == adminPharmacyId &&
                        x.IsActive);

            if (targetBranch == null)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message =
                        "The selected branch is inactive, invalid, or does not belong to your pharmacy."
                });
            }

            pharmacist.BranchId = targetBranch.Id;
            pharmacist.PharmacyAssignedAt = DateTime.UtcNow;
            pharmacist.PharmacyAssignedByUserId = UserId;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success =
                true,

            Message =
                "Pharmacist updated successfully."
        });
    }

    // =====================================================
    // DELETE / DEACTIVATE PHARMACIST
    // DELETE: /api/pharmacy-admin/pharmacists/{id}
    //
    // Users -> Delete
    // =====================================================

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "Delete");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId);

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        if (!pharmacist.IsActive)
        {
            return Ok(new
            {
                Success =
                    true,

                Message =
                    "Pharmacist is already inactive."
            });
        }

        pharmacist.IsActive =
            false;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success =
                true,

            Message =
                "Pharmacist deactivated successfully."
        });
    }

    // =====================================================
    // ASSIGN PHARMACY
    // PUT: /api/pharmacy-admin/pharmacists/{id}/pharmacy-assignment
    //
    // Users -> Edit
    // =====================================================

    [HttpPut("{id:int}/pharmacy-assignment")]
    public async Task<IActionResult> AssignPharmacy(
        int id,
        [FromBody] PharmacistBranchAssignmentDto dto)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission("Users", "Edit");

        if (permissionError != null)
            return permissionError;

        if (!IsPharmacyAssigned)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "The Pharmacy Admin must have an active pharmacy assignment first."
            });
        }

        var branch =
            await _db.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == x.PharmacyId && i.ExistingBranchId == x.Id).Select(i => (int?)i.BranchId).FirstOrDefault() == dto.BranchId &&
                    x.PharmacyId == PharmacyId &&
                    x.IsActive);

        if (branch == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message = "The selected branch is inactive, invalid, or does not belong to your pharmacy."
            });
        }

        var pharmacist =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == PharmacyId);

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success = false,
                Message = "Pharmacist was not found in your pharmacy."
            });
        }

        pharmacist.PharmacyId = PharmacyId;
        pharmacist.BranchId = branch.Id;
        pharmacist.AccountStatus = "Active";
        pharmacist.PharmacyAssignedAt = DateTime.UtcNow;
        pharmacist.PharmacyAssignedByUserId = UserId;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message = "Pharmacy branch assigned. The Pharmacist must log in again to receive a new scoped token.",
            Pharmacist = new
            {
                PharmacistId = id,
                pharmacist.PharmacyId,
                BranchId = _db.PharmacyBranchIdentities.Where(i => i.PharmacyId == branch.PharmacyId && i.ExistingBranchId == branch.Id).Select(i => (int?)i.BranchId).FirstOrDefault(),
                BranchName = branch.Name,
                pharmacist.AccountStatus,
                IsPharmacyAssigned = true
            }
        });
    }


    // =====================================================
    // ACTIVATE / DEACTIVATE PHARMACIST
    // PATCH: /api/pharmacy-admin/pharmacists/{id}/status
    //
    // Users -> Edit
    // =====================================================

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> Status(
        int id,
        [FromBody] StatusDto dto)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "Edit");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId);

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        pharmacist.IsActive =
            dto.IsActive;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success =
                true,

            Message =
                dto.IsActive
                    ? "Pharmacist activated successfully."
                    : "Pharmacist deactivated successfully."
        });
    }

    // =====================================================
    // UPDATE PHARMACIST PERMISSIONS
    // PUT: /api/pharmacy-admin/pharmacists/{id}/permissions
    //
    // Users -> Edit
    // =====================================================

    [HttpPut("{id:int}/permissions")]
    public async Task<IActionResult> Permissions(
        int id,
        [FromBody] List<PharmacyPermissionDto> dto)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "Edit");

        if (permissionError != null)
            return permissionError;

        dto ??=
            new List<PharmacyPermissionDto>();

        if (!IsPharmacyAssigned)
        {
            return BadRequest(new
            {
                Success =
                    false,

                Message =
                    "Permissions can be assigned only after the Pharmacy Admin has an active pharmacy assignment."
            });
        }

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId);

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        if (!pharmacist.PharmacyId.HasValue ||
            !pharmacist.BranchId.HasValue ||
            pharmacist.AccountStatus != "Active")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Assign an active pharmacy branch to this Pharmacist before assigning permissions."
            });
        }

        var pharmacistBranchIsValid =
            await _db.Branches
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == pharmacist.BranchId.Value &&
                    x.PharmacyId == PharmacyId &&
                    x.IsActive);

        if (pharmacist.PharmacyId != PharmacyId ||
            !pharmacistBranchIsValid)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        "You can manage permissions only for Pharmacists assigned to an active branch in your pharmacy."
                });
        }

        var allowedPharmacistModules =
            PermissionModules
                .Pharmacist
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var invalidModule =
            dto.FirstOrDefault(x =>
                string.IsNullOrWhiteSpace(
                    x.Module) ||
                !allowedPharmacistModules.Contains(
                    x.Module.Trim()));

        if (invalidModule != null)
        {
            return BadRequest(new
            {
                Success =
                    false,

                Message =
                    $"Invalid Pharmacist sidebar permission module: {invalidModule.Module}",

                AllowedModules =
                    PermissionModules
                        .Pharmacist
            });
        }

        // Admin may delegate only permissions
        // granted to Admin by Super Admin.
        foreach (var requested in dto)
        {
            foreach (var action in new[]
                     {
                         "View",
                         "Create",
                         "Edit",
                         "Delete"
                     })
            {
                var requestedAction =
                    action switch
                    {
                        "View" =>
                            requested.CanView,

                        "Create" =>
                            requested.CanCreate,

                        "Edit" =>
                            requested.CanEdit,

                        "Delete" =>
                            requested.CanDelete,

                        _ =>
                            false
                    };

                if (requestedAction &&
                    !await _permissionChecker
                        .HasPermissionAsync(
                            UserId,
                            PharmacyPermissionChecker
                                .ParentAdminModuleForPharmacist(
                                    requested.Module.Trim()),
                            action))
                {
                    return StatusCode(
                        StatusCodes.Status403Forbidden,
                        new
                        {
                            Success =
                                false,

                            Message =
                                $"You cannot grant {action} permission for {requested.Module} because it was not granted to your Admin role by Super Admin."
                        });
                }
            }
        }

        var duplicateModules =
            dto
                .Where(x =>
                    !string.IsNullOrWhiteSpace(
                        x.Module))
                .GroupBy(
                    x => x.Module.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .Where(group =>
                    group.Count() > 1)
                .Select(group =>
                    group.Key)
                .ToList();

        if (duplicateModules.Count > 0)
        {
            return BadRequest(new
            {
                Success =
                    false,

                Message =
                    "Duplicate permission modules are not allowed.",

                Modules =
                    duplicateModules
            });
        }

        // Keep all Pharmacist modules present. Missing modules are
        // created as TRUE by default. Only modules explicitly supplied
        // by the Admin are changed. Existing values for omitted modules
        // are preserved.
        var existingPermissions =
            await _db.UserPermissions
                .Where(x => x.UserId == internalPharmacistId.Value)
                .ToListAsync();

        var byModule = existingPermissions
            .GroupBy(x => x.Module, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var requestedByModule = dto
            .Where(x => !string.IsNullOrWhiteSpace(x.Module))
            .ToDictionary(x => x.Module.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var module in PermissionModules.Pharmacist.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!byModule.TryGetValue(module, out var permission))
            {
                permission = new UserPermission
                {
                    // Permissions reference Users.Id (global internal key), not the
                    // pharmacy-scoped public PharmacistId from the route.
                    UserId = internalPharmacistId.Value,

                    Module = module,
                    CanView = true,
                    CanCreate = true,
                    CanEdit = true,
                    CanDelete = true,
                    AssignedByUserId = UserId,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserPermissions.Add(permission);
            }

            if (requestedByModule.TryGetValue(module, out var supplied))
            {
                permission.CanView = supplied.CanView;
                permission.CanCreate = supplied.CanCreate;
                permission.CanEdit = supplied.CanEdit;
                permission.CanDelete = supplied.CanDelete;
                permission.AssignedByUserId = UserId;
                permission.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success =
                true,

            Message =
                "Pharmacist permissions updated successfully.",

            PermissionCount =
                PermissionModules.Pharmacist.Length
        });
    }

    // =====================================================
    // RESET PHARMACIST PASSWORD
    // POST: /api/pharmacy-admin/pharmacists/{id}/reset-password
    //
    // Users -> Edit
    // =====================================================

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id,
        [FromBody] ResetPasswordDto dto)
    {
        var internalPharmacistId = await ResolvePharmacistUserIdAsync(id);
        if (!internalPharmacistId.HasValue)
            return NotFound(new { Success = false, Message = "Pharmacist was not found in your pharmacy." });

        var permissionError =
            await CheckPermission(
                "Users",
                "Edit");

        if (permissionError != null)
            return permissionError;

        var adminPharmacyId =
            PharmacyId;

        var pharmacist =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == internalPharmacistId.Value &&
                    x.Role == "Pharmacist" &&
                    x.PharmacyId == adminPharmacyId);

        if (pharmacist == null)
        {
            return NotFound(new
            {
                Success =
                    false,

                Message =
                    "Pharmacist was not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.NewPassword) ||
            dto.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                Success =
                    false,

                Message =
                    "Password must contain at least 8 characters."
            });
        }

        pharmacist.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        pharmacist.MustChangePassword =
            true;

        pharmacist.PasswordResetOtpHash =
            null;

        pharmacist.PasswordResetOtpExpiresAt =
            null;

        pharmacist.PasswordResetOtpLastSentAt =
            null;

        pharmacist.PasswordResetOtpAttempts =
            0;

        pharmacist.PasswordResetTokenHash =
            null;

        pharmacist.PasswordResetTokenExpiresAt =
            null;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success =
                true,

            Message =
                "Temporary password created. The pharmacist must change it after login."
        });
    }

    // =====================================================
    // TEMPORARY PASSWORD
    // =====================================================

    private static string GenerateTemporaryPassword()
    {
        return
            $"Pharm@{Random.Shared.Next(100000, 999999)}!";
    }
}

public sealed class PharmacistBranchAssignmentDto
{
    public int BranchId { get; set; }
}

