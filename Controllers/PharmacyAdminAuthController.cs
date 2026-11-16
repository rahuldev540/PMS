using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;
using AuthDemo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-admin-auth")]
public class PharmacyAdminAuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public PharmacyAdminAuthController(
        AppDbContext db)
    {
        _db = db;
    }

    // =====================================================
    // IMPORTANT ARCHITECTURE
    //
    // Pharmacy Admin:
    //   PharmacyId = REQUIRED
    //   Pharmacy-scoped authentication
    //   BranchId   = NOT REQUIRED
    //
    // Pharmacy Admin belongs to the entire pharmacy.
    // The Admin can create/manage multiple branches.
    //
    // Pharmacists remain branch-specific.
    //
    // Sign-in is handled through:
    // POST /api/auth/login
    // =====================================================


    // =====================================================
    // GET ASSIGNMENT STATUS
    //
    // GET:
    // /api/pharmacy-admin-auth/assignment-status
    //
    // BranchId is intentionally NOT required.
    // =====================================================

    [Authorize(Roles = "PharmacyAdmin")]
    [HttpGet("assignment-status")]
    public async Task<IActionResult> AssignmentStatus()
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value
            ??
            User.FindFirst("userId")?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Invalid authentication token."
            });
        }

        var admin =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == userId &&
                    x.IsActive &&
                    x.Role == "PharmacyAdmin");

        if (admin == null)
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Pharmacy Admin was not found."
            });
        }


        // =================================================
        // NEW ASSIGNMENT RULE
        //
        // Pharmacy Admin is assigned when:
        //
        // AccountStatus = Active
        // IsActive      = true
        // PharmacyId    != null
        // Pharmacy      = active
        //
        // BranchId is not required for Admin login.
        // =================================================

        var assigned =
            admin.IsActive &&
            admin.AccountStatus == "Active" &&
            admin.PharmacyId.HasValue;


        // =================================================
        // VALIDATE ASSIGNED PHARMACY
        // =================================================

        AuthDemo.Models.PharmacyTenant? pharmacy = null;

        if (admin.PharmacyId.HasValue)
        {
            pharmacy =
                await _db.Pharmacies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == admin.PharmacyId.Value &&
                        x.IsActive);
        }


        var validAssignment =
            assigned &&
            pharmacy != null;


        // =================================================
        // TOKEN ASSIGNMENT CLAIM
        // =================================================

        var tokenAssignmentClaim =
            User.FindFirst(
                "IsPharmacyAssigned")?.Value
            ??
            User.FindFirst(
                "isPharmacyAssigned")?.Value;

        var tokenSaysAssigned =
            string.Equals(
                tokenAssignmentClaim,
                "true",
                StringComparison.OrdinalIgnoreCase);


        // =================================================
        // BRANCH SUMMARY
        //
        // Business branches only.
        // PharmacySystem/internal rows are excluded.
        // =================================================

        var totalBranches = 0;
        var activeBranches = 0;
        var inactiveBranches = 0;

        if (validAssignment &&
            admin.PharmacyId.HasValue)
        {
            var branchQuery =
                _db.Branches
                    .AsNoTracking()
                    .Where(x =>
                        x.PharmacyId ==
                            admin.PharmacyId.Value);

            totalBranches =
                await branchQuery.CountAsync();

            activeBranches =
                await branchQuery.CountAsync(x =>
                    x.IsActive);

            inactiveBranches =
                await branchQuery.CountAsync(x =>
                    !x.IsActive);
        }


        int? publicBranchId = null;
        if (admin.PharmacyId.HasValue && admin.BranchId.HasValue)
        {
            publicBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(
                _db, admin.PharmacyId.Value, admin.BranchId.Value);
        }

        return Ok(new
        {
            Success = true,

            admin.AccountStatus,

            admin.PharmacyId,

            PharmacyName =
                pharmacy != null
                    ? pharmacy.Name
                    : null,

            OperationMode = "Standalone",

            // Pharmacy Admin is NOT assigned to one branch.
            // This can normally remain null.
            BranchId = publicBranchId,

            IsPharmacyAssigned =
                validAssignment,

            CanAccessPharmacyOperations =
                validAssignment,

            CanManageBranches =
                validAssignment,

            BranchSummary = new
            {
                TotalBranches =
                    totalBranches,

                ActiveBranches =
                    activeBranches,

                InactiveBranches =
                    inactiveBranches
            },

            // If assignment happened after this JWT was
            // generated, Admin should login again so the
            // new PharmacyId/assignment claims are included.
            RequiresNewLogin =
                validAssignment &&
                !tokenSaysAssigned
        });
    }


    // =====================================================
    // GET CURRENT ADMIN PERMISSIONS
    //
    // GET:
    // /api/pharmacy-admin-auth/permissions
    // =====================================================

    [Authorize(Roles = "PharmacyAdmin")]
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("userId")?.Value;
        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { Success = false, Message = "Invalid authentication token." });

        var admin = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive && x.Role == "PharmacyAdmin")
            .Select(x => new { x.Id, x.Name, x.Email, x.PharmacyId, x.AccountStatus })
            .FirstOrDefaultAsync();
        if (admin == null) return Unauthorized(new { Success = false, Message = "Pharmacy Admin was not found." });

        var activePharmacyExists = admin.PharmacyId.HasValue && await _db.Pharmacies.AsNoTracking()
            .AnyAsync(x => x.Id == admin.PharmacyId.Value && x.IsActive);
        var isPharmacyAssigned = admin.AccountStatus == "Active" && admin.PharmacyId.HasValue && activePharmacyExists;

        var savedPermissions = await _db.UserPermissions.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var permissions = AuthDemo.Constants.PermissionModules.PharmacyAdmin
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(module =>
            {
                var saved = savedPermissions.FirstOrDefault(x =>
                    string.Equals(x.Module, module, StringComparison.OrdinalIgnoreCase));

                return new
                {
                    Module = module,
                    CanView = saved?.CanView ?? true,
                    CanCreate = saved?.CanCreate ?? true,
                    CanEdit = saved?.CanEdit ?? true,
                    CanDelete = saved?.CanDelete ?? true,
                    UpdatedAt = saved?.UpdatedAt
                };
            })
            .ToList();

        return Ok(new
        {
            Success = true,
            UserId = admin.Id,
            Name = admin.Name,
            Email = admin.Email,
            IsPharmacyAssigned = isPharmacyAssigned,
            Permissions = permissions
        });
    }

    // =====================================================
    // CHANGE PHARMACY ADMIN PASSWORD
    //
    // POST:
    // /api/pharmacy-admin-auth/change-password
    // =====================================================

    [Authorize(Roles = "PharmacyAdmin")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordDto dto)
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value
            ??
            User.FindFirst("userId")?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Invalid authentication token."
            });
        }


        var admin =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == userId &&
                    x.IsActive &&
                    x.Role ==
                        "PharmacyAdmin");


        if (admin == null)
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Pharmacy Admin was not found."
            });
        }


        if (string.IsNullOrWhiteSpace(
                admin.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(
                dto.CurrentPassword,
                admin.PasswordHash))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Current password is incorrect."
            });
        }


        if (string.IsNullOrWhiteSpace(
                dto.NewPassword) ||
            dto.NewPassword.Length < 8)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "New password must contain at least 8 characters."
            });
        }


        if (string.Equals(
            dto.CurrentPassword,
            dto.NewPassword,
            StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "New password must be different from the current password."
            });
        }


        admin.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        admin.MustChangePassword =
            false;


        await _db.SaveChangesAsync();


        return Ok(new
        {
            Success = true,

            Message =
                "Password changed successfully."
        });
    }


    // =====================================================
    // PHARMACY ADMIN LOGOUT
    //
    // POST:
    // /api/pharmacy-admin-auth/logout
    // =====================================================

    [Authorize(Roles = "PharmacyAdmin")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value
            ??
            User.FindFirst("userId")?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Invalid authentication token."
            });
        }


        var loginActivity =
            await _db.AuditLogs
                .Where(x =>
                    x.UserId == userId &&
                    x.Role ==
                        "PharmacyAdmin" &&
                    x.IsLoginActivity &&
                    x.IsOnline)
                .OrderByDescending(x =>
                    x.LoginTime)
                .FirstOrDefaultAsync();


        if (loginActivity != null)
        {
            loginActivity.IsOnline =
                false;

            loginActivity.LogoutTime =
                DateTime.UtcNow;

            loginActivity.Timestamp =
                DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }


        return Ok(new
        {
            Success = true,

            Message =
                "Pharmacy Admin logged out successfully."
        });
    }
}