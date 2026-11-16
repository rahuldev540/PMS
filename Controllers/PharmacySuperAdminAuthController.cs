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
[Route("api/pharmacy-super-admin-auth")]
public class PharmacySuperAdminAuthController : ControllerBase
{
    private readonly AppDbContext _db;

    public PharmacySuperAdminAuthController(
        AppDbContext db)
    {
        _db = db;
    }

    // Sign-in is intentionally exposed only through POST /api/auth/login.
    // This controller keeps Super Admin profile/password/logout endpoints only.

    // ============================================================
    // PROFILE
    // GET: /api/pharmacy-super-admin-auth/profile
    // ============================================================

    [Authorize(Roles = "PharmacySuperAdmin")]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = "Invalid user token."
            });
        }

        var superAdmin =
            await _db.Users
                .AsNoTracking()
                .Where(x =>
                    x.Id == userId &&
                    x.Role ==
                    "PharmacySuperAdmin")
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Email,
                    x.MobileNumber,
                    x.Role,
                    x.IsActive,
                    x.MustChangePassword,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

        if (superAdmin == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Pharmacy Super Admin profile was not found."
            });
        }

        return Ok(new
        {
            Success = true,

            Profile = new
            {
                superAdmin.Id,

                superAdmin.Name,

                superAdmin.Email,

                superAdmin.MobileNumber,

                superAdmin.Role,

                RoleDisplayName =
                    "Super Admin",

                superAdmin.IsActive,

                Status =
                    superAdmin.IsActive
                        ? "Active"
                        : "Inactive",

                superAdmin.MustChangePassword,

                superAdmin.CreatedAt
            }
        });
    }

    // ============================================================
    // CHANGE PASSWORD
    // POST: /api/pharmacy-super-admin-auth/change-password
    // ============================================================

    [Authorize(Roles = "PharmacySuperAdmin")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordDto dto)
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = "Invalid user token."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.CurrentPassword))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Current password is required."
            });
        }

        if (string.IsNullOrWhiteSpace(
                dto.NewPassword))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "New password is required."
            });
        }

        var superAdmin =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == userId &&
                    x.IsActive &&
                    x.Role ==
                    "PharmacySuperAdmin");

        if (superAdmin == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Pharmacy Super Admin was not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
                superAdmin.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(
                dto.CurrentPassword,
                superAdmin.PasswordHash))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Current password is incorrect."
            });
        }

        if (!IsStrongPassword(
                dto.NewPassword))
        {
            return BadRequest(new
            {
                Success = false,

                Message =
                    "New password must contain at least 8 characters, one uppercase letter, one lowercase letter, one number and one special character."
            });
        }

        if (BCrypt.Net.BCrypt.Verify(
                dto.NewPassword,
                superAdmin.PasswordHash))
        {
            return BadRequest(new
            {
                Success = false,

                Message =
                    "New password must be different from the current password."
            });
        }

        superAdmin.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        superAdmin.MustChangePassword = false;

        // Clear any password reset related data
        superAdmin.PasswordResetOtpHash = null;
        superAdmin.PasswordResetOtpExpiresAt = null;
        superAdmin.PasswordResetOtpLastSentAt = null;
        superAdmin.PasswordResetOtpAttempts = 0;
        superAdmin.PasswordResetTokenHash = null;
        superAdmin.PasswordResetTokenExpiresAt = null;

        AddPasswordChangeAudit(superAdmin);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Password changed successfully."
        });
    }

    // ============================================================
    // LOGOUT
    // POST: /api/pharmacy-super-admin-auth/logout
    // ============================================================

    [Authorize(Roles = "PharmacySuperAdmin")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message = "Invalid user token."
            });
        }

        var activity =
            await _db.AuditLogs
                .Where(x =>
                    x.UserId == userId &&
                    x.Role ==
                    "PharmacySuperAdmin" &&
                    x.IsLoginActivity &&
                    x.IsOnline)
                .OrderByDescending(x =>
                    x.LoginTime)
                .FirstOrDefaultAsync();

        if (activity != null)
        {
            activity.IsOnline = false;

            activity.LogoutTime =
                DateTime.UtcNow;

            activity.Timestamp =
                DateTime.UtcNow;

            activity.SystemAction =
                "Pharmacy Super Admin Logout";

            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            Success = true,

            Message =
                "Pharmacy Super Admin logged out successfully."
        });
    }

    // ============================================================
    // LOGIN AUDIT
    // ============================================================

    private void AddLoginAudit(
        User superAdmin)
    {
        _db.AuditLogs.Add(
            new AuditLog
            {
                UserId =
                    superAdmin.Id,

                UserName =
                    superAdmin.Name ??
                    string.Empty,

                Role =
                    "PharmacySuperAdmin",

                BranchId = null,

                Action =
                    "Login",

                SystemAction =
                    "Pharmacy Super Admin Login",

                IsLoginActivity =
                    true,

                IpAddress =
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                Browser =
                    Request.Headers
                        .UserAgent
                        .ToString(),

                Device =
                    "Unknown",

                LoginTime =
                    DateTime.UtcNow,

                LogoutTime = null,

                IsOnline =
                    true,

                Timestamp =
                    DateTime.UtcNow
            });
    }

    // ============================================================
    // PASSWORD CHANGE AUDIT
    // ============================================================

    private void AddPasswordChangeAudit(
        User superAdmin)
    {
        _db.AuditLogs.Add(
            new AuditLog
            {
                UserId =
                    superAdmin.Id,

                UserName =
                    superAdmin.Name ??
                    string.Empty,

                Role =
                    "PharmacySuperAdmin",

                BranchId = null,

                Action =
                    "Change Password",

                SystemAction =
                    "Pharmacy Super Admin changed account password",

                IsLoginActivity =
                    false,

                IpAddress =
                    HttpContext.Connection
                        .RemoteIpAddress?
                        .ToString(),

                Browser =
                    Request.Headers
                        .UserAgent
                        .ToString(),

                Device =
                    "Unknown",

                IsOnline =
                    true,

                Timestamp =
                    DateTime.UtcNow
            });
    }

    // ============================================================
    // PASSWORD VALIDATION
    //
    // Frontend requirements:
    // Minimum 8 characters
    // 1 uppercase
    // 1 lowercase
    // 1 number
    // 1 special character
    // ============================================================

    private static bool IsStrongPassword(
        string password)
    {
        if (string.IsNullOrWhiteSpace(
                password))
        {
            return false;
        }

        if (password.Length < 8)
        {
            return false;
        }

        if (!password.Any(
                char.IsUpper))
        {
            return false;
        }

        if (!password.Any(
                char.IsLower))
        {
            return false;
        }

        if (!password.Any(
                char.IsDigit))
        {
            return false;
        }

        if (!password.Any(
                character =>
                    !char.IsLetterOrDigit(
                        character)))
        {
            return false;
        }

        return true;
    }
}