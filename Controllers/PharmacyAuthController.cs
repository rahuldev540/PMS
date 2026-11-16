using AuthDemo.Data;
using AuthDemo.Helpers;
using AuthDemo.DTOs;
using AuthDemo.Services.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-auth")]
public class PharmacyAuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public PharmacyAuthController(
        AppDbContext db,
        IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    // =====================================================
    // GET CURRENT USER ID
    // =====================================================

    private bool TryGetCurrentUserId(
        out int userId)
    {
        var value =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(
            value,
            out userId);
    }

    // =====================================================
    // FORGOT PASSWORD
    // POST /api/pharmacy-auth/forgot-password
    // =====================================================

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordDto dto)
    {
        const string responseMessage =
            "If the email belongs to an active pharmacist, an OTP has been sent.";

        if (string.IsNullOrWhiteSpace(
                dto.Email))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Email is required."
            });
        }

        var email =
            dto.Email
                .Trim()
                .ToLowerInvariant();

        var user =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    x.Role ==
                    "Pharmacist" &&
                    x.Email != null &&
                    x.Email.ToLower() ==
                    email);

        // Do not expose whether the email exists.
        if (user == null)
        {
            return Ok(new
            {
                Success = true,
                Message =
                    responseMessage
            });
        }

        var now =
            DateTime.UtcNow;

        if (user.PasswordResetOtpLastSentAt.HasValue &&
            now -
            user.PasswordResetOtpLastSentAt.Value <
            TimeSpan.FromMinutes(1))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new
                {
                    Success = false,
                    Message =
                        "Please wait one minute before requesting another OTP."
                });
        }

        var otp =
            RandomNumberGenerator
                .GetInt32(
                    100000,
                    1000000)
                .ToString();

        user.PasswordResetOtpHash =
            BCrypt.Net.BCrypt.HashPassword(
                otp);

        user.PasswordResetOtpExpiresAt =
            now.AddMinutes(10);

        user.PasswordResetOtpLastSentAt =
            now;

        user.PasswordResetOtpAttempts =
            0;

        user.PasswordResetTokenHash =
            null;

        user.PasswordResetTokenExpiresAt =
            null;

        await _db.SaveChangesAsync();

        await _emailService
            .SendPasswordResetOtpAsync(
                user.Email!,
                user.Name ??
                "Pharmacist",
                otp);

        return Ok(new
        {
            Success = true,
            Message =
                responseMessage
        });
    }

    // =====================================================
    // VERIFY RESET OTP
    // POST /api/pharmacy-auth/verify-reset-otp
    // =====================================================

    [AllowAnonymous]
    [HttpPost("verify-reset-otp")]
    public async Task<IActionResult> VerifyResetOtp(
        [FromBody] VerifyPasswordResetOtpDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.Email) ||
            string.IsNullOrWhiteSpace(
                dto.Otp))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Email and OTP are required."
            });
        }

        var email =
            dto.Email
                .Trim()
                .ToLowerInvariant();

        var user =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    x.Role ==
                    "Pharmacist" &&
                    x.Email != null &&
                    x.Email.ToLower() ==
                    email);

        var now =
            DateTime.UtcNow;

        if (user == null ||
            string.IsNullOrWhiteSpace(
                user.PasswordResetOtpHash) ||
            !user.PasswordResetOtpExpiresAt.HasValue ||
            user.PasswordResetOtpExpiresAt.Value <=
            now ||
            user.PasswordResetOtpAttempts >=
            5)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Invalid or expired OTP."
            });
        }

        user.PasswordResetOtpAttempts++;

        if (!BCrypt.Net.BCrypt.Verify(
                dto.Otp.Trim(),
                user.PasswordResetOtpHash))
        {
            await _db.SaveChangesAsync();

            return BadRequest(new
            {
                Success = false,
                Message =
                    "Invalid or expired OTP."
            });
        }

        var resetToken =
            Convert.ToHexString(
                RandomNumberGenerator
                    .GetBytes(32));

        user.PasswordResetTokenHash =
            HashToken(
                resetToken);

        user.PasswordResetTokenExpiresAt =
            now.AddMinutes(15);

        user.PasswordResetOtpHash =
            null;

        user.PasswordResetOtpExpiresAt =
            null;

        user.PasswordResetOtpAttempts =
            0;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "OTP verified.",

            ResetToken =
                resetToken,

            ExpiresInMinutes =
                15
        });
    }

    // =====================================================
    // RESET FORGOTTEN PASSWORD
    // POST /api/pharmacy-auth/reset-password
    // =====================================================

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetForgottenPassword(
        [FromBody] ResetForgottenPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(
                dto.ResetToken))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Reset token is required."
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

        var tokenHash =
            HashToken(
                dto.ResetToken.Trim());

        var now =
            DateTime.UtcNow;

        var user =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    x.Role ==
                    "Pharmacist" &&
                    x.PasswordResetTokenHash ==
                    tokenHash &&
                    x.PasswordResetTokenExpiresAt >
                    now);

        if (user == null)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Invalid or expired reset token."
            });
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        user.MustChangePassword =
            false;

        user.PasswordResetTokenHash =
            null;

        user.PasswordResetTokenExpiresAt =
            null;

        user.PasswordResetOtpHash =
            null;

        user.PasswordResetOtpExpiresAt =
            null;

        user.PasswordResetOtpLastSentAt =
            null;

        user.PasswordResetOtpAttempts =
            0;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Password reset successfully. You can now log in."
        });
    }

    // =====================================================
    // HASH RESET TOKEN
    // =====================================================

    private static string HashToken(
        string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    token)));
    }

    // Sign-in is intentionally exposed only through POST /api/auth/login.
    // This controller keeps Pharmacist password-reset/change-password/logout endpoints only.

    // =====================================================
    // ASSIGNMENT STATUS
    // GET /api/pharmacy-auth/assignment-status
    // =====================================================

    [Authorize(Roles = "Pharmacist")]
    [HttpGet("assignment-status")]
    public async Task<IActionResult> AssignmentStatus()
    {
        if (!TryGetCurrentUserId(
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Invalid authentication token."
            });
        }

        var user =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == userId &&
                    x.IsActive &&
                    x.Role ==
                    "Pharmacist");

        if (user == null)
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Pharmacist was not found."
            });
        }

        var assigned =
            user.AccountStatus ==
            "Active" &&
            user.PharmacyId.HasValue &&
            user.BranchId.HasValue;

        int? publicBranchId = null;
        if (user.PharmacyId.HasValue && user.BranchId.HasValue)
        {
            publicBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(
                _db, user.PharmacyId.Value, user.BranchId.Value);
        }

        return Ok(new
        {
            Success = true,

            user.AccountStatus,

            user.PharmacyId,

            BranchId = publicBranchId,

            IsPharmacyAssigned =
                assigned,

            CanAccessPharmacyOperations =
                assigned,

            RequiresNewLogin =
                assigned &&
                !string.Equals(
                    User.FindFirst(
                        "IsPharmacyAssigned")?.Value,
                    "true",
                    StringComparison.OrdinalIgnoreCase)
        });
    }

    // =====================================================
    // CHANGE PASSWORD
    // POST /api/pharmacy-auth/change-password
    // =====================================================

    [Authorize(Roles = "Pharmacist")]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordDto dto)
    {
        if (!TryGetCurrentUserId(
                out var userId))
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Invalid authentication token."
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

        var user =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == userId &&
                    x.IsActive &&
                    x.Role ==
                    "Pharmacist");

        if (user == null)
        {
            return Unauthorized(new
            {
                Success = false,
                Message =
                    "Pharmacist was not found."
            });
        }

        if (string.IsNullOrWhiteSpace(
                user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(
                dto.CurrentPassword,
                user.PasswordHash))
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Current password is incorrect."
            });
        }

        user.PasswordHash =
            BCrypt.Net.BCrypt.HashPassword(
                dto.NewPassword);

        user.MustChangePassword =
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
    // PHARMACIST LOGOUT
    // POST /api/pharmacy-auth/logout
    // =====================================================

    [Authorize(Roles = "Pharmacist")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (!TryGetCurrentUserId(
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
                    x.UserId ==
                    userId &&
                    x.Role ==
                    "Pharmacist" &&
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
                "Pharmacist logged out successfully."
        });
    }
}